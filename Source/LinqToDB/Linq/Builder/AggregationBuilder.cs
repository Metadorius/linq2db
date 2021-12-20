﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace LinqToDB.Linq.Builder
{
	using LinqToDB.Expressions;
	using Extensions;
	using Mapping;
	using SqlQuery;
	using LinqToDB.Reflection;
	using LinqToDB.Common.Internal;

	class AggregationBuilder : MethodCallBuilder
	{
		public  static readonly string[] MethodNames      = { "Average"     , "Min"     , "Max"     , "Sum"      };
		private static readonly string[] MethodNamesAsync = { "AverageAsync", "MinAsync", "MaxAsync", "SumAsync" };

		public static Sql.ExpressionAttribute? GetAggregateDefinition(MethodCallExpression methodCall, MappingSchema mapping)
		{
			var functions = mapping.GetAttributes<Sql.ExpressionAttribute>(methodCall.Method.ReflectedType!,
				methodCall.Method,
				f => f.Configuration);
			return functions.FirstOrDefault(f => f.IsAggregate || f.IsWindowFunction);
		}

		protected override bool CanBuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			if (methodCall.IsQueryable(MethodNames) || methodCall.IsAsyncExtension(MethodNamesAsync))
				return true;

			return false;
		}

		public override bool IsAggregationContext(ExpressionBuilder builder, BuildInfo buildInfo)
		{
			return true;
		}

		protected override IBuildContext BuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			var methodName = methodCall.Method.Name.Replace("Async", "");

			var inGrouping = false;

			IBuildContext?      sequence = null;
			SqlSearchCondition? filter   = null;

			if (buildInfo.IsSubQuery)
			{
				var testSequence = builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[0], new SelectQuery()) { AggregationTest = true });

				// It means that as root we have used fake context
				var testSelectQuery = testSequence.SelectQuery;
				if (testSelectQuery.From.Tables.Count == 0)
				{
					var valid = true;
					if (!testSelectQuery.Where.IsEmpty)
					{
						valid = false;
						/*
						switch (methodName)
						{
							case "Sum":
							{
								filter = testSelectQuery.
							}
						}
						*/
					}

					if (valid)
					{
						sequence = builder.BuildSequence(
							new BuildInfo(buildInfo, methodCall.Arguments[0]) { CreateSubQuery = false });
						inGrouping = true;
					}
				}
			}

			if (sequence == null)
			{
				sequence = builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[0]) { CreateSubQuery = true });
			}

			var prevSequence = sequence;

			if (!inGrouping && buildInfo.IsSubQuery)
			{
				// Wrap by subquery to handle aggregate limitations, especially for SQL Server
				//
				sequence = new SubQueryContext(sequence);

				if (prevSequence.SelectQuery.OrderBy.Items.Count > 0)
				{
					if (prevSequence.SelectQuery.Select.TakeValue == null &&
					    prevSequence.SelectQuery.Select.SkipValue == null)
						prevSequence.SelectQuery.OrderBy.Items.Clear();
				}
			}

			var context = new AggregationContext(buildInfo.Parent, sequence, methodCall);

			var refExpression  = new ContextRefExpression(methodCall.Arguments[0].Type, prevSequence);
			var sqlPlaceholder = builder.ConvertToSqlPlaceholder(context, refExpression);

			var sql = sqlPlaceholder.Sql;

			var functionPlaceholder = ExpressionBuilder.CreatePlaceholder(context, /*context*/
				new SqlFunction(methodCall.Type, methodName, true, sql.Sql) { CanBeNull = true }, buildInfo.Expression);

			functionPlaceholder.Alias = methodName;

			//functionPlaceholder = (SqlPlaceholderExpression)builder.UpdateNesting(context, functionPlaceholder);

			if (!inGrouping && buildInfo.IsSubQuery)
			{
				builder.MakeColumn(context, functionPlaceholder);

				functionPlaceholder = ExpressionBuilder.CreatePlaceholder(buildInfo.Parent, context.SelectQuery, functionPlaceholder.MemberExpression, functionPlaceholder.ConvertType);
			}

			context.Placeholder = functionPlaceholder;

			return context;
		}

		protected override SequenceConvertInfo? Convert(
			ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo, ParameterExpression? param)
		{
			return null;
		}

		class AggregationContext : SequenceContextBase
		{
			public AggregationContext(IBuildContext? parent, IBuildContext sequence, MethodCallExpression methodCall)
				: base(parent, sequence, null)
			{
				_returnType = methodCall.Method.ReturnType;
				_methodName = methodCall.Method.Name;

				if (_returnType.IsGenericType && _returnType.GetGenericTypeDefinition() == typeof(Task<>))
				{
					_returnType = _returnType.GetGenericArguments()[0];
					_methodName = _methodName.Replace("Async", "");
				}
			}

			readonly string     _methodName;
			readonly Type       _returnType;
			private  SqlInfo[]? _index;
			private  int?       _parentIndex;

			public SqlPlaceholderExpression Placeholder = null!;

			static int CheckNullValue(bool isNull, object context)
			{
				if (isNull)
					throw new InvalidOperationException(
						$"Function {context} returns non-nullable value, but result is NULL. Use nullable version of the function instead.");
				return 0;
			}

			public override void BuildQuery<T>(Query<T> query, ParameterExpression queryParameter)
			{
				var expr = Builder.FinalizeProjection(this,
					Builder.MakeExpression(new ContextRefExpression(typeof(T), this), ProjectFlags.Expression));

				var mapper = Builder.BuildMapper<object>(expr);

				QueryRunner.SetRunQuery(query, mapper);

				//throw new NotImplementedException();
				/*var expr   = BuildExpression(FieldIndex, Sql);
				var mapper = Builder.BuildMapper<object>(expr);

				CompleteColumns();
				QueryRunner.SetRunQuery(query, mapper);*/
			}

			public override Expression MakeExpression(Expression path, ProjectFlags flags)
			{
				return Placeholder;
			}

			public override Expression BuildExpression(Expression? expression, int level, bool enforceServerSide)
			{
				throw new NotImplementedException();
				var info  = ConvertToIndex(expression, level, ConvertFlags.Field)[0];
				var index = info.Index;
				if (Parent != null)
					index = ConvertToParentIndex(index, Parent);
				return BuildExpression(index, info.Sql);
			}

			Expression BuildExpression(int fieldIndex, ISqlExpression? sqlExpression)
			{
				throw new NotImplementedException();

				Expression expr;

				if (SequenceHelper.UnwrapSubqueryContext(Sequence) is DefaultIfEmptyBuilder.DefaultIfEmptyContext defaultIfEmpty)
				{
					expr = Builder.BuildSql(_returnType, fieldIndex, sqlExpression);
					if (defaultIfEmpty.DefaultValue != null && expr is ConvertFromDataReaderExpression convert)
					{
						var generator = new ExpressionGenerator();
						expr = convert.MakeNullable();
						if (expr.Type.IsNullable())
						{
							var exprVar      = generator.AssignToVariable(expr, "nullable");
							var defaultValue = defaultIfEmpty.DefaultValue;
							if (defaultValue.Type != expr.Type)
							{
								var convertLambda = Builder.MappingSchema.GenerateSafeConvert(defaultValue.Type, expr.Type);
								defaultValue = InternalExtensions.ApplyLambdaToExpression(convertLambda, defaultValue);
							}

							var resultVar = generator.AssignToVariable(defaultValue, "result");
							
							generator.AddExpression(Expression.IfThen(
								Expression.NotEqual(exprVar, ExpressionInstances.UntypedNull),
								Expression.Assign(resultVar, Expression.Convert(exprVar, resultVar.Type))));

							generator.AddExpression(resultVar);

							expr = generator.Build();
						}
					}
				}
				else
				if (_methodName == "Sum" || _returnType.IsNullableType())
				{
					expr = Builder.BuildSql(_returnType, fieldIndex, sqlExpression);
				}
				else
				{
					expr = Expression.Block(
						Expression.Call(null, MemberHelper.MethodOf(() => CheckNullValue(false, null!)), Expression.Call(ExpressionBuilder.DataReaderParam, Methods.ADONet.IsDBNull, ExpressionInstances.Constant0), Expression.Constant(_methodName)),
						Builder.BuildSql(_returnType, fieldIndex, sqlExpression));
				}

				return expr;
			}

			public override SqlInfo[] ConvertToSql(Expression? expression, int level, ConvertFlags flags)
			{
				throw new NotImplementedException();

				switch (flags)
				{
					case ConvertFlags.All   :
					case ConvertFlags.Key   :
					case ConvertFlags.Field : return Sequence.ConvertToSql(expression, level + 1, flags);
				}

				throw new InvalidOperationException();
			}

			public override int ConvertToParentIndex(int index, IBuildContext context)
			{
				throw new NotImplementedException();
				/*if (index != FieldIndex)
					throw new InvalidOperationException();

				if (_parentIndex != null)
					return _parentIndex.Value;

				if (Parent != null)
				{
					index = Parent.SelectQuery.Select.Add(Sql);
					_parentIndex = Parent.ConvertToParentIndex(index, Parent);
				}
				else
				{
					_parentIndex = index;
				}

				return _parentIndex.Value;*/
			}

			public override SqlInfo[] ConvertToIndex(Expression? expression, int level, ConvertFlags flags)
			{
				throw new NotImplementedException();
				/*switch (flags)
				{
					case ConvertFlags.Field :
						{
							var result = _index ??= new[]
							{
								new SqlInfo(Sql!, SelectQuery, FieldIndex)
							};

							return result;
						}
				}


				throw new InvalidOperationException();*/
			}

			public override IsExpressionResult IsExpression(Expression? expression, int level, RequestFor requestFlag)
			{
				throw new NotImplementedException();
				return requestFlag switch
				{
					RequestFor.Root       => IsExpressionResult.GetResult(Lambda != null && expression == Lambda.Parameters[0]),
					RequestFor.Expression => IsExpressionResult.True,
					_                     => IsExpressionResult.False,
				};
			}

			public override IBuildContext GetContext(Expression? expression, int level, BuildInfo buildInfo)
			{
				throw new NotImplementedException();
			}
		}
	}
}
