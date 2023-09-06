﻿using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace LinqToDB.Expressions
{
	public class ExpressionVisitorBase : ExpressionVisitor
	{
		[DebuggerStepThrough]
		[return: NotNullIfNotNull(nameof(node))]
		public override Expression? Visit(Expression? node)
		{
			return base.Visit(node);
		}

		public virtual Expression VisitSqlPlaceholderExpression(SqlPlaceholderExpression node)
		{
			return node;
		}

		public virtual Expression VisitChangeTypeExpression(ChangeTypeExpression node)
		{
			return node.Update(Visit(node.Expression)!);
		}

		internal virtual Expression VisitContextRefExpression(ContextRefExpression node)
		{
			return node;
		}

		internal virtual Expression VisitConvertFromDataReaderExpression(ConvertFromDataReaderExpression node)
		{
			return node;
		}

		public virtual Expression VisitDefaultValueExpression(DefaultValueExpression node)
		{
			return node;
		}

		internal virtual Expression VisitSqlAdjustTypeExpression(SqlAdjustTypeExpression node)
		{
			return node.Update(Visit(node.Expression)!);
		}

		internal virtual Expression VisitSqlEagerLoadExpression(SqlEagerLoadExpression node)
		{
			return node.Update(Visit(node.SequenceExpression), Visit(node.Predicate));
		}

		internal virtual Expression VisitSqlErrorExpression(SqlErrorExpression node)
		{
			return node;
		}

		internal virtual SqlGenericConstructorExpression.Assignment VisitSqlGenericAssignment(
			SqlGenericConstructorExpression.Assignment assignment)
		{
			return assignment.WithExpression(Visit(assignment.Expression));
		}

		internal virtual SqlGenericConstructorExpression.Parameter VisitSqlGenericParameter(
			SqlGenericConstructorExpression.Parameter parameter)
		{
			return parameter.WithExpression(Visit(parameter.Expression));
		}

		internal virtual Expression VisitSqlGenericConstructorExpression(SqlGenericConstructorExpression node)
		{
			var assignments = Visit(node.Assignments, VisitSqlGenericAssignment);

			if (!ReferenceEquals(assignments, node.Assignments))
			{
				node = node.ReplaceAssignments(assignments.ToList());
			}

			var parameters = Visit(node.Parameters, VisitSqlGenericParameter);

			if (!ReferenceEquals(parameters, node.Parameters))
			{
				node = node.ReplaceParameters(parameters.ToList());
			}

			return node;
		}

		internal virtual Expression VisitSqlGenericParamAccessExpression(SqlGenericParamAccessExpression node)
		{
			return node;
		}

		internal virtual Expression VisitSqlReaderIsNullExpression(SqlReaderIsNullExpression node)
		{
			return node.Update((SqlPlaceholderExpression)Visit(node.Placeholder)!);
		}

		internal virtual Expression VisitSqlPathExpression(SqlPathExpression node)
		{
			return node;
		}

		public virtual void Cleanup()
		{
		}

		public virtual Expression VisitClosurePlaceholderExpression(ClosurePlaceholderExpression node)
		{
			return node.Update(Visit(node.ClosureExpression));
		}
	}
}
