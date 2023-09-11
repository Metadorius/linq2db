﻿using System;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqToDB.Linq.Builder
{
	using LinqToDB.Expressions;
	using SqlQuery;

	using static LinqToDB.Reflection.Methods.LinqToDB.Merge;

	internal partial class MergeBuilder
	{
		internal sealed class MergeInto : MethodCallBuilder
		{
			static readonly MethodInfo[] _supportedMethods = {MergeIntoMethodInfo1, MergeIntoMethodInfo2};

			protected override bool CanBuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
			{
				return methodCall.IsSameGenericMethod(_supportedMethods);
			}

			protected override IBuildContext BuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
			{
				// MergeInto<TTarget, TSource>(IQueryable<TSource> source, ITable<TTarget> target, string hint)
				var sourceContext = builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[0], new SelectQuery()));
				var target        = builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[1]) { AssociationsAsSubQueries = true });

				var targetTable = GetTargetTable(target);
				if (targetTable == null)
					throw new NotImplementedException("Currently, only CTEs are supported as the target of a merge. You can fix by calling .AsCte() on the parameter before passing into .MergeInto().");

				var merge = new SqlMergeStatement(targetTable);
				if (methodCall.Arguments.Count == 3)
					merge.Hint = methodCall.Arguments[2].EvaluateExpression<string>(builder.DataContext);

				target.SetAlias(merge.Target.Alias!);
				target.Statement = merge;

				var genericArguments = methodCall.Method.GetGenericArguments();

				var source = new TableLikeQueryContext(new ContextRefExpression(genericArguments[0], target, "t"),
					new ContextRefExpression(genericArguments[1], sourceContext, "s"));

				return new MergeContext(merge, target, source);
			}

		}
	}
}
