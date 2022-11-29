﻿using System.Linq.Expressions;

namespace LinqToDB.Linq.Builder
{
	using LinqToDB.Expressions;

	using static LinqToDB.Reflection.Methods.LinqToDB.Merge;

	internal partial class MergeBuilder
	{
		internal sealed class UsingTarget : MethodCallBuilder
		{
			protected override bool CanBuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
			{
				return methodCall.IsSameGenericMethod(UsingTargetMethodInfo);
			}

			protected override IBuildContext BuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
			{
				var mergeContext = (MergeContext)builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[0]));

				var genericArguments = methodCall.Method.GetGenericArguments();

				var cloningContext      = new CloningContext();
				var clonedTargetContext = cloningContext.CloneContext(mergeContext.TargetContext);

				var targetContextRef = new ContextRefExpression(genericArguments[0], mergeContext.TargetContext, "target");
				var sourceContextRef = new ContextRefExpression(genericArguments[0], clonedTargetContext, "source");

				var source                = new TableLikeQueryContext(targetContextRef, sourceContextRef);
				mergeContext.Sequences    = new IBuildContext[] { mergeContext.Sequence, source };
				mergeContext.Merge.Source = source.Source;

				return mergeContext;
			}
		}
	}
}
