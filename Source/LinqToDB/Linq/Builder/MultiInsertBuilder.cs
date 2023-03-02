﻿using System;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqToDB.Linq.Builder
{
	using SqlQuery;
	using LinqToDB.Expressions;

	using Methods = Reflection.Methods.LinqToDB.MultiInsert;

	sealed class MultiInsertBuilder : MethodCallBuilder
	{
		#region MultiInsertBuilder

		protected override bool CanBuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
			=> methodCall.Method.DeclaringType == typeof(MultiInsertExtensions);

		static readonly Dictionary<MethodInfo, Func<ExpressionBuilder, MethodCallExpression, BuildInfo, IBuildContext>> _methodBuilders = new()
		{
			{ Methods.Begin,       BuildMultiInsert },
			{ Methods.Into,        BuildInto        },
			{ Methods.When,        BuildWhen        },
			{ Methods.Else,        BuildElse        },
			{ Methods.Insert,      BuildInsert      },
			{ Methods.InsertAll,   BuildInsertAll   },
			{ Methods.InsertFirst, BuildInsertFirst },
		};

		protected override IBuildContext BuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			var genericMethod = methodCall.Method.GetGenericMethodDefinition();
			return _methodBuilders.TryGetValue(genericMethod, out var build)
				? build(builder, methodCall, buildInfo)
				: throw new InvalidOperationException("Unknown method " + methodCall.Method.Name);
		}

		static void ExtractSequence(IBuildContext sequence, out TableLikeQueryContext source, out MultiInsertContext multiInsertContext)
		{
			if (sequence is MultiInsertContext ic)
			{
				multiInsertContext = ic;
				source             = multiInsertContext.QuerySource;
			}
			else
			{
				source             = (TableLikeQueryContext)sequence;
				multiInsertContext = new MultiInsertContext(source);
			}
		}


		static IBuildContext BuildMultiInsert(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			// MultiInsert(IQueryable)
			//
			var sourceContext = builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[0]));

			var sourceContextRef = new ContextRefExpression(methodCall.Method.GetGenericArguments()[0], sourceContext);

			var source = new TableLikeQueryContext(sourceContextRef, sourceContextRef);
			return new MultiInsertContext(source);
		}

		static IBuildContext BuildTargetTable(
			ExpressionBuilder builder,
			BuildInfo         buildInfo,
			bool              isConditional,
			Expression        query,
			LambdaExpression? condition,
			Expression        table,
			LambdaExpression  setterLambda)
		{
			var sequence = builder.BuildSequence(new BuildInfo(buildInfo, query));

			ExtractSequence(sequence, out var source, out var multiInsertContext);

			var statement = multiInsertContext.MultiInsertStatement;
			var into      = builder.BuildSequence(new BuildInfo(buildInfo, table, new SelectQuery()));

			var intoTable = SequenceHelper.GetTableContext(into) ?? throw new LinqToDBException($"Cannot get table context from {SqlErrorExpression.PrepareExpression(query)}");

			var when          = condition != null ? new SqlSearchCondition() : null;
			var insert        = new SqlInsertClause
			{
				Into          = intoTable.SqlTable
			};

			statement.Add(when, insert);

			if (condition != null)
			{
				var conditionExpr = source.PrepareSourceBody(condition);
				builder.BuildSearchCondition(source, builder.ConvertExpression(conditionExpr), ProjectFlags.SQL,
					when!.Conditions);
			}

			var setterExpression = source.PrepareSourceBody(setterLambda);
			
			var targetRef        = new ContextRefExpression(setterExpression.Type, into);

			var setterExpressions = new List<UpdateBuilder.SetExpressionEnvelope>();
			UpdateBuilder.ParseSetter(builder, targetRef, setterExpression, setterExpressions);
			UpdateBuilder.InitializeSetExpressions(builder, into, source, setterExpressions, insert.Items, false);

			return multiInsertContext;
		}

		static IBuildContext BuildInto(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			// Into(IQueryable, ITable, Expression setter)
			return BuildTargetTable(
				builder,
				buildInfo,
				false,
				methodCall.Arguments[0],
				null,
				methodCall.Arguments[1],
				methodCall.Arguments[2].UnwrapLambda());
		}

		static IBuildContext BuildWhen(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			// When(IQueryable, Expression condition, ITable, Expression setter)
			return BuildTargetTable(
				builder,
				buildInfo,
				true,
				methodCall.Arguments[0],
				methodCall.Arguments[1].UnwrapLambda(),
				methodCall.Arguments[2],
				methodCall.Arguments[3].UnwrapLambda());
		}

		static IBuildContext BuildElse(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			// Else(IQueryable, ITable, Expression setter)
			return BuildTargetTable(
				builder,
				buildInfo,
				true,
				methodCall.Arguments[0],
				null,
				methodCall.Arguments[1],
				methodCall.Arguments[2].UnwrapLambda());
		}

		static IBuildContext BuildInsert(ExpressionBuilder builder, BuildInfo buildInfo, MultiInsertType type, Expression query)
		{
			var sequence = builder.BuildSequence(new BuildInfo(buildInfo, query));
			ExtractSequence(sequence, out _, out var multiInsertContext);

			var statement = multiInsertContext.MultiInsertStatement;
			statement.InsertType = type;

			return multiInsertContext;
		}

		static IBuildContext BuildInsert(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			// Insert(IQueryable)
			return BuildInsert(
				builder,
				buildInfo,
				MultiInsertType.Unconditional,
				methodCall.Arguments[0]);
		}

		static IBuildContext BuildInsertAll(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			// InsertAll(IQueryable)
			return BuildInsert(
				builder,
				buildInfo,
				MultiInsertType.All,
				methodCall.Arguments[0]);
		}

		static IBuildContext BuildInsertFirst(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			// InsertFirst(IQueryable)
			return BuildInsert(
				builder,
				buildInfo,
				MultiInsertType.First,
				methodCall.Arguments[0]);
		}

		#endregion

		#region MultiInsertContext

		sealed class MultiInsertContext : BuildContextBase
		{
			public MultiInsertContext(TableLikeQueryContext source)
				: base(source.Builder, source.ElementType, source.SelectQuery)
			{
				MultiInsertStatement = new SqlMultiInsertStatement(source.Source);
				QuerySource          = source;
			}

			public TableLikeQueryContext   QuerySource          { get; }
			public SqlMultiInsertStatement MultiInsertStatement { get; }

			public override Expression MakeExpression(Expression path, ProjectFlags flags)
			{
				return path;
			}

			public override IBuildContext Clone(CloningContext context)
			{
				throw new NotImplementedException();
			}

			public override void SetRunQuery<T>(Query<T> query, Expression expr)
			{
				QueryRunner.SetNonQueryQuery(query);
			}

			public override SqlStatement GetResultStatement()
			{
				return MultiInsertStatement;
			}
		}

		#endregion
	}
}
