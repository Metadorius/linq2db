﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace LinqToDB.SqlQuery
{
	public class SqlMergeStatement : SqlStatementWithQueryBase
	{
		private const string TargetAlias = "Target";

		public SqlMergeStatement(SqlTable target) : base(null)
		{
			Target = new SqlTableSource(target, TargetAlias);
		}

		internal SqlMergeStatement(
			SqlWithClause?                       with,
			string?                              hint,
			SqlTableSource                       target,
			SqlTableLikeSource                   source,
			SqlSearchCondition                   on,
			IEnumerable<SqlMergeOperationClause> operations)
			: base(null)
		{
			With = with;
			Hint = hint;
			Target = target;
			Source = source;
			On = on;

			foreach (var operation in operations)
				Operations.Add(operation);
		}

		public string?                       Hint       { get; internal set; }
		public SqlTableSource                Target     { get; private  set; }
		public SqlTableLikeSource            Source     { get; internal set; }  = null!;
		public SqlSearchCondition            On         { get; private  set; }  = new();
		public List<SqlMergeOperationClause> Operations { get; private  set; } = new();
		public SqlOutputClause?              Output     { get; set; }

		public bool                          HasIdentityInsert => Operations.Any(o => o.OperationType == MergeOperationType.Insert && o.Items.Any(item => item.Column is SqlField field && field.IsIdentity));
		public override QueryType            QueryType         => QueryType.Merge;
		public override QueryElementType     ElementType       => QueryElementType.MergeStatement;

		public void Modify(SqlTableSource target,     SqlTableLikeSource source, SqlSearchCondition on,
			List<SqlMergeOperationClause> operations, SqlOutputClause?   output)
		{
			Target     = target;
			Source     = source;
			On         = on;
			Operations = operations;
			Output     = output;
		}

		public override QueryElementTextWriter ToString(QueryElementTextWriter writer)
		{
			writer
				.AppendElement(With)
				.Append("MERGE INTO ")
				.AppendElement(Target)
				.AppendLine()
				.Append("USING (")
				.AppendElement(Source)
				.AppendLine(")")
				.Append("ON ")
				.AppendElement(On)
				.AppendLine();

			foreach (var operation in Operations)
			{
				writer
					.AppendElement(operation)
					.AppendLine();
			}

            if (Output?.HasOutput == true)
                writer.AppendElement(Output);
			return writer;
		}

		public override ISqlExpression? Walk<TContext>(WalkOptions options, TContext context, Func<TContext, ISqlExpression, ISqlExpression> func)
		{
			Target.Walk(options, context, func);
			Source.Walk(options, context, func);

			((ISqlExpressionWalkable)On).Walk(options, context, func);

			for (var i = 0; i < Operations.Count; i++)
				((ISqlExpressionWalkable)Operations[i]).Walk(options, context, func);

			With = With?.Walk(options, context, func) as SqlWithClause;

			return base.Walk(options, context, func);
		}

		public override bool IsParameterDependent
		{
			get => Source.IsParameterDependent;
			set => Source.IsParameterDependent = value;
		}

		[NotNull]
		public override SelectQuery? SelectQuery
		{
			get => base.SelectQuery;
			set => throw new InvalidOperationException();
		}

		public override ISqlTableSource? GetTableSource(ISqlTableSource table)
		{
			if (Target.Source == table)
				return Target;

			if (Source == table)
				return Source;

			return null;
		}
	}
}
