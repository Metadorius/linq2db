﻿using System.Diagnostics;
using System.Linq.Expressions;

namespace LinqToDB.Linq.Builder
{
	using SqlQuery;

	[DebuggerDisplay("{BuildContextDebuggingHelper.GetContextInfo(this)}")]
	abstract class SequenceContextBase : IBuildContext
	{
		protected SequenceContextBase(IBuildContext? parent, IBuildContext[] sequences, LambdaExpression? lambda)
		{
			Parent          = parent;
			Sequences       = sequences;
			Builder         = sequences[0].Builder;
			Lambda          = lambda;
			Body            = lambda == null ? null : SequenceHelper.PrepareBody(lambda, sequences);
			SelectQuery     = sequences[0].SelectQuery;
			Sequence.Parent = this;

			Builder.Contexts.Add(this);
#if DEBUG
			ContextId = Builder.GenerateContextId();
#endif
		}

		protected SequenceContextBase(IBuildContext? parent, IBuildContext sequence, LambdaExpression? lambda)
			: this(parent, new[] { sequence }, lambda)
		{
		}

#if DEBUG
		public string SqlQueryText  => SelectQuery?.SqlText ?? "";
		public string Path          => this.GetPath();
		public int    ContextId     { get; }
#endif

		public IBuildContext?    Parent      { get; set; }
		public IBuildContext[]   Sequences   { get; set; }
		public ExpressionBuilder Builder     { get; set; }
		public LambdaExpression? Lambda      { get; set; }
		public Expression?       Body        { get; set; }
		public SelectQuery       SelectQuery { get; set; }
		public SqlStatement?     Statement   { get; set; }
		public IBuildContext     Sequence    => Sequences[0];

		Expression? IBuildContext.Expression => Lambda;

		public virtual void BuildQuery<T>(Query<T> query, ParameterExpression queryParameter)
		{
			throw new NotImplementedException();

			/*var expr = Builder.FinalizeProjection(this,
				Builder.MakeExpression(new ContextRefExpression(typeof(T), this), ProjectFlags.Expression));

			var mapper = Builder.BuildMapper<T>(expr);

			QueryRunner.SetRunQuery(query, mapper);*/
		}

		public abstract Expression         BuildExpression(Expression? expression, int level, bool enforceServerSide);
		public abstract SqlInfo[]          ConvertToSql   (Expression? expression, int level, ConvertFlags flags);
		public abstract SqlInfo[]          ConvertToIndex (Expression? expression, int level, ConvertFlags flags);

		public virtual Expression MakeExpression(Expression path, ProjectFlags flags)
		{
			path = SequenceHelper.CorrectExpression(path, this, Sequence);
			return Builder.MakeExpression(path, flags);
		}

		public abstract IBuildContext Clone(CloningContext    context);

		public virtual void SetRunQuery<T>(Query<T> query, Expression expr)
		{
			var mapper = Builder.BuildMapper<T>(expr);

			QueryRunner.SetRunQuery(query, mapper);
		}

		public abstract IsExpressionResult IsExpression   (Expression? expression, int level, RequestFor requestFlag);
		public abstract IBuildContext?     GetContext     (Expression? expression, int level, BuildInfo buildInfo);

		public virtual SqlStatement GetResultStatement()
		{
			return Sequence.GetResultStatement();
		}

		public void CompleteColumns()
		{
			foreach (var sequence in Sequences)
			{
				sequence.CompleteColumns();
			}
		}

		public virtual int ConvertToParentIndex(int index, IBuildContext context)
		{
			return Parent?.ConvertToParentIndex(index, context) ?? index;
		}

		public virtual void SetAlias(string? alias)
		{
			if (SelectQuery.Select.Columns.Count == 1)
			{
				SelectQuery.Select.Columns[0].Alias = alias;
			}

			if (SelectQuery.From.Tables.Count > 0)
				SelectQuery.From.Tables[SelectQuery.From.Tables.Count - 1].Alias = alias;
		}

		public virtual ISqlExpression? GetSubQuery(IBuildContext context)
		{
			return null;
		}

	}
}
