﻿using System.Reflection;
using System.Linq.Expressions;

namespace LinqToDB.Linq.Builder
{
	using Reflection;
	using LinqToDB.Expressions;

	sealed class DistinctBuilder : MethodCallBuilder
	{
		static readonly MethodInfo[] _supportedMethods = { Methods.Queryable.Distinct, Methods.Enumerable.Distinct, Methods.LinqToDB.SelectDistinct };

		protected override bool CanBuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			return methodCall.IsSameGenericMethod(_supportedMethods);
		}

		protected override IBuildContext BuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			var sequence = builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[0]));

			var sql      = sequence.SelectQuery;
			if (sql.Select.TakeValue != null || sql.Select.SkipValue != null)
			{
				sequence = new SubQueryContext(sequence);
			}

			var subQueryContext = new SubQueryContext(sequence);

			subQueryContext.SelectQuery.Select.IsDistinct = true;

			var outerSubqueryContext = new SubQueryContext(subQueryContext);

			// We do not need all fields for SelectDistinct
			//
			if (methodCall.IsSameGenericMethod(Methods.LinqToDB.SelectDistinct))
			{
				subQueryContext.SelectQuery.Select.OptimizeDistinct = true;
			}
			else
			{
				// create all columns
				var sqlExpr = builder.BuildSqlExpression(outerSubqueryContext, new ContextRefExpression(methodCall.Method.GetGenericArguments()[0], subQueryContext), buildInfo.GetFlags());
				sqlExpr = builder.UpdateNesting(outerSubqueryContext, sqlExpr);
			}

			return new DistinctContext(outerSubqueryContext);
		}

		protected override SequenceConvertInfo? Convert(
			ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo, ParameterExpression? param)
		{
			return null;
		}

		class DistinctContext : PassThroughContext
		{
			public DistinctContext(IBuildContext context) : base(context)
			{
			}

			public override Expression MakeExpression(Expression path, ProjectFlags flags)
			{
				if (SequenceHelper.IsSameContext(path, this) && (flags.IsRoot() || flags.IsAssociationRoot()))
					return path;

				var corrected = SequenceHelper.CorrectExpression(path, this, Context);

				if (flags.IsTable() || flags.IsTraverse() || flags.IsSubquery())
					return corrected;

				Expression result;
				if (flags.IsExpand())
				{
					result = Builder.MakeExpression(Context, corrected, flags);
				}
				else
				{
					result = Builder.BuildSqlExpression(Context, corrected, flags.SqlFlag());
					result = Builder.UpdateNesting(Context, result);
				}

				return result;
			}

			public override IBuildContext Clone(CloningContext context)
			{
				return new DistinctContext(context.CloneContext(Context));
			}
		}
	}
}
