﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace LinqToDB.Linq.Builder
{
	using Extensions;
	using Mapping;
	using LinqToDB.Expressions;
	using SqlQuery;

	internal static class SequenceHelper
	{
		public static Expression PrepareBody(LambdaExpression lambda, params IBuildContext[] sequences)
		{
			var body = lambda.Parameters.Count == 0
				? lambda.Body
				: lambda.GetBody(sequences
					.Select((s, idx) =>
					{
						var parameter = lambda.Parameters[idx];
						return (Expression)new ContextRefExpression(parameter.Type, s, parameter.Name);
					}).ToArray());

			if (!ReferenceEquals(body, lambda.Body))
			{
				body = body.Transform(e =>
				{
					if (e.NodeType == ExpressionType.Convert &&
					    ((UnaryExpression)e).Operand is ContextRefExpression contextRef && !e.Type.ToUnderlying().IsValueType)
					{
						return contextRef.WithType(e.Type);
					}
					return e;
				});
			}

			return body;
		}

		public static Expression ReplaceBody(Expression body, ParameterExpression parameter, IBuildContext sequence)
		{
			var contextRef = new ContextRefExpression(parameter.Type, sequence, parameter.Name);
			body = body.Replace(parameter, contextRef);
			return body;
		}

		public static bool IsSameContext(Expression? expression, IBuildContext context)
		{
			return expression == null ||
			       (expression is ContextRefExpression contextRef && contextRef.BuildContext == context);
		}

		[return: NotNullIfNotNull(nameof(expression))]
		public static Expression? CorrectExpression(Expression? expression, IBuildContext current,
			IBuildContext                                       underlying)
		{
			if (expression != null)
			{
				return ReplaceContext(expression, current, underlying);
			}

			return expression;
		}

		[return: NotNullIfNotNull(nameof(expression))]
		public static Expression? CorrectTrackingPath(Expression? expression, IBuildContext toContext)
		{
			if (expression == null || expression.Find(1, (_, e) => e is SqlPlaceholderExpression) == null)
				return expression;

			var contextRef = new ContextRefExpression(toContext.ElementType, toContext);

			var transformed = CorrectTrackingPath(expression, contextRef);

			return transformed;
		}

		public static Expression CorrectTrackingPath(Expression expression, Expression toPath)
		{
			return CorrectTrackingPath(expression, null, toPath);
		}

		public static Expression CorrectTrackingPath(Expression expression, IBuildContext from, IBuildContext to)
		{
			var result = expression.Transform((from, to), (ctx, e) =>
			{
				if (e is SqlPlaceholderExpression placeholder && placeholder.TrackingPath != null)
				{
					return placeholder.WithTrackingPath(ReplaceContext(placeholder.TrackingPath, ctx.from, ctx.to));
				}

				return e;
			});

			return result;
		}

		public static Expression EnsureType(Expression expr, Type type)
		{
			if (expr.Type != type)
			{
				expr = expr.UnwrapConvert();
				if (expr.Type != type)
				{
					if (expr is ContextRefExpression refExpression)
						return refExpression.WithType(type);
					return Expression.Convert(expr, type);
				}

				return expr;
			}

			return expr;
		}

		[return: NotNullIfNotNull(nameof(expression))]
		public static Expression? CorrectTrackingPath(Expression? expression, Expression? except, Expression toPath)
		{
			if (expression == null)
				return null;

			if (toPath is not ContextRefExpression && toPath is not MemberExpression)
				return expression;

			if (expression is SqlGenericConstructorExpression generic)
			{
				List<SqlGenericConstructorExpression.Assignment>? assignments = null;
				List<SqlGenericConstructorExpression.Parameter>?  parameters  = null;

				var contextRef = toPath as ContextRefExpression;

				for (int i = 0; i < generic.Assignments.Count; i++)
				{
					var assignment = generic.Assignments[i];

					var currentPath = toPath;

					var applicable = true;
					if (assignment.MemberInfo.DeclaringType != null)
					{
						applicable = assignment.MemberInfo.DeclaringType.IsAssignableFrom(currentPath.Type);
						if (applicable)
							currentPath = EnsureType(currentPath, assignment.MemberInfo.DeclaringType);
					}

					if (!applicable)
					{
						assignments?.Add(assignment);
						continue;
					}


					var memberTrackingPath = Expression.MakeMemberAccess(currentPath, assignment.MemberInfo);
					var newExpression = CorrectTrackingPath(assignment.Expression, memberTrackingPath);

					if (!ReferenceEquals(assignment.Expression, newExpression))
					{
						if (assignments == null)
						{
							assignments = new();
							for (int j = 0; j < i; j++)
							{
								assignments.Add(generic.Assignments[j]);
							}
						}

						assignments.Add(assignment.WithExpression(newExpression));
					}
					else
						assignments?.Add(assignment);
				}

				if (assignments != null)
				{
					generic = generic.ReplaceAssignments(assignments);
				}

				for (var i = 0; i < generic.Parameters.Count; i++)
				{
					var parameter     = generic.Parameters[i];
					var currentPath   = toPath;
					var newExpression = parameter.Expression;

					if (parameter.MemberInfo != null)
					{
						var memberTrackingPath = Expression.MakeMemberAccess(currentPath, parameter.MemberInfo);
						newExpression = CorrectTrackingPath(parameter.Expression, memberTrackingPath);
					}

					if (!ReferenceEquals(parameter.Expression, newExpression))
					{
						if (parameters == null)
						{
							parameters = new();
							for (int j = 0; j < i; j++)
							{
								parameters.Add(generic.Parameters[j]);
							}
						}

						parameters.Add(parameter.WithExpression(newExpression));
					}
					else
						parameters?.Add(parameter.WithExpression(newExpression));

				}

				if (parameters != null)
				{
					generic = generic.ReplaceParameters(parameters);
				}


				return generic;
			}

			if (expression is NewExpression or MemberInitExpression)
			{
				var parsed = SqlGenericConstructorExpression.Parse(expression);
				if (!ReferenceEquals(parsed, expression))
					return CorrectTrackingPath(parsed, toPath);
			}

			if (expression is SqlPlaceholderExpression placeholder)
			{
				if (placeholder.TrackingPath != null && !placeholder.Type.IsAssignableFrom(toPath.Type) && !placeholder.Type.IsValueType)
				{
					if (IsSpecialProperty(placeholder.TrackingPath, out var propType, out var propName))
					{
						toPath = CreateSpecialProperty(toPath, propType, propName);
						if (placeholder.Type != toPath.Type)
						{
							toPath = Expression.Convert(toPath, placeholder.Type);
						}

						return placeholder.WithTrackingPath(toPath);
					}
				}

				if (placeholder.TrackingPath is MemberExpression me && me.Member.DeclaringType != null && me.Member.DeclaringType.IsAssignableFrom(toPath.Type))
				{
					var toPathConverted = EnsureType(toPath, me.Member.DeclaringType);
					var newExpr         = (Expression)Expression.MakeMemberAccess(toPathConverted, me.Member);

					return placeholder.WithTrackingPath(newExpr);
				}

				/*
				if (placeholder.Path is MemberExpression me && me.Member.DeclaringType != null)
				{
					var toPathConverted = EnsureType(toPath, me.Member.DeclaringType);
					var newExpr         = (Expression)Expression.MakeMemberAccess(toPathConverted, me.Member);

					return placeholder.WithTrackingPath(newExpr);
				}
				*/

				/*if (placeholder.TrackingPath != null && !placeholder.Type.IsAssignableFrom(toPath.Type) &&
				    !toPath.Type.IsValueType)
				{
					if (placeholder.TrackingPath is MemberExpression memberExpression)
					{
						toPath = Expression.MakeMemberAccess(toPath, memberExpression.Member);
						return placeholder.WithTrackingPath(toPath);
					}
				}*/

				return placeholder.WithTrackingPath(toPath);
			}

			/*
			if (expression == except)
				return expression;

			var transformed = expression.Transform(toPath, (t, e) => CorrectTrackingPath(e, e, t));
			return transformed;
			*/

			if (expression is ConstantExpression)
				return expression;

			if (expression is ConditionalExpression conditional)
			{
				return conditional.Update(
					CorrectTrackingPath(conditional.Test, toPath),
					CorrectTrackingPath(conditional.IfTrue, toPath),
					CorrectTrackingPath(conditional.IfFalse, toPath));
			}

			if (expression is BinaryExpression binary)
			{
				return binary.Update(CorrectTrackingPath(binary.Left, toPath), binary.Conversion, CorrectTrackingPath(binary.Right, toPath));
			}

			if (expression is UnaryExpression unary)
			{
				return unary.Update(CorrectTrackingPath(unary.Operand, toPath));
			}

			/*
			if (expression is MemberExpression eme && eme.Expression is ContextRefExpression && toPath is MemberExpression && expression.Type == toPath.Type)
				return toPath;

			if (expression is ContextRefExpression && toPath is ContextRefExpression && expression.Type == toPath.Type)
				return toPath;
				*/

			return expression;
		}

		public static Expression ReplacePlaceholdersPathByTrackingPath(Expression expression)
		{
			var transformed = expression.Transform(e =>
			{
				if (e is SqlPlaceholderExpression { TrackingPath: { } } placeholder)
				{
					return placeholder.WithPath(placeholder.TrackingPath);
				}

				return e;
			});

			return transformed;
		}

		[return: NotNullIfNotNull(nameof(expression))]	
		public static Expression? RemapToNewPathSimple(Expression? expression, Expression toPath, ProjectFlags flags)
		{
			if (expression == null)
				return null;

			if (toPath is not ContextRefExpression && toPath is not MemberExpression && toPath is not SqlGenericParamAccessExpression)
				return expression;

			if (expression is SqlGenericConstructorExpression generic)
			{
				List<SqlGenericConstructorExpression.Assignment>? assignments = null;
				List<SqlGenericConstructorExpression.Parameter>?  parameters  = null;

				var contextRef = toPath as ContextRefExpression;

				for (int i = 0; i < generic.Assignments.Count; i++)
				{
					var assignment = generic.Assignments[i];

					var currentPath = toPath;
					if (contextRef != null && assignment.MemberInfo.DeclaringType != null && !assignment.MemberInfo.DeclaringType.IsAssignableFrom(contextRef.Type))
					{
						currentPath = contextRef.WithType(assignment.MemberInfo.DeclaringType);
					}

					var memberPath = Expression.MakeMemberAccess(currentPath, assignment.MemberInfo);
					var parsed     = SqlGenericConstructorExpression.Parse(assignment.Expression);

					var newExpression = ReferenceEquals(parsed, assignment.Expression)
						? memberPath
						: RemapToNewPathSimple(assignment.Expression, memberPath, flags);

					if (!ReferenceEquals(assignment.Expression, newExpression))
					{
						if (assignments == null)
						{
							assignments = new();
							for (int j = 0; j < i; j++)
							{
								assignments.Add(generic.Assignments[j]);
							}
						}

						assignments.Add(assignment.WithExpression(newExpression));
					}
					else
						assignments?.Add(assignment);
				}

				for (int i = 0; i < generic.Parameters.Count; i++)
				{
					var parameter = generic.Parameters[i];

					var currentPath = toPath;
					if (contextRef != null && parameter.MemberInfo?.DeclaringType != null && !parameter.MemberInfo.DeclaringType.IsAssignableFrom(contextRef.Type))
					{
						currentPath = contextRef.WithType(parameter.MemberInfo.DeclaringType);
					}

					Expression newExpression;

					if (parameter.MemberInfo != null)
					{
						newExpression = Expression.MakeMemberAccess(currentPath, parameter.MemberInfo);
					}
					else
					{
						var paramAccess = new SqlGenericParamAccessExpression(currentPath, parameter.ParameterInfo);

						newExpression = paramAccess;
					}

					if (!ReferenceEquals(parameter.Expression, newExpression))
					{
						if (parameters == null)
						{
							parameters = new();
							for (int j = 0; j < i; j++)
							{
								parameters.Add(generic.Parameters[j]);
							}
						}

						parameters.Add(parameter.WithExpression(newExpression));
					}
					else
						parameters?.Add(parameter);
				}

				if (assignments != null)
				{
					generic = generic.ReplaceAssignments(assignments);
				}

				if (parameters != null)
				{
					generic = generic.ReplaceParameters(parameters);
				}

				generic = generic.WithConstructionRoot(toPath);

				return generic;
			}

			if (expression is NewExpression or MemberInitExpression)
			{
				var parsed = SqlGenericConstructorExpression.Parse(expression);
				if (!ReferenceEquals(parsed, expression))
					return RemapToNewPathSimple(parsed, toPath, flags);
			}

			/*
			if (expression is MemberExpression && toPath is MemberExpression && expression.Type == toPath.Type)
				return toPath;

			if (expression is ContextRefExpression && toPath is ContextRefExpression && expression.Type == toPath.Type)
				return toPath;
				*/

			return expression;
		}


		[return: NotNullIfNotNull(nameof(expression))]	
		public static Expression? RemapToNewPath(Expression? expression, Expression toPath, ProjectFlags flags)
		{
			if (expression == null)
				return null;

			if (toPath is not ContextRefExpression && toPath is not MemberExpression && toPath is not SqlGenericParamAccessExpression)
				return expression;

			if (expression is SqlGenericConstructorExpression generic)
			{
				List<SqlGenericConstructorExpression.Assignment>? assignments = null;
				List<SqlGenericConstructorExpression.Parameter>?  parameters  = null;

				var contextRef = toPath as ContextRefExpression;

				for (int i = 0; i < generic.Assignments.Count; i++)
				{
					var assignment = generic.Assignments[i];

					var currentPath = toPath;
					if (contextRef != null && assignment.MemberInfo.DeclaringType != null && !assignment.MemberInfo.DeclaringType.IsAssignableFrom(contextRef.Type))
					{
						currentPath = contextRef.WithType(assignment.MemberInfo.DeclaringType);
					}

					var newExpression = RemapToNewPath(
						assignment.Expression,
						Expression.MakeMemberAccess(currentPath, assignment.MemberInfo), flags
					);

					if (!ReferenceEquals(assignment.Expression, newExpression))
					{
						if (assignments == null)
						{
							assignments = new();
							for (int j = 0; j < i; j++)
							{
								assignments.Add(generic.Assignments[j]);
							}
						}

						assignments.Add(assignment.WithExpression(newExpression));
					}
					else
						assignments?.Add(assignment);
				}

				for (int i = 0; i < generic.Parameters.Count; i++)
				{
					var parameter = generic.Parameters[i];

					var currentPath = toPath;
					if (contextRef != null && parameter.MemberInfo?.DeclaringType != null && !parameter.MemberInfo.DeclaringType.IsAssignableFrom(contextRef.Type))
					{
						currentPath = contextRef.WithType(parameter.MemberInfo.DeclaringType);
					}

					var paramAccess = new SqlGenericParamAccessExpression(currentPath, parameter.ParameterInfo);

					var newExpression = RemapToNewPath(parameter.Expression, paramAccess, flags);

					if (!ReferenceEquals(parameter.Expression, newExpression))
					{
						if (parameters == null)
						{
							parameters = new();
							for (int j = 0; j < i; j++)
							{
								parameters.Add(generic.Parameters[j]);
							}
						}

						parameters.Add(parameter.WithExpression(newExpression));
					}
					else
						parameters?.Add(parameter);
				}

				if (assignments != null)
				{
					generic = generic.ReplaceAssignments(assignments);
				}

				if (parameters != null)
				{
					generic = generic.ReplaceParameters(parameters);
				}

				generic = generic.WithConstructionRoot(toPath);

				return generic;
			}

			if (expression is NewExpression or MemberInitExpression)
			{
				return RemapToNewPath(SqlGenericConstructorExpression.Parse(expression), toPath, flags);
			}

			if (expression is SqlPlaceholderExpression placeholder)
			{
				if (QueryHelper.IsNullValue(placeholder.Sql))
					return Expression.Default(placeholder.Type);

				if (placeholder.Type == toPath.Type)
				{
					return toPath;
				}

				if (IsSpecialProperty(expression, out var propType, out var propName))
				{
					Expression newExpr = CreateSpecialProperty(toPath, propType, propName);
					if (placeholder.Type != newExpr.Type)
					{
						newExpr = Expression.Convert(newExpr, placeholder.Type);
					}

					return newExpr;
				}

				if (placeholder.Path is MemberExpression me && me.Expression?.Type == toPath.Type)
				{
					var newExpr = (Expression)Expression.MakeMemberAccess(toPath, me.Member);
					if (placeholder.Type != newExpr.Type)
					{
						newExpr = Expression.Convert(newExpr, placeholder.Type);
					}

					return newExpr;
				}

				return RemapToNewPath(placeholder.TrackingPath, toPath, flags);
			}

			if (expression is BinaryExpression binary && toPath.Type != binary.Type)
			{
				var left  = binary.Left;
				var right = binary.Right;

				if (left is SqlPlaceholderExpression)
				{
					left = RemapToNewPath(left, toPath, flags);
				}

				if (right is SqlPlaceholderExpression)
				{
					right = RemapToNewPath(right, toPath, flags);
				}

				if (left.Type != right.Type)
				{
					var newLeft  = left.UnwrapConvert();
					var newRight = right.UnwrapConvert();

					if (newLeft.Type != newRight.Type)
					{
						if (!ReferenceEquals(left, newLeft))
							newLeft = Expression.Convert(newLeft, newRight.Type);
						else
							newRight = Expression.Convert(newRight, newLeft.Type);
					}

					left  = newLeft;
					right = newRight;
				}

				return binary.Update(left, binary.Conversion, right);
			}

			if (expression is ConditionalExpression conditional)
			{
				var newTest  = RemapToNewPath(conditional.Test,    toPath, flags);
				var newTrue  = RemapToNewPath(conditional.IfTrue,  toPath, flags);
				var newFalse = RemapToNewPath(conditional.IfFalse, toPath, flags);

				if (newTrue.Type != expression.Type)
					newTrue = Expression.Convert(newTrue, expression.Type);

				if (newFalse.Type != expression.Type)
					newFalse = Expression.Convert(newFalse, expression.Type);

				return conditional.Update(newTest, newTrue, newFalse);
			}

			if (expression.NodeType == ExpressionType.Convert)
			{
				var unary = (UnaryExpression)expression;
				return unary.Update(RemapToNewPath(unary.Operand, toPath, flags));
			}

			if (expression is MethodCallExpression mc)
			{
				return mc.Update(mc.Object, mc.Arguments.Select(a => RemapToNewPath(a, toPath, flags)));
			}

			if (expression is SqlAdjustTypeExpression adjust)
			{
				return adjust.Update(RemapToNewPath(adjust.Expression, toPath, flags));
			}

			if (expression is SqlEagerLoadExpression eager)
			{
				return eager;
			}

			if (expression is DefaultExpression or DefaultValueExpression)
			{
				return expression;
			}

			if (flags.IsExpression())
			{
				if (expression is DefaultValueExpression)
				{
					return expression;
				}
				if (!expression.Type.IsValueType)
				{
					if (expression is DefaultExpression)
					{
						return expression;
					}

					if (expression is ConstantExpression constant && constant.Value == null)
					{
						return expression;
					}
				}
			}

			return toPath;
		}


		public static Expression ReplaceContext(Expression expression, IBuildContext current, IBuildContext onContext)
		{
			var newExpression = expression.Transform((expression, current, onContext), (ctx, e) =>
			{
				if (e.NodeType == ExpressionType.Convert)
				{
					if (((UnaryExpression)e).Operand is ContextRefExpression contextOperand)
					{
						return new TransformInfo(contextOperand.WithType(e.Type), false, true);
					}
				}

				if (e is ContextRefExpression contextRef)
				{
					if (contextRef.BuildContext == ctx.current)
						return new TransformInfo(new ContextRefExpression(contextRef.Type, ctx.onContext, contextRef.Alias));
				}


				if (e is SqlPlaceholderExpression { TrackingPath: { } } placeholder)
				{
					return new TransformInfo(placeholder.WithTrackingPath(ReplaceContext(placeholder.TrackingPath, ctx.current,
						ctx.onContext)));
				}

				return new TransformInfo(e);
			});

			return newExpression;
		}

		public static Expression ReplaceContext(Expression expression, IBuildContext current, Expression onPath)
		{
			var newExpression = expression.Transform((expression, current, onPath), (ctx, e) =>
			{
				if (e.NodeType == ExpressionType.Convert)
				{
					if (((UnaryExpression)e).Operand is ContextRefExpression contextOperand)
					{
						return new TransformInfo(contextOperand.WithType(e.Type), false, true);
					}
				}

				if (e is ContextRefExpression contextRef && contextRef.BuildContext == ctx.current)
				{
					var replacement = ctx.onPath;
					if (replacement.Type != e.Type)
						replacement = Expression.Convert(replacement, e.Type);

					return new TransformInfo(replacement);
				}

				return new TransformInfo(e);
			});

			return newExpression;
		}

		public static Expression CorrectSelectQuery(Expression expression, SelectQuery selectQuery)
		{
			var newExpression = expression.Transform((expression, selectQuery), (ctx, e) =>
			{
				if (e.NodeType == ExpressionType.Extension && e is SqlPlaceholderExpression sqlPlaceholderExpression)
				{
					return sqlPlaceholderExpression.WithSelectQuery(ctx.selectQuery);
				}

				return e;
			});

			return newExpression;
		}

		public static Expression MoveAllToDefaultIfEmptyContext(Expression expression)
		{
			if (expression is ContextRefExpression)
				return expression;

			var newExpression = expression.Transform((expression), (ctx, e) =>
			{
				if (e.NodeType == ExpressionType.Extension)
				{
					if (e is ContextRefExpression contextRef)
					{
						if (contextRef.BuildContext is DefaultIfEmptyBuilder.DefaultIfEmptyContext)
						{
							return e;
						}

						return contextRef.WithContext(new DefaultIfEmptyBuilder.DefaultIfEmptyContext(null, contextRef.BuildContext, null, false));
					}
				}

				return e;
			});

			return newExpression;
		}

		public static Expression MoveAllToScopedContext(Expression expression, IBuildContext upTo)
		{
			if (expression is ContextRefExpression)
				return expression;

			var newExpression = expression.Transform((expression, upTo), (ctx, e) =>
			{
				if (e.NodeType == ExpressionType.Extension)
				{
					if (e is ContextRefExpression contextRef)
					{
						if (contextRef.BuildContext == upTo || contextRef.BuildContext.SelectQuery == upTo.SelectQuery)
						{
							return e;
						}

						// already correctly scoped
						if (contextRef.BuildContext is ScopeContext scopeContext && scopeContext.UpTo == ctx.upTo)
						{
							return e;
						}

						return contextRef.WithContext(new ScopeContext(contextRef.BuildContext, ctx.upTo));
					}
				}

				return e;
			});

			return newExpression;
		}

		public static Expression StampNullability(Expression expression, SelectQuery query)
		{
			var nullability = NullabilityContext.GetContext(query);
			var translated = expression.Transform(e =>
			{
				if (e is SqlPlaceholderExpression placeholder)
				{
					placeholder = placeholder.WithSql(SqlNullabilityExpression.ApplyNullability(placeholder.Sql, nullability));
					return placeholder;
				}

				return e;
			});

			return translated;
		}

		public static ISqlExpression UnwrapNullability(ISqlExpression expression)
		{
			while (expression is SqlNullabilityExpression nullability)
			{
				expression = nullability.SqlExpression;
			}

			return expression;
		}

		public static ISqlTableSource? GetExpressionSource(ISqlExpression expression)
		{
			if (expression is SqlColumn column)
				return column.Parent;

			if (expression is SqlField field)
				return field.Table;

			return null;
		}

		public static Expression MoveToScopedContext(Expression expression, IBuildContext upTo)
		{
			var scoped        = new ScopeContext(upTo, upTo);
			var newExpression = ReplaceContext(expression, upTo, scoped);
			return newExpression;
		}

		public static ITableContext? GetTableOrCteContext(ExpressionBuilder builder, Expression pathExpression)
		{
			var rootContext = builder.MakeExpression(null, pathExpression, ProjectFlags.Table) as ContextRefExpression;

			var tableContext = rootContext?.BuildContext as ITableContext;

			return tableContext;
		}

		public static TableBuilder.TableContext? GetTableContext(IBuildContext context)
		{
			var contextRef = new ContextRefExpression(context.ElementType, context);

			var rootContext =
				context.Builder.MakeExpression(context, contextRef, ProjectFlags.Table) as ContextRefExpression;

			var tableContext = rootContext?.BuildContext as TableBuilder.TableContext;

			return tableContext;
		}

		public static CteTableContext? GetCteContext(IBuildContext context)
		{
			var contextRef = new ContextRefExpression(context.ElementType, context);

			var rootContext =
				context.Builder.MakeExpression(context, contextRef, ProjectFlags.Table) as ContextRefExpression;

			var tableContext = rootContext?.BuildContext as CteTableContext;

			return tableContext;
		}

		public static ITableContext? GetTableOrCteContext(IBuildContext context)
		{
			var contextRef = new ContextRefExpression(context.ElementType, context);

			var rootContext =
				context.Builder.MakeExpression(context, contextRef, ProjectFlags.Table) as ContextRefExpression;

			var tableContext = rootContext?.BuildContext as ITableContext;

			return tableContext;
		}

		/// <summary>
		/// Checks that provider can handle limitation inside subquery. This function is tightly coupled with <see cref="SelectQueryOptimizerVisitor.OptimizeApply"/> 
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public static bool IsSupportedSubqueryForModifier(IBuildContext context)
		{
			if (!context.Builder.DataContext.SqlProviderFlags.IsApplyJoinSupported)
			{
				if (!QueryHelper.IsDependsOnOuterSources(context.SelectQuery))
					return true;

				if (!context.Builder.DataContext.SqlProviderFlags.IsWindowFunctionsSupported)
					return false;

				if (HasModifierWithOuter(context.SelectQuery))
					return false;
			}

			return true;
		}

		public static bool HasModifierWithOuter(SelectQuery selectQuery)
		{
			if (selectQuery.Select.HasModifier && QueryHelper.IsDependsOnOuterSources(selectQuery))
				return true;

			foreach (var source in QueryHelper.EnumerateAccessibleSources(selectQuery))
			{
				if (source is SelectQuery sc && (sc.Select.HasModifier || !sc.GroupBy.IsEmpty) && QueryHelper.IsDependsOnOuterSources(sc))
					return true;
			}

			return false;
		}

		static IBuildContext UnwrapSubqueryContext(IBuildContext context)
		{
			var current = context;
			while (true)
			{
				if (current is SubQueryContext sc)
				{
					current = sc.SubQuery;
				}
				else if (current is PassThroughContext pass)
				{
					current = pass.Context;
				}
				else 
					break;
			}

			return current;
		}

		public static bool IsDefaultIfEmpty(IBuildContext context)
		{
			return UnwrapSubqueryContext(context) is DefaultIfEmptyBuilder.DefaultIfEmptyContext;
		}

		public static LambdaExpression? GetArgumentLambda(MethodCallExpression methodCall, string argumentName)
		{
			var idx = Array.FindIndex(methodCall.Method.GetParameters(), a => a.Name == argumentName);
			if (idx < 0)
				return null;
			return methodCall.Arguments[idx].UnwrapLambda();
		}

		#region Special fields helpers

		public static MemberExpression CreateSpecialProperty(Expression obj, Type type, string name)
		{
			return Expression.MakeMemberAccess(obj, new SpecialPropertyInfo(obj.Type, type, name));
		}

		public static bool IsSpecialProperty(Expression expression, Type type, string propName)
		{
			if (expression.Type != type)
				return false;

			if (expression is not MemberExpression memberExpression)
				return false;

			if (memberExpression.Member is not SpecialPropertyInfo)
				return false;

			if (memberExpression.Member.Name != propName)
				return false;

			return true;
		}

		public static bool IsSpecialProperty(Expression expression, [NotNullWhen(true)] out Type? type, [NotNullWhen(true)] out string? propName)
		{
			type     = null;
			propName = null;

			if (expression is not MemberExpression memberExpression)
				return false;

			if (memberExpression.Member is not SpecialPropertyInfo)
				return false;

			type     = expression.Type;
			propName = memberExpression.Member.Name;

			return true;
		}

		#endregion
	}
}
