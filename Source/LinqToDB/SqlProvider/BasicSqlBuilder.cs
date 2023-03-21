﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Linq;
using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LinqToDB.SqlProvider
{
	using Common;
	using Common.Internal;
	using DataProvider;
	using Extensions;
	using Mapping;
	using SqlQuery;

	public abstract partial class BasicSqlBuilder : ISqlBuilder
	{
		#region Init

		protected BasicSqlBuilder(IDataProvider? provider, MappingSchema mappingSchema, DataOptions dataOptions, ISqlOptimizer sqlOptimizer, SqlProviderFlags sqlProviderFlags)
		{
			DataProvider     = provider;
			MappingSchema    = mappingSchema;
			DataOptions      = dataOptions;
			SqlOptimizer     = sqlOptimizer;
			SqlProviderFlags = sqlProviderFlags;
		}

		protected BasicSqlBuilder(BasicSqlBuilder parentBuilder)
		{
			DataProvider     = parentBuilder.DataProvider;
			MappingSchema    = parentBuilder.MappingSchema;
			DataOptions      = parentBuilder.DataOptions;
			SqlOptimizer     = parentBuilder.SqlOptimizer;
			SqlProviderFlags = parentBuilder.SqlProviderFlags;
			TablePath        = parentBuilder.TablePath;
			QueryName        = parentBuilder.QueryName;
			TableIDs         = parentBuilder.TableIDs ??= new();
		}

		public OptimizationContext OptimizationContext { get; protected set; } = null!;
		public MappingSchema       MappingSchema       { get;                }
		public StringBuilder       StringBuilder       { get; set;           } = null!;
		public SqlProviderFlags    SqlProviderFlags    { get;                }
		public DataOptions         DataOptions         { get;                }

		protected IDataProvider?      DataProvider;
		protected ValueToSqlConverter ValueToSqlConverter => MappingSchema.ValueToSqlConverter;
		protected SqlStatement        Statement = null!;
		protected int                 Indent;
		protected Step                BuildStep;
		protected ISqlOptimizer       SqlOptimizer;
		protected bool                SkipAlias;

		#endregion

		#region Support Flags

		public virtual bool IsNestedJoinSupported           => true;
		public virtual bool IsNestedJoinParenthesisRequired => false;
		/// <summary>
		/// Identifies CTE clause location:
		/// <list type="bullet">
		/// <item><c>CteFirst = true</c> (default): WITH clause goes first in query</item>
		/// <item><c>CteFirst = false</c>: WITH clause goes before SELECT</item>
		/// </list>
		/// </summary>
		public virtual bool CteFirst                        => true;

		/// <summary>
		/// True if it is needed to wrap join condition with ()
		/// </summary>
		/// <example>
		/// <code>
		/// INNER JOIN Table2 t2 ON (t1.Value = t2.Value)
		/// </code>
		/// </example>
		public virtual bool WrapJoinCondition => false;

		protected virtual bool CanSkipRootAliases(SqlStatement statement) => true;

		#endregion

		#region CommandCount

		public virtual int CommandCount(SqlStatement statement)
		{
			return 1;
		}

		#endregion

		#region Formatting
		/// <summary>
		/// Inline comma separator.
		/// Default value: <code>", "</code>
		/// </summary>
		protected virtual string InlineComma => ", ";

		// some providers could define different separator, e.g. DB2 iSeries OleDb provider needs ", " as separator
		/// <summary>
		/// End-of-line comma separator.
		/// Default value: <code>","</code>
		/// </summary>
		protected virtual string Comma => ",";

		/// <summary>
		/// End-of-line open parentheses element.
		/// Default value: <code>"("</code>
		/// </summary>
		protected virtual string OpenParens => "(";

		protected StringBuilder RemoveInlineComma()
		{
			StringBuilder.Length -= InlineComma.Length;
			return StringBuilder;
		}

		#endregion

		#region Helpers

		[return: NotNullIfNotNull(nameof(element))]
		public T? ConvertElement<T>(T? element, NullabilityContext nullability)
			where T : class, IQueryElement
		{
			return (T?)SqlOptimizer.ConvertElement(MappingSchema, DataOptions, element, OptimizationContext, nullability);
		}

		#endregion

		#region BuildSql

		public void BuildSql(int commandNumber, SqlStatement statement, StringBuilder sb, OptimizationContext optimizationContext, int startIndent = 0)
		{
			BuildSql(commandNumber, statement, sb, optimizationContext, startIndent, !Configuration.Sql.GenerateFinalAliases && CanSkipRootAliases(statement));
		}

		protected virtual void BuildSetOperation(SetOperation operation, StringBuilder sb)
		{
			switch (operation)
			{
				case SetOperation.Union       : sb.Append("UNION");         break;
				case SetOperation.UnionAll    : sb.Append("UNION ALL");     break;
				case SetOperation.Except      : sb.Append("EXCEPT");        break;
				case SetOperation.ExceptAll   : sb.Append("EXCEPT ALL");    break;
				case SetOperation.Intersect   : sb.Append("INTERSECT");     break;
				case SetOperation.IntersectAll: sb.Append("INTERSECT ALL"); break;
				default                       : throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
			}
		}

		protected virtual void BuildSql(int commandNumber, SqlStatement statement, StringBuilder sb, OptimizationContext optimizationContext, int indent, bool skipAlias)
		{
			Statement           = statement;
			StringBuilder       = sb;
			OptimizationContext = optimizationContext;
			Indent              = indent;
			SkipAlias           = skipAlias;

			if (commandNumber == 0)
			{
				BuildSql();

				if (Statement.SelectQuery != null && Statement.SelectQuery.HasSetOperators)
				{
					foreach (var union in Statement.SelectQuery.SetOperators)
					{
						AppendIndent();
						BuildSetOperation(union.Operation, sb);
						sb.AppendLine();

						var sqlBuilder = ((BasicSqlBuilder)CreateSqlBuilder());
						sqlBuilder.BuildSql(commandNumber,
							new SqlSelectStatement(union.SelectQuery) { ParentStatement = statement }, sb,
							optimizationContext, indent,
							skipAlias);
					}
				}

				FinalizeBuildQuery(NullabilityContext.GetContext(statement.SelectQuery),  statement);
			}
			else
			{
				BuildCommand(statement, commandNumber);
			}
		}

		protected virtual void BuildCommand(SqlStatement statement, int commandNumber)
		{
		}

		List<Action>? _finalBuilders;

		protected virtual void FinalizeBuildQuery(NullabilityContext nullability, SqlStatement statement)
		{
			if (_finalBuilders != null)
				foreach (var builder in _finalBuilders)
					builder();
		}

		#endregion

		#region Overrides

		protected virtual void BuildSqlBuilder(NullabilityContext nullability, SelectQuery selectQuery, int indent, bool skipAlias)
		{
			SqlOptimizer.ConvertSkipTake(nullability, MappingSchema, DataOptions, selectQuery, OptimizationContext, out var takeExpr, out var skipExpr);

			if (!SqlProviderFlags.GetIsSkipSupportedFlag(takeExpr, skipExpr)
				&& skipExpr != null)
				throw new SqlException("Skip for subqueries is not supported by the '{0}' provider.", Name);

			if (!SqlProviderFlags.IsTakeSupported && takeExpr != null)
				throw new SqlException("Take for subqueries is not supported by the '{0}' provider.", Name);

			var sqlBuilder = (BasicSqlBuilder)CreateSqlBuilder();
			sqlBuilder.BuildSql(0,
				new SqlSelectStatement(selectQuery) { ParentStatement = Statement }, StringBuilder, OptimizationContext, indent, skipAlias);
		}

		protected abstract ISqlBuilder CreateSqlBuilder();

		protected string WithStringBuilderBuildExpression(NullabilityContext nullability, ISqlExpression expr)
		{
			using var sb = Pools.StringBuilder.Allocate();

			var current   = StringBuilder;
			StringBuilder = sb.Value;

			BuildExpression(nullability, expr);

			StringBuilder = current;

			return sb.Value.ToString();
		}

		protected string WithStringBuilderBuildExpression(NullabilityContext nullability, int precedence, ISqlExpression expr)
		{
			using var sb = Pools.StringBuilder.Allocate();

			var current   = StringBuilder;
			StringBuilder = sb.Value;

			BuildExpression(nullability, precedence, expr);

			StringBuilder = current;

			return sb.Value.ToString();
		}

		protected string WithStringBuilder<TContext>(Action<TContext> func, TContext context)
		{
			using var sb = Pools.StringBuilder.Allocate();

			var current   = StringBuilder;
			StringBuilder = sb.Value;

			func(context);

			StringBuilder = current;

			return sb.Value.ToString();
		}

		void WithStringBuilder<TContext>(StringBuilder sb, Action<TContext> func, TContext context)
		{
			var current = StringBuilder;

			StringBuilder = sb;

			func(context);

			StringBuilder = current;
		}

		protected virtual bool ParenthesizeJoin(List<SqlJoinedTable> joins)
		{
			return false;
		}

		protected virtual void BuildSql()
		{
			switch (Statement.QueryType)
			{
				case QueryType.Select        : BuildSelectQuery((SqlSelectStatement)Statement);                                                                 break;
				case QueryType.Delete        : BuildDeleteQuery((SqlDeleteStatement)Statement);                                                                 break;
				case QueryType.Update        : BuildUpdateQuery((SqlUpdateStatement)Statement, Statement.SelectQuery!, ((SqlUpdateStatement)Statement).Update); break;
				case QueryType.Insert        : BuildInsertQuery((SqlInsertStatement)Statement, ((SqlInsertStatement)Statement).Insert, false);                  break;
				case QueryType.InsertOrUpdate: BuildInsertOrUpdateQuery((SqlInsertOrUpdateStatement)Statement);                                                 break;
				case QueryType.CreateTable   : BuildCreateTableStatement((SqlCreateTableStatement)Statement);                                                   break;
				case QueryType.DropTable     : BuildDropTableStatement((SqlDropTableStatement)Statement);                                                       break;
				case QueryType.TruncateTable : BuildTruncateTableStatement((SqlTruncateTableStatement)Statement);                                               break;
				case QueryType.Merge         : BuildMergeStatement((SqlMergeStatement)Statement);                                                               break;
				case QueryType.MultiInsert   : BuildMultiInsertQuery((SqlMultiInsertStatement)Statement);                                                       break;
				default                      : BuildUnknownQuery();                                                                                             break;
			}
		}

		protected virtual void BuildDeleteQuery(SqlDeleteStatement deleteStatement)
		{
			var nullability = new NullabilityContext(deleteStatement.SelectQuery);

			BuildStep = Step.Tag;               BuildTag(deleteStatement);
			BuildStep = Step.WithClause;        BuildWithClause(deleteStatement.With);
			BuildStep = Step.DeleteClause;      BuildDeleteClause(nullability, deleteStatement);
			BuildStep = Step.FromClause;        BuildDeleteFromClause(nullability, deleteStatement);
			BuildStep = Step.AlterDeleteClause; BuildAlterDeleteClause(deleteStatement);
			BuildStep = Step.WhereClause;       BuildWhereClause(nullability, deleteStatement.SelectQuery);
			BuildStep = Step.GroupByClause;     BuildGroupByClause(nullability, deleteStatement.SelectQuery);
			BuildStep = Step.HavingClause;      BuildHavingClause(nullability, deleteStatement.SelectQuery);
			BuildStep = Step.OrderByClause;     BuildOrderByClause(nullability, deleteStatement.SelectQuery);
			BuildStep = Step.OffsetLimit;       BuildOffsetLimit(nullability, deleteStatement.SelectQuery);
			BuildStep = Step.Output;            BuildOutputSubclause(nullability, deleteStatement.GetOutputClause());
			BuildStep = Step.QueryExtensions;   BuildQueryExtensions(nullability, deleteStatement);
		}

		protected void BuildDeleteQuery2(SqlDeleteStatement deleteStatement)
		{
			var nullability = new NullabilityContext(deleteStatement.SelectQuery);

			BuildStep = Step.Tag;          BuildTag(deleteStatement);
			BuildStep = Step.DeleteClause; BuildDeleteClause(nullability, deleteStatement);

			while (StringBuilder[StringBuilder.Length - 1] == ' ')
				StringBuilder.Length--;

			StringBuilder.AppendLine();
			AppendIndent().AppendLine(OpenParens);

			++Indent;

			var selectStatement = new SqlSelectStatement(deleteStatement.SelectQuery)
			{ ParentStatement = deleteStatement, With = deleteStatement.GetWithClause() };

			var sqlBuilder = (BasicSqlBuilder)CreateSqlBuilder();
			sqlBuilder.BuildSql(0, selectStatement, StringBuilder, OptimizationContext, Indent);

			--Indent;

			AppendIndent().AppendLine(")");
		}

		protected virtual void BuildUpdateQuery(SqlStatement statement, SelectQuery selectQuery, SqlUpdateClause updateClause)
		{
			var nullability = new NullabilityContext(selectQuery);

			BuildStep = Step.Tag;          BuildTag(statement);
			BuildStep = Step.WithClause;   BuildWithClause(statement.GetWithClause());
			BuildStep = Step.UpdateClause; BuildUpdateClause(nullability, Statement, selectQuery, updateClause);

			if (SqlProviderFlags.IsUpdateFromSupported)
			{
				BuildStep = Step.FromClause; BuildFromClause(nullability, Statement, selectQuery);
			}

			BuildStep = Step.WhereClause;     BuildUpdateWhereClause(nullability, selectQuery);
			BuildStep = Step.GroupByClause;   BuildGroupByClause(nullability, selectQuery);
			BuildStep = Step.HavingClause;    BuildHavingClause(nullability, selectQuery);
			BuildStep = Step.OrderByClause;   BuildOrderByClause(nullability, selectQuery);
			BuildStep = Step.OffsetLimit;     BuildOffsetLimit(nullability, selectQuery);
			BuildStep = Step.Output;          BuildOutputSubclause(nullability, statement.GetOutputClause());
			BuildStep = Step.QueryExtensions; BuildQueryExtensions(nullability, statement);
		}

		protected virtual void BuildSelectQuery(SqlSelectStatement selectStatement)
		{
			var queryName = QueryName;
			var tablePath = TablePath;

			if (selectStatement.SelectQuery.QueryName is not null && SqlProviderFlags.IsNamingQueryBlockSupported)
			{
				QueryName = selectStatement.SelectQuery.QueryName;
				TablePath = null;
			}

			var nullability = new NullabilityContext(selectStatement.SelectQuery);

			BuildStep = Step.Tag;             BuildTag(selectStatement);
			BuildStep = Step.WithClause;      BuildWithClause(selectStatement.With);
			BuildStep = Step.SelectClause;    BuildSelectClause(nullability, selectStatement.SelectQuery);
			BuildStep = Step.FromClause;      BuildFromClause(nullability, selectStatement, selectStatement.SelectQuery);
			BuildStep = Step.WhereClause;     BuildWhereClause(nullability, selectStatement.SelectQuery);
			BuildStep = Step.GroupByClause;   BuildGroupByClause(nullability, selectStatement.SelectQuery);
			BuildStep = Step.HavingClause;    BuildHavingClause(nullability, selectStatement.SelectQuery);
			BuildStep = Step.OrderByClause;   BuildOrderByClause(nullability, selectStatement.SelectQuery);
			BuildStep = Step.OffsetLimit;     BuildOffsetLimit(nullability, selectStatement.SelectQuery);
			BuildStep = Step.QueryExtensions; BuildQueryExtensions(nullability, selectStatement);

			TablePath = tablePath;
			QueryName = queryName;
		}

		protected virtual void BuildCteBody(SelectQuery selectQuery)
		{
			var sqlBuilder = (BasicSqlBuilder)CreateSqlBuilder();
			sqlBuilder.BuildSql(0, new SqlSelectStatement(selectQuery), StringBuilder, OptimizationContext, Indent, SkipAlias);
		}

		protected virtual void BuildInsertQuery(SqlStatement statement, SqlInsertClause insertClause, bool addAlias)
		{
			if (!CteFirst && statement is SqlStatementWithQueryBase withQuery && withQuery.With?.Clauses.Count > 0)
			{
				BuildInsertQuery2(statement, insertClause, addAlias);
				return;
			}

			var nullability = NullabilityContext.GetContext(statement.SelectQuery);

			BuildStep = Step.Tag;          BuildTag(statement);
			BuildStep = Step.WithClause;   BuildWithClause(statement.GetWithClause());
			BuildStep = Step.InsertClause; BuildInsertClause(nullability, statement, insertClause, addAlias);

			if (statement.QueryType == QueryType.Insert && statement.SelectQuery!.From.Tables.Count != 0)
			{
				BuildStep = Step.SelectClause;    BuildSelectClause(nullability, statement.SelectQuery);
				BuildStep = Step.FromClause;      BuildFromClause(nullability, statement, statement.SelectQuery);
				BuildStep = Step.WhereClause;     BuildWhereClause(nullability, statement.SelectQuery);
				BuildStep = Step.GroupByClause;   BuildGroupByClause(nullability, statement.SelectQuery);
				BuildStep = Step.HavingClause;    BuildHavingClause(nullability, statement.SelectQuery);
				BuildStep = Step.OrderByClause;   BuildOrderByClause(nullability, statement.SelectQuery);
				BuildStep = Step.OffsetLimit;     BuildOffsetLimit(nullability, statement.SelectQuery);
				BuildStep = Step.QueryExtensions; BuildQueryExtensions(nullability, statement);
			}

			if (insertClause.WithIdentity)
				BuildGetIdentity(nullability, insertClause);
			else
			{
				BuildStep = Step.Output;
				BuildOutputSubclause(nullability, statement.GetOutputClause());
			}
		}

		protected void BuildInsertQuery2(SqlStatement statement, SqlInsertClause insertClause, bool addAlias)
		{
			var nullability = NullabilityContext.GetContext(statement.SelectQuery);

			BuildStep = Step.Tag;          BuildTag(statement);
			BuildStep = Step.InsertClause; BuildInsertClause(nullability, statement, insertClause, addAlias);

			BuildStep = Step.WithClause;   BuildWithClause(statement.GetWithClause());

			if (statement.QueryType == QueryType.Insert && statement.SelectQuery!.From.Tables.Count != 0)
			{
				BuildStep = Step.SelectClause;    BuildSelectClause(nullability, statement.SelectQuery);
				BuildStep = Step.FromClause;      BuildFromClause(nullability, statement, statement.SelectQuery);
				BuildStep = Step.WhereClause;     BuildWhereClause(nullability, statement.SelectQuery);
				BuildStep = Step.GroupByClause;   BuildGroupByClause(nullability, statement.SelectQuery);
				BuildStep = Step.HavingClause;    BuildHavingClause(nullability, statement.SelectQuery);
				BuildStep = Step.OrderByClause;   BuildOrderByClause(nullability, statement.SelectQuery);
				BuildStep = Step.OffsetLimit;     BuildOffsetLimit(nullability, statement.SelectQuery);
				BuildStep = Step.QueryExtensions; BuildQueryExtensions(nullability, statement);
			}

			if (insertClause.WithIdentity)
				BuildGetIdentity(nullability, insertClause);
			else
				BuildOutputSubclause(nullability, statement.GetOutputClause());
		}

		protected virtual void BuildMultiInsertQuery(SqlMultiInsertStatement statement)
			=> throw new SqlException("This data provider does not support multi-table insert.");

		protected virtual void BuildUnknownQuery()
		{
			throw new SqlException("Unknown query type '{0}'.", Statement.QueryType);
		}

		// Default implementation. Doesn't generate linked server and package name components.
		public virtual StringBuilder BuildObjectName(StringBuilder sb, SqlObjectName name, ConvertType objectType, bool escape, TableOptions tableOptions)
		{
			if (name.Database != null)
			{
				(escape ? Convert(sb, name.Database, ConvertType.NameToDatabase) : sb.Append(name.Database))
					.Append('.');
				if (name.Schema == null)
					sb.Append('.');
			}

			if (name.Schema != null)
			{
				(escape ? Convert(sb, name.Schema, ConvertType.NameToSchema) : sb.Append(name.Schema))
					.Append('.');
			}

			return escape ? Convert(sb, name.Name, objectType) : sb.Append(name.Name);
		}

		public string ConvertInline(string value, ConvertType convertType)
		{
			using var sb = Pools.StringBuilder.Allocate();
			return Convert(sb.Value, value, convertType).ToString();
		}

		public virtual StringBuilder Convert(StringBuilder sb, string value, ConvertType convertType)
		{
			sb.Append(value);
			return sb;
		}

		#endregion

		#region Build CTE

		protected virtual bool IsRecursiveCteKeywordRequired => false;
		protected virtual bool IsCteColumnListSupported      => true;

		protected virtual void BuildWithClause(SqlWithClause? with)
		{
			if (with == null || with.Clauses.Count == 0)
				return;

			var first = true;

			foreach (var cte in with.Clauses)
			{
				if (first)
				{
					AppendIndent();
					StringBuilder.Append("WITH ");

					if (IsRecursiveCteKeywordRequired && with.Clauses.Any(c => c.IsRecursive))
						StringBuilder.Append("RECURSIVE ");

					first = false;
				}
				else
				{
					StringBuilder.AppendLine(Comma);
					AppendIndent();
				}

				BuildObjectName(StringBuilder, new (cte.Name!), ConvertType.NameToQueryTable, true, TableOptions.NotSet);

				if (IsCteColumnListSupported)
				{
					if (cte.Fields.Count > 3)
					{
						StringBuilder.AppendLine();
						AppendIndent(); StringBuilder.AppendLine(OpenParens);
						++Indent;

						var firstField = true;
						foreach (var field in cte.Fields)
						{
							if (!firstField)
								StringBuilder.AppendLine(Comma);
							firstField = false;
							AppendIndent();
							Convert(StringBuilder, field.PhysicalName, ConvertType.NameToQueryField);
						}

						--Indent;
						StringBuilder.AppendLine();
						AppendIndent(); StringBuilder.AppendLine(")");
					}
					else if (cte.Fields.Count > 0)
					{
						StringBuilder.Append(" (");

						var firstField = true;
						foreach (var field in cte.Fields)
						{
							if (!firstField)
								StringBuilder.Append(InlineComma);
							firstField = false;
							Convert(StringBuilder, field.PhysicalName, ConvertType.NameToQueryField);
						}
						StringBuilder.AppendLine(")");
					}
					else
						StringBuilder.Append(' ');
				}
				else
					StringBuilder.Append(' ');

				AppendIndent();
				StringBuilder.AppendLine("AS");
				AppendIndent();
				StringBuilder.AppendLine(OpenParens);

				Indent++;

				BuildCteBody(cte.Body!);

				Indent--;

				AppendIndent();
				StringBuilder.Append(')');
			}

			StringBuilder.AppendLine();
		}

		#endregion

		#region Build Select

		protected virtual void BuildSelectClause(NullabilityContext nullability, SelectQuery selectQuery)
		{
			AppendIndent();
			StringBuilder.Append("SELECT");

			StartStatementQueryExtensions(selectQuery);

			if (selectQuery.Select.IsDistinct)
				StringBuilder.Append(" DISTINCT");

			BuildSkipFirst(nullability, selectQuery);

			StringBuilder.AppendLine();
			BuildColumns(nullability, selectQuery);
		}

		protected virtual void StartStatementQueryExtensions(SelectQuery? selectQuery)
		{
			if (selectQuery?.QueryName is {} queryName)
				StringBuilder
					.Append(" /* ")
					.Append(queryName)
					.Append(" */")
					;
		}

		protected virtual IEnumerable<SqlColumn> GetSelectedColumns(SelectQuery selectQuery)
		{
			return selectQuery.Select.Columns;
		}

		protected virtual void BuildColumns(NullabilityContext nullability, SelectQuery selectQuery)
		{
			Indent++;

			var first = true;

			foreach (var col in GetSelectedColumns(selectQuery))
			{
				if (!first)
					StringBuilder.AppendLine(Comma);

				first = false;

				var addAlias = true;
				var expr     = ConvertElement(col.Expression, nullability);

				AppendIndent();
				BuildColumnExpression(nullability, selectQuery, expr, col.Alias, ref addAlias);

				if (!SkipAlias && addAlias && !string.IsNullOrEmpty(col.Alias))
				{
					StringBuilder.Append(" as ");
					Convert(StringBuilder, col.Alias!, ConvertType.NameToQueryFieldAlias);
				}
			}

			if (first)
				AppendIndent().Append('*');

			Indent--;

			StringBuilder.AppendLine();
		}

		protected virtual void BuildOutputColumnExpressions(NullabilityContext nullability, IReadOnlyList<ISqlExpression> expressions)
		{
			Indent++;

			var first = true;

			foreach (var expr in expressions)
			{
				if (!first)
					StringBuilder.AppendLine(Comma);

				first = false;

				var addAlias  = true;
				var converted = ConvertElement(expr, nullability);

				AppendIndent();
				BuildColumnExpression(nullability, null, converted, null, ref addAlias);
			}

			Indent--;

			StringBuilder.AppendLine();
		}

		protected virtual bool SupportsBooleanInColumn => false;
		protected virtual bool SupportsNullInColumn    => true;

		protected virtual ISqlExpression WrapBooleanExpression(ISqlExpression expr)
		{
			if (expr.SystemType == typeof(bool))
			{
				SqlSearchCondition? sc = null;
				if (expr is SqlSearchCondition sc1)
				{
					sc = sc1;
				}
				else if (
					expr is SqlExpression ex      &&
					ex.Expr              == "{0}" &&
					ex.Parameters.Length == 1     &&
					ex.Parameters[0] is SqlSearchCondition sc2)
				{
					sc = sc2;
				}

				if (sc != null)
				{
					if (sc.Conditions.Count == 0)
					{
						expr = new SqlValue(true);
					}
					else
					{
						expr = new SqlFunction(typeof(bool), "CASE", expr, new SqlValue(true), new SqlValue(false))
						{
							DoNotOptimize = true
						};
					}
				}
			}

			return expr;
		}

		protected virtual void BuildColumnExpression(NullabilityContext nullability, SelectQuery? selectQuery, ISqlExpression expr, string? alias, ref bool addAlias)
		{
			expr = WrapBooleanExpression(expr);

			expr = WrapColumnExpression(expr);

			BuildExpression(nullability, expr, true, true, alias, ref addAlias, true);
		}

		protected virtual ISqlExpression WrapColumnExpression(ISqlExpression expr)
		{
			if (!SupportsNullInColumn && expr is SqlValue sqlValue && sqlValue.Value == null)
			{
				return new SqlFunction(sqlValue.ValueType.SystemType, "Convert", false, new SqlDataType(sqlValue.ValueType), sqlValue);
			}

			return expr;
		}

		#endregion

		#region Build Delete

		protected virtual void BuildAlterDeleteClause(SqlDeleteStatement deleteStatement)
		{
		}
		
		protected virtual void BuildDeleteClause(NullabilityContext nullability, SqlDeleteStatement deleteStatement)
		{
			AppendIndent();
			StringBuilder.Append("DELETE");
			StartStatementQueryExtensions(deleteStatement.SelectQuery);
			BuildSkipFirst(nullability, deleteStatement.SelectQuery);
			StringBuilder.Append(' ');
		}

		#endregion

		#region Build Update

		protected virtual void BuildUpdateWhereClause(NullabilityContext nullability, SelectQuery selectQuery)
		{
			BuildWhereClause(nullability, selectQuery);
		}

		protected virtual void BuildUpdateClause(NullabilityContext nullability, SqlStatement statement, SelectQuery selectQuery, SqlUpdateClause updateClause)
		{
			BuildUpdateTable(nullability, selectQuery, updateClause);
			BuildUpdateSet  (nullability, selectQuery, updateClause);
		}

		protected virtual void BuildUpdateTable(NullabilityContext nullability, SelectQuery selectQuery, SqlUpdateClause updateClause)
		{
			AppendIndent().Append(UpdateKeyword);

			StartStatementQueryExtensions(selectQuery);
			BuildSkipFirst(nullability, selectQuery);

			StringBuilder.AppendLine().Append('\t');
			BuildUpdateTableName(nullability, selectQuery, updateClause);
			StringBuilder.AppendLine();
		}

		protected virtual void BuildUpdateTableName(NullabilityContext nullability, SelectQuery selectQuery, SqlUpdateClause updateClause)
		{
			if (updateClause.Table != null && (selectQuery.From.Tables.Count == 0 || updateClause.Table != selectQuery.From.Tables[0].Source))
			{
				BuildPhysicalTable(nullability, updateClause.Table, null);
			}
			else
			{
				if (selectQuery.From.Tables[0].Source is SelectQuery)
					StringBuilder.Length--;

				BuildTableName(nullability, selectQuery.From.Tables[0], true, true);
			}
		}

		protected virtual string UpdateKeyword => "UPDATE";
		protected virtual string UpdateSetKeyword => "SET";

		protected virtual void BuildUpdateSet(NullabilityContext nullability, SelectQuery? selectQuery, SqlUpdateClause updateClause)
		{
			AppendIndent()
				.AppendLine(UpdateSetKeyword);

			Indent++;

			var first = true;

			foreach (var expr in updateClause.Items)
			{
				if (!first)
					StringBuilder.AppendLine(Comma);

				first = false;

				AppendIndent();

				if (expr.Column is SqlRow row)
				{
					if (!SqlProviderFlags.RowConstructorSupport.HasFlag(RowFeature.Update))
						throw new LinqToDBException("This provider does not support SqlRow in UPDATE.");
					if (!SqlProviderFlags.RowConstructorSupport.HasFlag(RowFeature.UpdateLiteral) && expr.Expression is not SelectQuery)
						throw new LinqToDBException("This provider does not support SqlRow literal on the right-hand side of an UPDATE SET.");
				}

				BuildExpression(nullability, expr.Column, updateClause.TableSource != null, true, false);

				if (expr.Expression != null)
				{
					StringBuilder.Append(" = ");

					var addAlias = false;

					BuildColumnExpression(nullability, selectQuery, expr.Expression, null, ref addAlias);
				}
			}

			Indent--;

			StringBuilder.AppendLine();
		}

		#endregion

		#region Build Insert
		protected virtual string OutputKeyword       => "RETURNING";
		// don't change case, override in specific db builder, if database needs other case
		protected virtual string DeletedOutputTable  => "OLD";
		protected virtual string InsertedOutputTable => "NEW";

		protected void BuildInsertClause(NullabilityContext nullability, SqlStatement statement, SqlInsertClause insertClause, bool addAlias)
		{
			BuildInsertClause(nullability, statement, insertClause, "INSERT INTO ", true, addAlias);
		}

		protected virtual void BuildEmptyInsert(SqlInsertClause insertClause)
		{
			StringBuilder.AppendLine("DEFAULT VALUES");
		}

		protected virtual void BuildOutputSubclause(NullabilityContext nullability, SqlStatement statement, SqlInsertClause insertClause)
		{
		}

		protected virtual void BuildOutputSubclause(NullabilityContext nullability, SqlOutputClause? output)
		{
			if (output?.HasOutput == true)
			{
				AppendIndent()
					.AppendLine(OutputKeyword);

				if (output.InsertedTable?.SqlTableType == SqlTableType.SystemTable)
					output.InsertedTable.TableName = output.InsertedTable.TableName with { Name = InsertedOutputTable };

				if (output.DeletedTable?.SqlTableType == SqlTableType.SystemTable)
					output.DeletedTable.TableName  = output.DeletedTable.TableName  with { Name = DeletedOutputTable  };

				++Indent;

				var first = true;

				if (output.HasOutputItems)
				{
					foreach (var oi in output.OutputItems)
					{
						if (!first)
							StringBuilder.AppendLine(Comma);
						first = false;

						AppendIndent();

						BuildExpression(nullability, oi.Expression!);
					}

					StringBuilder
						.AppendLine();
				}

				--Indent;

				if (output.OutputColumns != null)
				{
					BuildOutputColumnExpressions(nullability, output.OutputColumns);
				}

				if (output.OutputTable != null)
				{
					AppendIndent()
						.Append("INTO ");
					BuildObjectName(StringBuilder, new(output.OutputTable.TableName.Name), ConvertType.NameToQueryTable, true, output.OutputTable.TableOptions);
					StringBuilder
						.AppendLine();

					AppendIndent()
						.AppendLine(OpenParens);

					++Indent;

					var firstColumn = true;
					if (output.HasOutputItems)
					{
						foreach (var oi in output.OutputItems)
						{
							if (!firstColumn)
								StringBuilder.AppendLine(Comma);
							firstColumn = false;

							AppendIndent();

							BuildExpression(nullability, oi.Column, false, true);
						}
					}

					StringBuilder
						.AppendLine();

					--Indent;

					AppendIndent()
						.AppendLine(")");
				}
			}
		}

		protected virtual void BuildInsertClause(NullabilityContext nullability, SqlStatement statement, SqlInsertClause insertClause, string? insertText, bool appendTableName, bool addAlias)
		{
			AppendIndent().Append(insertText);

			StartStatementQueryExtensions(statement.SelectQuery);

			if (appendTableName)
			{
				if (insertClause.Into != null)
				{
					BuildPhysicalTable(nullability, insertClause.Into, null);

					if (addAlias)
					{
						var ts    = Statement.SelectQuery!.GetTableSource(insertClause.Into!);
						var alias = GetTableAlias(ts!);

						if (alias != null)
						{
							StringBuilder
								.Append(" AS ");
							Convert(StringBuilder, alias, ConvertType.NameToQueryTableAlias);
						}
					}
				}
			}

			if (insertClause.Items.Count == 0)
			{
				StringBuilder.Append(' ');

				BuildOutputSubclause(nullability, statement, insertClause);

				BuildEmptyInsert(insertClause);
			}
			else
			{
				StringBuilder.AppendLine();

				AppendIndent().AppendLine(OpenParens);

				Indent++;

				var first = true;

				foreach (var expr in insertClause.Items)
				{
					if (!first)
						StringBuilder.AppendLine(Comma);
					first = false;

					AppendIndent();
					BuildExpression(nullability, expr.Column, false, true);
				}

				Indent--;

				StringBuilder.AppendLine();
				AppendIndent().AppendLine(")");

				BuildOutputSubclause(nullability, statement, insertClause);

				if (statement.QueryType == QueryType.InsertOrUpdate ||
					statement.QueryType == QueryType.MultiInsert ||
					statement.EnsureQuery().From.Tables.Count == 0)
				{
					AppendIndent().AppendLine("VALUES");
					AppendIndent().AppendLine(OpenParens);

					Indent++;

					first = true;

					foreach (var expr in insertClause.Items)
					{
						if (!first)
							StringBuilder.AppendLine(Comma);
						first = false;

						AppendIndent();
						BuildExpression(nullability, expr.Expression!);
					}

					Indent--;

					StringBuilder.AppendLine();
					AppendIndent().AppendLine(")");
				}
			}
		}

		protected virtual void BuildGetIdentity(NullabilityContext nullability, SqlInsertClause insertClause)
		{
			//throw new SqlException("Insert with identity is not supported by the '{0}' sql provider.", Name);
		}

		#endregion

		#region Build InsertOrUpdate

		protected virtual void BuildInsertOrUpdateQuery(SqlInsertOrUpdateStatement insertOrUpdate)
		{
			throw new SqlException("InsertOrUpdate query type is not supported by {0} provider.", Name);
		}

		protected virtual void BuildInsertOrUpdateQueryAsMerge(SqlInsertOrUpdateStatement insertOrUpdate, string? fromDummyTable)
		{
			var nullability = new NullabilityContext(insertOrUpdate.SelectQuery);

			SkipAlias = false;

			var table       = insertOrUpdate.Insert.Into;
			var targetAlias = ConvertInline(insertOrUpdate.SelectQuery.From.Tables[0].Alias!, ConvertType.NameToQueryTableAlias);
			var sourceAlias = ConvertInline(GetTempAliases(1, "s")[0],        ConvertType.NameToQueryTableAlias);
			var keys        = insertOrUpdate.Update.Keys;

			BuildTag(insertOrUpdate);
			AppendIndent().Append("MERGE INTO ");
			BuildPhysicalTable(nullability, table!, null);
			StringBuilder.Append(' ').AppendLine(targetAlias);

			AppendIndent().Append("USING (SELECT ");

			for (var i = 0; i < keys.Count; i++)
			{
				BuildExpression(nullability, keys[i].Expression!, false, false);
				StringBuilder.Append(" AS ");
				BuildExpression(nullability, keys[i].Column, false, false);

				if (i + 1 < keys.Count)
					StringBuilder.Append(InlineComma);
			}

			if (!string.IsNullOrEmpty(fromDummyTable))
				StringBuilder.Append(' ').Append(fromDummyTable);

			StringBuilder.Append(") ").Append(sourceAlias).AppendLine(" ON");

			AppendIndent().AppendLine(OpenParens);

			Indent++;

			for (var i = 0; i < keys.Count; i++)
			{
				var key = keys[i];

				AppendIndent();

				if (key.Column.CanBeNullable(nullability))
				{
					StringBuilder.Append('(');

					StringBuilder.Append(targetAlias).Append('.');
					BuildExpression(nullability, key.Column, false, false);
					StringBuilder.Append(" IS NULL AND ");

					StringBuilder.Append(sourceAlias).Append('.');
					BuildExpression(nullability, key.Column, false, false);
					StringBuilder.Append(" IS NULL OR ");
				}

				StringBuilder.Append(targetAlias).Append('.');
				BuildExpression(nullability, key.Column, false, false);

				StringBuilder.Append(" = ").Append(sourceAlias).Append('.');
				BuildExpression(nullability, key.Column, false, false);

				if (key.Column.CanBeNullable(nullability))
					StringBuilder.Append(')');

				if (i + 1 < keys.Count)
					StringBuilder.Append(" AND");

				StringBuilder.AppendLine();
			}

			Indent--;

			AppendIndent().AppendLine(")");

			if (insertOrUpdate.Update.Items.Count > 0)
			{
				AppendIndent().AppendLine("WHEN MATCHED THEN");

				Indent++;
				AppendIndent().AppendLine("UPDATE ");
				BuildUpdateSet(nullability, insertOrUpdate.SelectQuery, insertOrUpdate.Update);
				Indent--;
			}

			AppendIndent().AppendLine("WHEN NOT MATCHED THEN");

			Indent++;
			BuildInsertClause(nullability, insertOrUpdate, insertOrUpdate.Insert, "INSERT", false, false);
			Indent--;

			while (EndLine.Contains(StringBuilder[StringBuilder.Length - 1]))
				StringBuilder.Length--;
		}

		protected static readonly char[] EndLine = { ' ', '\r', '\n' };

		protected void BuildInsertOrUpdateQueryAsUpdateInsert(SqlInsertOrUpdateStatement insertOrUpdate)
		{
			var nullability = new NullabilityContext(insertOrUpdate.SelectQuery);

			BuildTag(insertOrUpdate);

			var buildUpdate = insertOrUpdate.Update.Items.Count > 0;
			if (buildUpdate)
			{
				BuildUpdateQuery(insertOrUpdate, insertOrUpdate.SelectQuery, insertOrUpdate.Update);
			}
			else
			{
				AppendIndent().Append("IF NOT EXISTS").AppendLine(OpenParens);
				Indent++;
				AppendIndent().AppendLine("SELECT 1 ");
				BuildFromClause(nullability, insertOrUpdate, insertOrUpdate.SelectQuery);
			}

			AppendIndent().AppendLine("WHERE");

			var alias = ConvertInline(insertOrUpdate.SelectQuery.From.Tables[0].Alias!, ConvertType.NameToQueryTableAlias);
			var exprs = insertOrUpdate.Update.Keys;

			Indent++;

			for (var i = 0; i < exprs.Count; i++)
			{
				var expr = exprs[i];

				AppendIndent();

				if (expr.Column.CanBeNullable(nullability))
				{
					StringBuilder.Append('(');

					StringBuilder.Append(alias).Append('.');
					BuildExpression(nullability, expr.Column, false, false);
					StringBuilder.Append(" IS NULL OR ");
				}

				StringBuilder.Append(alias).Append('.');
				BuildExpression(nullability, expr.Column, false, false);

				StringBuilder.Append(" = ");
				BuildExpression(nullability, Precedence.Comparison, expr.Expression!);

				if (expr.Column.CanBeNullable(nullability))
					StringBuilder.Append(')');

				if (i + 1 < exprs.Count)
					StringBuilder.Append(" AND");

				StringBuilder.AppendLine();
			}

			Indent--;

			if (buildUpdate)
			{
				StringBuilder.AppendLine();
				AppendIndent().AppendLine("IF @@ROWCOUNT = 0");
			}
			else
			{
				Indent--;
				AppendIndent().AppendLine(")");
			}

			AppendIndent().AppendLine("BEGIN");

			Indent++;

			BuildInsertQuery(insertOrUpdate, insertOrUpdate.Insert, false);

			Indent--;

			AppendIndent().AppendLine("END");

			StringBuilder.AppendLine();
		}

		#endregion

		#region Build DDL

		protected virtual void BuildTruncateTableStatement(SqlTruncateTableStatement truncateTable)
		{
			BuildTag(truncateTable);

			var table = truncateTable.Table;

			AppendIndent();

			BuildTruncateTable(truncateTable);

			BuildPhysicalTable(NullabilityContext.NonQuery, table!, null);
			StringBuilder.AppendLine();
		}

		protected virtual void BuildTruncateTable(SqlTruncateTableStatement truncateTable)
		{
			//StringBuilder.Append("TRUNCATE TABLE ");
			StringBuilder.Append("DELETE FROM ");
		}

		protected virtual void BuildDropTableStatement(SqlDropTableStatement dropTable)
		{
			BuildTag(dropTable);
			AppendIndent().Append("DROP TABLE ");
			BuildPhysicalTable(NullabilityContext.NonQuery, dropTable.Table!, null);
			StringBuilder.AppendLine();
		}

		protected void BuildDropTableStatementIfExists(SqlDropTableStatement dropTable)
		{
			BuildTag(dropTable);
			AppendIndent().Append("DROP TABLE ");

			if (dropTable.Table.TableOptions.HasDropIfExists())
				StringBuilder.Append("IF EXISTS ");

			BuildPhysicalTable(NullabilityContext.NonQuery, dropTable.Table!, null);
			StringBuilder.AppendLine();
		}

		protected virtual void BuildCreateTableCommand(SqlTable table)
		{
			StringBuilder
				.Append("CREATE TABLE ");
		}

		protected virtual void BuildStartCreateTableStatement(SqlCreateTableStatement createTable)
		{
			if (createTable.StatementHeader == null)
			{
				AppendIndent();
				BuildCreateTableCommand(createTable.Table!);
				BuildPhysicalTable(NullabilityContext.NonQuery, createTable.Table!, null);
			}
			else
			{
				var name = WithStringBuilder(
					static ctx =>
					{
						ctx.this_.BuildPhysicalTable(NullabilityContext.NonQuery, ctx.createTable.Table!, null);
					}, (this_: this, createTable));

				AppendIndent().AppendFormat(createTable.StatementHeader, name);
			}
		}

		protected virtual void BuildEndCreateTableStatement(SqlCreateTableStatement createTable)
		{
			if (createTable.StatementFooter != null)
				AppendIndent().Append(createTable.StatementFooter);
		}

		sealed class CreateFieldInfo
		{
			public SqlField      Field = null!;
			public StringBuilder StringBuilder = null!;
			public string        Name = null!;
			public string?       Type;
			public string        Identity = null!;
			public string?       Null;
		}

		protected virtual void BuildCreateTableStatement(SqlCreateTableStatement createTable)
		{
			var table = createTable.Table;

			BuildStartCreateTableStatement(createTable);

			StringBuilder.AppendLine();
			AppendIndent().Append(OpenParens);
			Indent++;

			// Order columns by the Order field. Positive first then negative.
			var orderedFields = table.Fields.OrderBy(_ => _.CreateOrder >= 0 ? 0 : (_.CreateOrder == null ? 1 : 2)).ThenBy(_ => _.CreateOrder);
			var fields = orderedFields.Select(f => new CreateFieldInfo { Field = f, StringBuilder = new StringBuilder() }).ToList();
			var maxlen = 0;

			var isAnyCreateFormat = false;

			// Build field name.
			//
			foreach (var field in fields)
			{
				Convert(field.StringBuilder, field.Field.PhysicalName, ConvertType.NameToQueryField);

				if (maxlen < field.StringBuilder.Length)
					maxlen = field.StringBuilder.Length;

				if (field.Field.CreateFormat != null)
					isAnyCreateFormat = true;
			}

			AppendToMax(fields, maxlen, true);

			if (isAnyCreateFormat)
				foreach (var field in fields)
					if (field.Field.CreateFormat != null)
						field.Name = field.StringBuilder.ToString() + ' ';

			// Build field type.
			//
			foreach (var field in fields)
			{
				field.StringBuilder.Append(' ');

				if (!string.IsNullOrEmpty(field.Field.Type.DbType))
					field.StringBuilder.Append(field.Field.Type.DbType);
				else
				{
					var sb = StringBuilder;
					StringBuilder = field.StringBuilder;

					BuildCreateTableFieldType(field.Field);

					StringBuilder = sb;
				}

				if (maxlen < field.StringBuilder.Length)
					maxlen = field.StringBuilder.Length;
			}

			AppendToMax(fields, maxlen, true);

			if (isAnyCreateFormat)
			{
				foreach (var field in fields)
				{
					if (field.Field.CreateFormat != null)
					{
						var sb = field.StringBuilder;

						field.Type = sb.ToString().Substring(field.Name.Length) + ' ';
						sb.Length = 0;
					}
				}
			}

			var hasIdentity = fields.Any(f => f.Field.IsIdentity);

			// Build identity attribute.
			//
			if (hasIdentity)
			{
				foreach (var field in fields)
				{
					if (field.Field.CreateFormat == null)
						field.StringBuilder.Append(' ');

					if (field.Field.IsIdentity)
						WithStringBuilder(field.StringBuilder, static ctx => ctx.this_.BuildCreateTableIdentityAttribute1(ctx.field.Field), (this_: this, field));

					if (field.Field.CreateFormat != null)
					{
						field.Identity = field.StringBuilder.ToString();

						if (field.Identity.Length != 0)
							field.Identity += ' ';

						field.StringBuilder.Length = 0;
					}
					else if (maxlen < field.StringBuilder.Length)
					{
						maxlen = field.StringBuilder.Length;
					}
				}

				AppendToMax(fields, maxlen, false);
			}

			// Build nullable attribute.
			//
			foreach (var field in fields)
			{
				if (field.Field.CreateFormat == null)
					field.StringBuilder.Append(' ');

				WithStringBuilder(
					field.StringBuilder,
					static ctx => ctx.this_.BuildCreateTableNullAttribute(ctx.field.Field, ctx.createTable.DefaultNullable),
					(this_: this, field, createTable));

				if (field.Field.CreateFormat != null)
				{
					field.Null = field.StringBuilder.ToString() + ' ';
					field.StringBuilder.Length = 0;
				}
				else if (maxlen < field.StringBuilder.Length)
				{
					maxlen = field.StringBuilder.Length;
				}
			}

			AppendToMax(fields, maxlen, false);

			// Build identity attribute.
			//
			if (hasIdentity)
			{
				foreach (var field in fields)
				{
					if (field.Field.CreateFormat == null)
						field.StringBuilder.Append(' ');

					if (field.Field.IsIdentity)
						WithStringBuilder(field.StringBuilder, static ctx => ctx.this_.BuildCreateTableIdentityAttribute2(ctx.field.Field), (this_: this, field));

					if (field.Field.CreateFormat != null)
					{
						if (field.Identity.Length == 0)
						{
							field.Identity = field.StringBuilder.ToString() + ' ';
							field.StringBuilder.Length = 0;
						}
					}
					else if (maxlen < field.StringBuilder.Length)
					{
						maxlen = field.StringBuilder.Length;
					}
				}

				AppendToMax(fields, maxlen, false);
			}

			// Build fields.
			//
			for (var i = 0; i < fields.Count; i++)
			{
				while (fields[i].StringBuilder.Length > 0 && fields[i].StringBuilder[fields[i].StringBuilder.Length - 1] == ' ')
					fields[i].StringBuilder.Length--;

				StringBuilder.AppendLine(i == 0 ? "" : Comma);
				AppendIndent();

				var field = fields[i];

				if (field.Field.CreateFormat != null)
				{
					StringBuilder.AppendFormat(field.Field.CreateFormat, field.Name, field.Type, field.Null, field.Identity);

					while (StringBuilder.Length > 0 && StringBuilder[StringBuilder.Length - 1] == ' ')
						StringBuilder.Length--;
				}
				else
				{
					StringBuilder.Append(field.StringBuilder);
				}
			}

			var pk =
			(
				from f in fields
				where f.Field.IsPrimaryKey
				orderby f.Field.PrimaryKeyOrder
				select f
			).ToList();

			if (pk.Count > 0)
			{
				StringBuilder.AppendLine(Comma).AppendLine();

				BuildCreateTablePrimaryKey(createTable, ConvertInline("PK_" + createTable.Table.TableName.Name, ConvertType.NameToQueryTable),
					pk.Select(f => ConvertInline(f.Field.PhysicalName, ConvertType.NameToQueryField)));
			}

			Indent--;
			StringBuilder.AppendLine();
			AppendIndent().AppendLine(")");

			BuildEndCreateTableStatement(createTable);

			static void AppendToMax(IEnumerable<CreateFieldInfo> fields, int maxlen, bool addCreateFormat)
			{
				foreach (var field in fields)
					if (addCreateFormat || field.Field.CreateFormat == null)
						while (maxlen > field.StringBuilder.Length)
							field.StringBuilder.Append(' ');
			}
		}

		internal void BuildTypeName(StringBuilder sb, SqlDataType type)
		{
			StringBuilder = sb;
			BuildDataType(type, forCreateTable: true, canBeNull: true);
		}

		protected virtual void BuildCreateTableFieldType(SqlField field)
		{
			BuildDataType(new SqlDataType(field), forCreateTable: true, field.CanBeNull);
		}

		protected virtual void BuildCreateTableNullAttribute(SqlField field, DefaultNullable defaultNullable)
		{
			if (defaultNullable == DefaultNullable.Null && field.CanBeNull)
				return;

			if (defaultNullable == DefaultNullable.NotNull && !field.CanBeNull)
				return;

			StringBuilder.Append(field.CanBeNull ? "    NULL" : "NOT NULL");
		}

		protected virtual void BuildCreateTableIdentityAttribute1(SqlField field)
		{
		}

		protected virtual void BuildCreateTableIdentityAttribute2(SqlField field)
		{
		}

		protected virtual void BuildCreateTablePrimaryKey(SqlCreateTableStatement createTable, string pkName, IEnumerable<string> fieldNames)
		{
			AppendIndent();
			StringBuilder.Append("CONSTRAINT ").Append(pkName).Append(" PRIMARY KEY (");
			StringBuilder.Append(string.Join(InlineComma, fieldNames));
			StringBuilder.Append(')');
		}

		#endregion

		#region Build From

		protected virtual void BuildDeleteFromClause(NullabilityContext nullability, SqlDeleteStatement deleteStatement)
		{
			BuildFromClause(nullability, Statement, deleteStatement.SelectQuery);
		}

		protected virtual void BuildFromClause(NullabilityContext nullability, SqlStatement statement, SelectQuery selectQuery)
		{
			if (selectQuery.From.Tables.Count == 0 || selectQuery.From.Tables[0].Alias == "$F")
				return;

			AppendIndent();

			StringBuilder.Append("FROM").AppendLine();

			Indent++;
			AppendIndent();

			var first = true;

			foreach (var ts in selectQuery.From.Tables)
			{
				if (!first)
				{
					StringBuilder.AppendLine(Comma);
					AppendIndent();
				}

				first = false;

				var jn = ParenthesizeJoin(ts.Joins) ? ts.GetJoinNumber() : 0;

				if (jn > 0)
				{
					jn--;
					for (var i = 0; i < jn; i++)
						StringBuilder.Append('(');
				}

				BuildTableName(nullability, ts, true, true);

				foreach (var jt in ts.Joins)
					BuildJoinTable(nullability, selectQuery, ts, jt, ref jn);
			}

			Indent--;

			StringBuilder.AppendLine();
		}

		private static readonly Regex _selectDetector = new (@"^[\W\r\n]*select\W+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		protected virtual bool? BuildPhysicalTable(NullabilityContext nullability, ISqlTableSource table, string? alias, string? defaultDatabaseName = null)
		{
			var tablePath = TablePath;

			if (alias != null)
			{
				if (TablePath is { Length: > 0 })
					TablePath += '.';
				TablePath += alias;
			}

			bool? buildAlias = null;

			switch (table.ElementType)
			{
				case QueryElementType.SqlTable        :
				case QueryElementType.TableSource     :
				{
					var name = GetPhysicalTableName(nullability, table, alias, defaultDatabaseName : defaultDatabaseName);

					StringBuilder.Append(name);

					if (alias != null && table is SqlTable { ID: {} id })
					{
						var path = TablePath;

						if (QueryName is not null)
							path += $"@{QueryName}";

						(TableIDs ??= new())[id] = new(alias, name, path!);
					}

					break;
				}

				case QueryElementType.SqlQuery        :
					StringBuilder.AppendLine(OpenParens);
					BuildSqlBuilder(nullability, (SelectQuery)table, Indent + 1, false);
					AppendIndent().Append(')');
					break;

				case QueryElementType.SqlCteTable     :
				case QueryElementType.SqlTableLikeSource:
					StringBuilder.Append(GetPhysicalTableName(nullability, table, alias));
					break;

				case QueryElementType.SqlRawSqlTable  :

					var rawSqlTable = (SqlRawSqlTable)table;

					var appendParentheses = _selectDetector.IsMatch(rawSqlTable.SQL);
					var multiLine         = appendParentheses || rawSqlTable.SQL.Contains('\n');

					if (appendParentheses)
						StringBuilder.AppendLine(OpenParens);
					else if (multiLine)
						StringBuilder.AppendLine();

					var parameters = rawSqlTable.Parameters;
					if (rawSqlTable.Parameters.Any(e => e.ElementType == QueryElementType.SqlAliasPlaceholder))
					{
						buildAlias = false;
						var aliasExpr = new SqlExpression(ConvertInline(alias!, ConvertType.NameToQueryTableAlias), Precedence.Primary);
						parameters = rawSqlTable.Parameters.Select(e =>
								e.ElementType == QueryElementType.SqlAliasPlaceholder ? aliasExpr : e)
							.ToArray();
					}

					BuildFormatValues(nullability, IdentText(rawSqlTable.SQL, multiLine ? Indent + 1 : 0), parameters, Precedence.Primary);

					if (multiLine)
						StringBuilder.AppendLine();
					if (appendParentheses)
						AppendIndent().Append(')');

					break;

				case QueryElementType.SqlValuesTable:
				{
					if (alias == null)
						throw new LinqToDBException("Alias required for SqlValuesTable.");
					BuildSqlValuesTable(nullability, (SqlValuesTable)table, alias, out var aliasBuilt);
					buildAlias = !aliasBuilt;
					break;
				}

				default:
					throw new InvalidOperationException($"Unexpected table type {table.ElementType}");
			}

			TablePath = tablePath;

			return buildAlias;
		}

		protected virtual void BuildSqlValuesTable(NullabilityContext nullability, SqlValuesTable valuesTable, string alias, out bool aliasBuilt)
		{
			valuesTable = ConvertElement(valuesTable, nullability);
			var rows = valuesTable.BuildRows(OptimizationContext.Context);
			if (rows?.Count > 0)
			{
				StringBuilder.Append(OpenParens);

				if (IsValuesSyntaxSupported)
					BuildValues(nullability, valuesTable, rows);
				else
					BuildValuesAsSelectsUnion(nullability, valuesTable.Fields, valuesTable, rows);

				StringBuilder.Append(')');
			}
			else if (IsEmptyValuesSourceSupported)
			{
				StringBuilder.Append(OpenParens);
				BuildEmptyValues(nullability, valuesTable);
				StringBuilder.Append(')');
			}
			else
				throw new LinqToDBException($"{Name} doesn't support values with empty source");

			aliasBuilt = IsValuesSyntaxSupported;
			if (aliasBuilt)
			{
				BuildSqlValuesAlias(nullability, valuesTable, alias);
			}
		}

		private void BuildSqlValuesAlias(NullabilityContext nullability, SqlValuesTable valuesTable, string alias)
		{
			valuesTable = ConvertElement(valuesTable, nullability);
			StringBuilder.Append(' ');

			BuildObjectName(StringBuilder, new (alias), ConvertType.NameToQueryFieldAlias, true, TableOptions.NotSet);

			if (SupportsColumnAliasesInSource)
			{
				StringBuilder.Append(OpenParens);

				var first = true;
				foreach (var field in valuesTable.Fields)
				{
					if (!first)
						StringBuilder.Append(Comma).Append(' ');

					first = false;
					Convert(StringBuilder, field.PhysicalName, ConvertType.NameToQueryField);
				}

				StringBuilder.Append(')');
			}
		}

		protected void BuildEmptyValues(NullabilityContext nullability, SqlValuesTable valuesTable)
		{
			StringBuilder.Append("SELECT ");
			for (var i = 0; i < valuesTable.Fields.Count; i++)
			{
				if (i > 0)
					StringBuilder.Append(InlineComma);
				var field = valuesTable.Fields[i];
				if (IsSqlValuesTableValueTypeRequired(nullability, valuesTable, Array<ISqlExpression[]>.Empty, -1, i))
					BuildTypedExpression(nullability, new SqlDataType(field), new SqlValue(field.Type, null));
				else
					BuildExpression(nullability, new SqlValue(field.Type, null));
				StringBuilder.Append(' ');
				Convert(StringBuilder, field.PhysicalName, ConvertType.NameToQueryField);
			}

			if (FakeTable != null)
			{
				StringBuilder.Append(" FROM ");
				BuildFakeTableName();
			}

			StringBuilder
				.Append(" WHERE 1 = 0");
		}

		protected void BuildTableName(NullabilityContext nullability, SqlTableSource ts, bool buildName, bool buildAlias)
		{
			string? alias = null;

			if (buildName)
			{
				alias = GetTableAlias(ts);
				var isBuildAlias = BuildPhysicalTable(nullability, ts.Source, alias);
				if (isBuildAlias == false)
					buildAlias = false;
			}

			if (buildAlias)
			{
				if (ts.SqlTableType != SqlTableType.Expression)
				{

					if (buildName && ts.Source is SqlTable { SqlQueryExtensions : not null } t)
					{
						BuildTableNameExtensions(nullability, t);
					}

					if (buildName == false)
						alias = GetTableAlias(ts);

					if (!string.IsNullOrEmpty(alias))
					{
						if (buildName)
							StringBuilder.Append(' ');
						Convert(StringBuilder, alias!, ConvertType.NameToQueryTableAlias);
					}
				}
			}

			if (buildName && buildAlias && ts.Source is SqlTable { SqlQueryExtensions: not null } table)
			{
				BuildTableExtensions(nullability, table, alias!);
			}
		}

		protected virtual void BuildTableExtensions(NullabilityContext nullability, SqlTable table, string alias)
		{
		}

		protected virtual void BuildTableNameExtensions(NullabilityContext nullability, SqlTable table)
		{
		}

		static readonly ConcurrentDictionary<Type,ISqlExtensionBuilder> _extensionBuilders = new()
		{
			[typeof(NoneExtensionBuilder)]               = new NoneExtensionBuilder(),
			[typeof(HintExtensionBuilder)]               = new HintExtensionBuilder(),
			[typeof(HintWithParameterExtensionBuilder)]  = new HintWithParameterExtensionBuilder(),
			[typeof(HintWithParametersExtensionBuilder)] = new HintWithParametersExtensionBuilder(),
		};

		protected static ISqlExtensionBuilder GetExtensionBuilder(Type builderType)
		{
			return _extensionBuilders.GetOrAdd(
				builderType,
				type =>
				{
					var inst = Activator.CreateInstance(type);

					if (inst is not ISqlExtensionBuilder builder)
						throw new LinqToDBException($"Type '{builderType.FullName}' must implement the '{typeof(ISqlExtensionBuilder).FullName}' interface.");

					return builder;
				});
		}

		protected void BuildTableExtensions(
			NullabilityContext nullability, 
			StringBuilder sb,
			SqlTable table, string alias,
			string? prefix, string delimiter, string? suffix)
		{
			BuildTableExtensions(
				nullability,
				sb,
				table,  alias,
				prefix, delimiter, suffix,
				ext =>
					ext.Scope is
						Sql.QueryExtensionScope.TableHint or
						Sql.QueryExtensionScope.IndexHint or
						Sql.QueryExtensionScope.TablesInScopeHint);
		}

		protected void BuildTableExtensions(
			NullabilityContext nullability, 
			StringBuilder sb,
			SqlTable table, string alias,
			string? prefix, string delimiter, string? suffix,
			Func<SqlQueryExtension,bool> tableExtensionFilter)
		{
			if (table.SqlQueryExtensions?.Any(tableExtensionFilter) == true)
			{
				if (prefix != null)
					sb.Append(prefix);

				foreach (var ext in table.SqlQueryExtensions.Where(tableExtensionFilter))
				{
					if (ext.BuilderType != null)
					{
						var extensionBuilder = GetExtensionBuilder(ext.BuilderType);

						switch (extensionBuilder)
						{
							case ISqlQueryExtensionBuilder queryExtensionBuilder:
								queryExtensionBuilder.Build(nullability, this, sb, ext);
								break;
							case ISqlTableExtensionBuilder tableExtensionBuilder:
								tableExtensionBuilder.Build(nullability, this, sb, ext, table, alias);
								break;
							default:
								throw new LinqToDBException($"Type '{ext.BuilderType.FullName}' must implement either '{typeof(ISqlQueryExtensionBuilder).FullName}' or '{typeof(ISqlTableExtensionBuilder).FullName}' interface.");
						}
					}

					sb.Append(delimiter);
				}

				sb.Length -= delimiter.Length;

				if (suffix != null)
					sb.Append(suffix);
			}
		}

		protected void BuildQueryExtensions(
			NullabilityContext nullability, 
			StringBuilder sb,
			List<SqlQueryExtension> sqlQueryExtensions,
			string? prefix, string delimiter, string? suffix)
		{
			if (sqlQueryExtensions.Any(ext => ext.Scope is Sql.QueryExtensionScope.QueryHint or Sql.QueryExtensionScope.SubQueryHint))
			{
				if (prefix != null)
					sb.Append(prefix);

				foreach (var ext in sqlQueryExtensions!)
				{
					if (ext.BuilderType != null)
					{
						var extensionBuilder = GetExtensionBuilder(ext.BuilderType);

						switch (extensionBuilder)
						{
							case ISqlQueryExtensionBuilder queryExtensionBuilder:
								queryExtensionBuilder.Build(nullability, this, sb, ext);
								break;
							default:
								throw new LinqToDBException($"Type '{ext.BuilderType.FullName}' must implement either '{typeof(ISqlQueryExtensionBuilder).FullName}' or '{typeof(ISqlTableExtensionBuilder).FullName}' interface.");
						}
					}

					sb.Append(delimiter);
				}

				sb.Length -= delimiter.Length;

				if (suffix != null)
					sb.Append(suffix);
			}
		}

		protected void BuildJoinTable(NullabilityContext nullability, SelectQuery selectQuery, SqlTableSource tableSource, SqlJoinedTable join, ref int joinCounter)
		{
			StringBuilder.AppendLine();
			Indent++;
			AppendIndent();

			var condition = ConvertElement(join.Condition, nullability.WithOuterInnerSource(tableSource.Source, join.Table.Source));
			var buildOn   = BuildJoinType (join, condition);

			if (IsNestedJoinParenthesisRequired && join.Table.Joins.Count != 0)
				StringBuilder.Append('(');

			BuildTableName(nullability, join.Table, true, true);

			if (IsNestedJoinSupported && join.Table.Joins.Count != 0)
			{
				foreach (var jt in join.Table.Joins)
					BuildJoinTable(nullability, selectQuery, tableSource, jt, ref joinCounter);

				if (IsNestedJoinParenthesisRequired && join.Table.Joins.Count != 0)
					StringBuilder.Append(')');

				if (buildOn)
				{
					StringBuilder.AppendLine();
					AppendIndent();
					StringBuilder.Append("ON ");
				}
			}
			else if (buildOn)
				StringBuilder.Append(" ON ");

			if (WrapJoinCondition && condition.Conditions.Count > 0)
				StringBuilder.Append('(');

			if (buildOn)
			{
				if (condition.Conditions.Count != 0)
					BuildSearchCondition(nullability, Precedence.Unknown, condition, wrapCondition: false);
				else
					StringBuilder.Append("1=1");
			}

			if (WrapJoinCondition && condition.Conditions.Count > 0)
				StringBuilder.Append(')');

			if (joinCounter > 0)
			{
				joinCounter--;
				StringBuilder.Append(')');
			}

			if (!IsNestedJoinSupported)
				foreach (var jt in join.Table.Joins)
					BuildJoinTable(nullability, selectQuery, tableSource, jt, ref joinCounter);

			Indent--;
		}

		protected virtual bool BuildJoinType(SqlJoinedTable join, SqlSearchCondition condition)
		{
			switch (join.JoinType)
			{
				case JoinType.Inner when SqlProviderFlags.IsCrossJoinSupported && condition.Conditions.IsNullOrEmpty() :
					                      StringBuilder.Append("CROSS JOIN ");  return false;
				case JoinType.Inner     : StringBuilder.Append("INNER JOIN ");  return true;
				case JoinType.Left      : StringBuilder.Append("LEFT JOIN ");   return true;
				case JoinType.CrossApply: StringBuilder.Append("CROSS APPLY "); return false;
				case JoinType.OuterApply: StringBuilder.Append("OUTER APPLY "); return false;
				case JoinType.Right     : StringBuilder.Append("RIGHT JOIN ");  return true;
				case JoinType.Full      : StringBuilder.Append("FULL JOIN ");   return true;
				default: throw new InvalidOperationException();
			}
		}

		#endregion

		#region Where Clause

		protected virtual bool BuildWhere(NullabilityContext nullability, SelectQuery selectQuery)
		{
			var condition = ConvertElement(selectQuery.Where.SearchCondition, nullability);

			return condition.Conditions.Count > 0;
		}

		protected virtual void BuildWhereClause(NullabilityContext nullability, SelectQuery selectQuery)
		{
			if (!BuildWhere(nullability, selectQuery))
				return;

			AppendIndent();

			StringBuilder.Append("WHERE").AppendLine();

			Indent++;
			AppendIndent();
			BuildWhereSearchCondition(nullability, selectQuery, selectQuery.Where.SearchCondition);
			Indent--;

			StringBuilder.AppendLine();
		}

		#endregion

		#region GroupBy Clause

		protected virtual void BuildGroupByClause(NullabilityContext nullability, SelectQuery selectQuery)
		{
			if (selectQuery.GroupBy.Items.Count == 0)
				return;

			BuildGroupByBody(nullability, selectQuery.GroupBy.GroupingType, selectQuery.GroupBy.Items);
		}

		protected virtual void BuildGroupByBody(NullabilityContext nullability, GroupingType groupingType, List<ISqlExpression> items)
		{
			AppendIndent();

			StringBuilder.Append("GROUP BY");

			switch (groupingType)
			{
				case GroupingType.Default:
					break;
				case GroupingType.GroupBySets:
					StringBuilder.Append(" GROUPING SETS");
					break;
				case GroupingType.Rollup:
					StringBuilder.Append(" ROLLUP");
					break;
				case GroupingType.Cube:
					StringBuilder.Append(" CUBE");
					break;
				default:
					throw new InvalidOperationException($"Unexpected grouping type: {groupingType}");
			}

			if (groupingType != GroupingType.Default)
				StringBuilder.Append(' ').AppendLine(OpenParens);
			else
				StringBuilder.AppendLine();

			Indent++;

			for (var i = 0; i < items.Count; i++)
			{
				AppendIndent();

				var expr = WrapBooleanExpression(items[i]);
				BuildExpression(nullability, expr);

				if (i + 1 < items.Count)
					StringBuilder.AppendLine(Comma);
				else
					StringBuilder.AppendLine();
			}

			Indent--;

			if (groupingType != GroupingType.Default)
			{
				AppendIndent();
				StringBuilder.Append(')').AppendLine();
			}
		}

		#endregion

		#region Having Clause

		protected virtual void BuildHavingClause(NullabilityContext nullability, SelectQuery selectQuery)
		{
			var condition = ConvertElement(selectQuery.Where.Having.SearchCondition, nullability);
			if (condition.Conditions.Count == 0)
				return;

			AppendIndent();

			StringBuilder.Append("HAVING").AppendLine();

			Indent++;
			AppendIndent();
			BuildWhereSearchCondition(nullability, selectQuery, condition);
			Indent--;

			StringBuilder.AppendLine();
		}

		#endregion

		#region OrderBy Clause

		protected virtual void BuildOrderByClause(NullabilityContext nullability, SelectQuery selectQuery)
		{
			if (selectQuery.OrderBy.Items.Count == 0)
				return;

			AppendIndent();

			StringBuilder.Append("ORDER BY").AppendLine();

			Indent++;

			for (var i = 0; i < selectQuery.OrderBy.Items.Count; i++)
			{
				AppendIndent();

				var item = selectQuery.OrderBy.Items[i];

				BuildExpression(nullability, WrapBooleanExpression(item.Expression));

				if (item.IsDescending)
					StringBuilder.Append(" DESC");

				if (i + 1 < selectQuery.OrderBy.Items.Count)
					StringBuilder.AppendLine(Comma);
				else
					StringBuilder.AppendLine();
			}

			Indent--;
		}

		#endregion

		#region Skip/Take

		protected virtual bool   SkipFirst    => true;
		protected virtual string? SkipFormat  => null;
		protected virtual string? FirstFormat (SelectQuery selectQuery) => null;
		protected virtual string? LimitFormat (SelectQuery selectQuery) => null;
		protected virtual string? OffsetFormat(SelectQuery selectQuery) => null;
		protected virtual bool   OffsetFirst  => false;
		protected virtual string TakePercent  => "PERCENT";
		protected virtual string TakeTies     => "WITH TIES";

		protected bool NeedSkip(ISqlExpression? takeExpression, ISqlExpression? skipExpression)
			=> skipExpression != null && SqlProviderFlags.GetIsSkipSupportedFlag(takeExpression, skipExpression);

		protected bool NeedTake(ISqlExpression? takeExpression)
			=> takeExpression != null && SqlProviderFlags.IsTakeSupported;

		protected virtual void BuildSkipFirst(NullabilityContext nullability, SelectQuery selectQuery)
		{
			SqlOptimizer.ConvertSkipTake(nullability, MappingSchema, DataOptions, selectQuery, OptimizationContext, out var takeExpr, out var skipExpr);

			if (SkipFirst && NeedSkip(takeExpr, skipExpr) && SkipFormat != null)
				StringBuilder.Append(' ').AppendFormat(
					SkipFormat, WithStringBuilderBuildExpression(nullability, skipExpr!));

			if (NeedTake(takeExpr) && FirstFormat(selectQuery) != null)
			{
				StringBuilder.Append(' ').AppendFormat(
					FirstFormat(selectQuery)!, WithStringBuilderBuildExpression(nullability, takeExpr!));

				BuildTakeHints(selectQuery);
			}

			if (!SkipFirst && NeedSkip(takeExpr, skipExpr) && SkipFormat != null)
				StringBuilder.Append(' ').AppendFormat(
					SkipFormat, WithStringBuilderBuildExpression(nullability, skipExpr!));
		}

		protected virtual void BuildTakeHints(SelectQuery selectQuery)
		{
			if (selectQuery.Select.TakeHints == null)
				return;

			if ((selectQuery.Select.TakeHints.Value & TakeHints.Percent) != 0)
				StringBuilder.Append(' ').Append(TakePercent);

			if ((selectQuery.Select.TakeHints.Value & TakeHints.WithTies) != 0)
				StringBuilder.Append(' ').Append(TakeTies);
		}

		protected virtual void BuildOffsetLimit(NullabilityContext nullability, SelectQuery selectQuery)
		{
			SqlOptimizer.ConvertSkipTake(nullability, MappingSchema, DataOptions, selectQuery, OptimizationContext, out var takeExpr, out var skipExpr);

			var doSkip = NeedSkip(takeExpr, skipExpr) && OffsetFormat(selectQuery) != null;
			var doTake = NeedTake(takeExpr)           && LimitFormat(selectQuery)  != null;

			if (doSkip || doTake)
			{
				AppendIndent();

				if (doSkip && OffsetFirst)
				{
					StringBuilder.AppendFormat(
						OffsetFormat(selectQuery)!, WithStringBuilderBuildExpression(nullability, skipExpr!));

					if (doTake)
						StringBuilder.Append(' ');
				}
		
				if (doTake)
				{
					StringBuilder.AppendFormat(
						LimitFormat(selectQuery)!, WithStringBuilderBuildExpression(nullability, takeExpr!));

					if (doSkip)
						StringBuilder.Append(' ');
				}

				if (doSkip && !OffsetFirst)
					StringBuilder.AppendFormat(
						OffsetFormat(selectQuery)!, WithStringBuilderBuildExpression(nullability, skipExpr!));

				StringBuilder.AppendLine();
			}
		}

		#endregion

		#region Builders

		#region BuildSearchCondition

		protected virtual void BuildWhereSearchCondition(NullabilityContext nullability, SelectQuery selectQuery, SqlSearchCondition condition)
		{
			BuildSearchCondition(nullability, Precedence.Unknown, condition, wrapCondition: true);
		}

		protected virtual void BuildSearchCondition(NullabilityContext nullability, SqlSearchCondition condition, bool wrapCondition)
		{
			condition = ConvertElement(condition, nullability);

			var isOr = (bool?)null;
			var len = StringBuilder.Length;
			var parentPrecedence = condition.Precedence + 1;

			if (condition.Conditions.Count == 0)
			{
				BuildPredicate(nullability, parentPrecedence, parentPrecedence,
					new SqlPredicate.ExprExpr(
						new SqlValue(true), 
						SqlPredicate.Operator.Equal,
						new SqlValue(true), false));
			}

			foreach (var cond in condition.Conditions)
			{
				if (isOr != null)
				{
					StringBuilder.Append(isOr.Value ? " OR" : " AND");

					if (condition.Conditions.Count < 4 && StringBuilder.Length - len < 50 || !wrapCondition)
					{
						StringBuilder.Append(' ');
					}
					else
					{
						StringBuilder.AppendLine();
						AppendIndent();
						len = StringBuilder.Length;
					}
				}

				if (cond.IsNot)
					StringBuilder.Append("NOT ");

				var precedence = GetPrecedence(cond.Predicate);

				BuildPredicate(nullability, cond.IsNot ? Precedence.LogicalNegation : parentPrecedence, precedence, cond.Predicate);

				isOr = cond.IsOr;
			}
		}

		protected virtual void BuildSearchCondition(NullabilityContext nullability, int parentPrecedence, SqlSearchCondition condition, bool wrapCondition)
		{
			condition = ConvertElement(condition, nullability);

			var wrap = Wrap(GetPrecedence(condition as ISqlExpression), parentPrecedence);

			if (wrap) StringBuilder.Append('(');
			BuildSearchCondition(nullability, condition, wrapCondition);
			if (wrap) StringBuilder.Append(')');
		}

		#endregion

		#region BuildPredicate

		protected virtual void BuildPredicate(NullabilityContext nullability, ISqlPredicate predicate)
		{
			switch (predicate.ElementType)
			{
				case QueryElementType.ExprExprPredicate:
					BuildExprExprPredicate(nullability, (SqlPredicate.ExprExpr) predicate);
					break;

				case QueryElementType.LikePredicate:
					BuildLikePredicate(nullability, (SqlPredicate.Like)predicate);
					break;

				case QueryElementType.BetweenPredicate:
					{
						BuildExpression(nullability, GetPrecedence((SqlPredicate.Between)predicate), ((SqlPredicate.Between)predicate).Expr1);
						if (((SqlPredicate.Between)predicate).IsNot) StringBuilder.Append(" NOT");
						StringBuilder.Append(" BETWEEN ");
						BuildExpression(nullability, GetPrecedence((SqlPredicate.Between)predicate), ((SqlPredicate.Between)predicate).Expr2);
						StringBuilder.Append(" AND ");
						BuildExpression(nullability, GetPrecedence((SqlPredicate.Between)predicate), ((SqlPredicate.Between)predicate).Expr3);
					}

					break;

				case QueryElementType.IsNullPredicate:
					{
						BuildExpression(nullability, GetPrecedence((SqlPredicate.IsNull)predicate), ((SqlPredicate.IsNull)predicate).Expr1);
						StringBuilder.Append(((SqlPredicate.IsNull)predicate).IsNot ? " IS NOT NULL" : " IS NULL");
					}

					break;

				case QueryElementType.IsDistinctPredicate:
					BuildIsDistinctPredicate(nullability, (SqlPredicate.IsDistinct)predicate);
					break;

				case QueryElementType.InSubQueryPredicate:
					{
						BuildExpression(nullability, GetPrecedence((SqlPredicate.InSubQuery)predicate), ((SqlPredicate.InSubQuery)predicate).Expr1);
						StringBuilder.Append(((SqlPredicate.InSubQuery)predicate).IsNot ? " NOT IN " : " IN ");
						BuildExpression(nullability, GetPrecedence((SqlPredicate.InSubQuery)predicate), ((SqlPredicate.InSubQuery)predicate).SubQuery);
					}

					break;

				case QueryElementType.InListPredicate:
					BuildInListPredicate(nullability, predicate);
					break;

				case QueryElementType.FuncLikePredicate:
					BuildExpression(nullability, ((SqlPredicate.FuncLike)predicate).Function.Precedence, ((SqlPredicate.FuncLike)predicate).Function);
					break;

				case QueryElementType.SearchCondition:
					BuildSearchCondition(nullability, predicate.Precedence, (SqlSearchCondition)predicate, wrapCondition: false);
					break;

				case QueryElementType.NotExprPredicate:
					{
						var p = (SqlPredicate.NotExpr)predicate;

						if (p.IsNot)
							StringBuilder.Append("NOT ");

						BuildExpression(
							nullability,
							((SqlPredicate.NotExpr)predicate).IsNot
								? Precedence.LogicalNegation
								: GetPrecedence((SqlPredicate.NotExpr)predicate),
							((SqlPredicate.NotExpr)predicate).Expr1);
					}

					break;

				case QueryElementType.ExprPredicate:
					{
						var p = (SqlPredicate.Expr)predicate;

						if (p.Expr1 is SqlValue sqlValue)
						{
							var value = sqlValue.Value;

							if (value is bool b)
							{
								StringBuilder.Append(b ? "1 = 1" : "1 = 0");
								return;
							}
						}

						BuildExpression(nullability, GetPrecedence(p), p.Expr1);
					}

					break;
				default:
					throw new InvalidOperationException($"Unexpected predicate type {predicate.ElementType}");
			}
		}

		protected virtual void BuildExprExprPredicateOperator(SqlPredicate.ExprExpr expr)
		{
			switch (expr.Operator)
			{
				case SqlPredicate.Operator.Equal          : StringBuilder.Append(" = ");  break;
				case SqlPredicate.Operator.NotEqual       : StringBuilder.Append(" <> "); break;
				case SqlPredicate.Operator.Greater        : StringBuilder.Append(" > ");  break;
				case SqlPredicate.Operator.GreaterOrEqual : StringBuilder.Append(" >= "); break;
				case SqlPredicate.Operator.NotGreater     : StringBuilder.Append(" !> "); break;
				case SqlPredicate.Operator.Less           : StringBuilder.Append(" < ");  break;
				case SqlPredicate.Operator.LessOrEqual    : StringBuilder.Append(" <= "); break;
				case SqlPredicate.Operator.NotLess        : StringBuilder.Append(" !< "); break;
				case SqlPredicate.Operator.Overlaps       : StringBuilder.Append(" OVERLAPS "); break;
			}
			}

		protected virtual void BuildExprExprPredicate(NullabilityContext nullability, SqlPredicate.ExprExpr expr)
		{
			BuildExpression(nullability, GetPrecedence(expr), expr.Expr1);

			BuildExprExprPredicateOperator(expr);

			BuildExpression(nullability, GetPrecedence(expr), expr.Expr2);
		}

		protected virtual void BuildIsDistinctPredicate(NullabilityContext nullability, SqlPredicate.IsDistinct expr)
		{
			BuildExpression(nullability, GetPrecedence(expr), expr.Expr1);
			StringBuilder.Append(expr.IsNot ? " IS NOT DISTINCT FROM " : " IS DISTINCT FROM ");
			BuildExpression(nullability, GetPrecedence(expr), expr.Expr2);
		}

		protected void BuildIsDistinctPredicateFallback(NullabilityContext nullability, SqlPredicate.IsDistinct expr)
		{
			// This is the fallback implementation of IS DISTINCT FROM
			// for all providers that don't support the standard syntax
			// nor have a proprietary alternative
			expr.Expr1.ShouldCheckForNull(nullability);
			StringBuilder.Append("CASE WHEN ");
			BuildExpression(nullability, Precedence.Comparison, expr.Expr1);
			StringBuilder.Append(" = ");
			BuildExpression(nullability, Precedence.Comparison, expr.Expr2);
			StringBuilder.Append(" OR ");
			BuildExpression(nullability, Precedence.Comparison, expr.Expr1);
			StringBuilder.Append(" IS NULL AND ");
			BuildExpression(nullability, Precedence.Comparison, expr.Expr2);
			StringBuilder
				.Append(" IS NULL THEN 0 ELSE 1 END = ")
				.Append(expr.IsNot ? '0' : '1');
		}

		static SqlField GetUnderlayingField(ISqlExpression expr)
		{
			return expr.ElementType switch
			{
				QueryElementType.SqlField => (SqlField)expr,
				QueryElementType.Column	  => GetUnderlayingField(((SqlColumn)expr).Expression),
				_                         => throw new InvalidOperationException(),
			};
		}

		void BuildInListPredicate(NullabilityContext nullability, ISqlPredicate predicate)
		{
			var p      = (SqlPredicate.InList)predicate;
			var values = p.Values;

			// Handle x.In(IEnumerable variable)
			if (values.Count == 1 && values[0] is SqlParameter pr)
			{
				var prValue = pr.GetParameterValue(OptimizationContext.Context.ParameterValues).ProviderValue;
				switch (prValue)
				{
					case null:
						BuildPredicate(nullability, new SqlPredicate.Expr(new SqlValue(false)));
						return;
					// Be careful that string is IEnumerable, we don't want to handle x.In(string) here
					case string:
						break;
					case IEnumerable items:
						if (p.Expr1 is ISqlTableSource table)
							TableSourceIn(table, items);
						else
							InValues(items);
						return;
				}
			}

			// Handle x.In(val1, val2, val3)
			InValues(values);
			return;

			void TableSourceIn(ISqlTableSource table, IEnumerable items)
			{
				var keys = table.GetKeys(true);
				if (keys is null or { Count: 0 })
					throw new SqlException("Cannot create IN expression.");

				var firstValue = true;

				if (keys.Count == 1)
				{
					foreach (var item in items)
					{
						if (firstValue)
						{
							firstValue = false;
							BuildExpression(nullability, GetPrecedence(p), keys[0]);
							StringBuilder.Append(p.IsNot ? " NOT IN (" : " IN (");
						}

						var field = GetUnderlayingField(keys[0]);
						var value = field.ColumnDescriptor.MemberAccessor.GetValue(item!);

						if (value is ISqlExpression expression)
							BuildExpression(nullability, expression);
						else
							BuildValue(new SqlDataType(field), value);

						StringBuilder.Append(InlineComma);
					}
				}
				else
				{
					var len = StringBuilder.Length;
					var rem = 1;

					foreach (var item in items)
					{
						if (firstValue)
						{
							firstValue = false;
							StringBuilder.Append('(');
						}

						foreach (var key in keys)
						{
							var field = GetUnderlayingField(key);
							var value = field.ColumnDescriptor.MemberAccessor.GetValue(item!);

							BuildExpression(nullability, GetPrecedence(p), key);

							if (value == null)
							{
								StringBuilder.Append(" IS NULL");
							}
							else
							{
								StringBuilder.Append(" = ");
								BuildValue(new SqlDataType(field), value);
							}

							StringBuilder.Append(" AND ");
						}

						StringBuilder.Remove(StringBuilder.Length - 4, 4).Append("OR ");

						if (StringBuilder.Length - len >= 50)
						{
							StringBuilder.AppendLine();
							AppendIndent();
							StringBuilder.Append(' ');
							len = StringBuilder.Length;
							rem = 5 + Indent;
						}
					}

					if (!firstValue)
						StringBuilder.Remove(StringBuilder.Length - rem, rem);
				}

				if (firstValue)
					BuildPredicate(nullability, new SqlPredicate.Expr(new SqlValue(p.IsNot)));
				else
					StringBuilder.Remove(StringBuilder.Length - 2, 2).Append(')');
			}

			void InValues(IEnumerable values)
			{
				var firstValue    = true;
				var len           = StringBuilder.Length;
				var checkNull     = p.WithNull != null;
				var hasNull       = false;
				var count         = 0;
				var multipleParts = false;

				var sqlDataType = p.Expr1.ElementType switch
				{
					QueryElementType.SqlField => new SqlDataType((SqlField)p.Expr1),
					QueryElementType.SqlParameter => new SqlDataType(((SqlParameter)p.Expr1).Type),
					_ => null,
				};

				foreach (object? value in values)
				{
					if (++count > SqlProviderFlags.MaxInListValuesCount)
					{
						count       =  1;
					 	multipleParts = true;

						// start building next bucket
						firstValue = true;
						RemoveInlineComma()
							.Append(')')
							.Append(p.IsNot ? " AND " : " OR ");
					}

					object? val = value;

					if (checkNull)
					{
						if (val is ISqlExpression sqlExpr && sqlExpr.TryEvaluateExpression(OptimizationContext.Context, out var evaluated))
							val = evaluated;

						if (val == null)
						{
							hasNull = true;
							continue;
						}
					}

					if (firstValue)
					{
						firstValue = false;
						BuildExpression(nullability, GetPrecedence(p), p.Expr1);
						StringBuilder.Append(p.IsNot ? " NOT IN (" : " IN (");
					}

					if (value is ISqlExpression expression)
						BuildExpression(nullability, expression);
					else
						BuildValue(sqlDataType, value);

					StringBuilder.Append(InlineComma);
				}

				if (firstValue)
				{
					// Nothing was built, because the values contained only null values, or nothing at all.
					BuildPredicate(nullability, hasNull ?
						new SqlPredicate.IsNull(p.Expr1, p.IsNot) :
						new SqlPredicate.Expr(new SqlValue(p.IsNot)));
				}
				else
				{
					RemoveInlineComma().Append(')');

					if (hasNull)
					{
						StringBuilder.Append(p.IsNot ? " AND " : " OR ");
						BuildPredicate(nullability, new SqlPredicate.IsNull(p.Expr1, p.IsNot));
						multipleParts = true;
					}
					else if (p.WithNull == true && p.Expr1.ShouldCheckForNull(nullability))
					{
						StringBuilder.Append(" OR ");
	 					BuildPredicate(nullability, new SqlPredicate.IsNull(p.Expr1, false));
		 				multipleParts = true;
			 		}
				}

				if (multipleParts)
					StringBuilder.Insert(len, "(").Append(')');
			}
		}

		protected void BuildPredicate(NullabilityContext nullability, int parentPrecedence, int precedence, ISqlPredicate predicate)
		{
			var wrap = Wrap(precedence, parentPrecedence);

			if (wrap) StringBuilder.Append('(');
			BuildPredicate(nullability, predicate);
			if (wrap) StringBuilder.Append(')');
		}

		protected virtual void BuildLikePredicate(NullabilityContext nullability, SqlPredicate.Like predicate)
		{
			var precedence = GetPrecedence(predicate);

			BuildExpression(nullability, precedence, predicate.Expr1);
			StringBuilder
				.Append(predicate.IsNot ? " NOT " : " ")
				.Append(predicate.FunctionName ?? "LIKE")
				.Append(' ');
			BuildExpression(nullability, precedence, predicate.Expr2);

			if (predicate.Escape != null)
			{
				StringBuilder.Append(" ESCAPE ");
				BuildExpression(nullability, predicate.Escape);
			}
		}

		#endregion

		#region BuildExpression

		/// <summary>
		/// Used to disable field table name (alias) generation.
		/// </summary>
		protected virtual bool BuildFieldTableAlias(SqlField field) => true;

		protected virtual StringBuilder BuildExpression(
			NullabilityContext nullability, 
			ISqlExpression     expr,
			bool               buildTableName,
			bool               checkParentheses,
			string?            alias,
			ref bool           addAlias,
			bool               throwExceptionIfTableNotFound = true)
		{
			expr = ConvertElement(expr, nullability);

			switch (expr.ElementType)
			{
				case QueryElementType.SqlField:
					{
						var field = (SqlField)expr;

						if (BuildFieldTableAlias(field) && buildTableName && field.Table != null)
						{
							var ts = field.Table.SqlTableType == SqlTableType.SystemTable
								? field.Table
								: Statement.SelectQuery?.GetTableSource(field.Table);

							if (ts == null)
							{
								var current = Statement;

								do
								{
									ts = current.GetTableSource(field.Table);
									if (ts != null)
										break;
									current = current.ParentStatement;
								}
								while (current != null);
							}

							if (ts == null)
							{
								if (field != field.Table.All)
								{
#if DEBUG
									//SqlQuery.GetTableSource(field.Table);
#endif

									if (throwExceptionIfTableNotFound)
										throw new SqlException("Table '{0}' not found.", field.Table);
								}
							}
							else
							{
								var table = GetTableAlias(ts);
								var len   = StringBuilder.Length;

								if (table == null)
									StringBuilder.Append(GetPhysicalTableName(nullability, field.Table, null, true));
								else
									Convert(StringBuilder, table, ConvertType.NameToQueryTableAlias);

								if (len == StringBuilder.Length)
									throw new SqlException("Table {0} should have an alias.", field.Table);

								addAlias = alias != field.PhysicalName;

								StringBuilder
									.Append('.');
							}
						}

						if (field == field.Table?.All)
							StringBuilder.Append('*');
						else
							Convert(StringBuilder, field.PhysicalName, ConvertType.NameToQueryField);
					}

					break;

				case QueryElementType.Column:
					{
						var column = (SqlColumn)expr;

#if DEBUG
						var sql = Statement.SqlText;
#endif

						ISqlTableSource? table;
						var currentStatement = Statement;

						do
						{
							table = currentStatement.GetTableSource(column.Parent!);
							if (table != null)
								break;
							currentStatement = currentStatement.ParentStatement;
						}
						while (currentStatement != null);

						if (table == null)
						{
#if DEBUG
							table = Statement.GetTableSource(column.Parent!);
#endif

							throw new SqlException("Table not found for '{0}'.", column);
						}

						var tableAlias = GetTableAlias(table) ?? GetPhysicalTableName(nullability, column.Parent!, null, true);

						if (string.IsNullOrEmpty(tableAlias))
							throw new SqlException("Table {0} should have an alias.", column.Parent);

						addAlias = alias != column.Alias;

						Convert(StringBuilder, tableAlias, ConvertType.NameToQueryTableAlias);
						StringBuilder.Append('.');
						Convert(StringBuilder, column.Alias!, ConvertType.NameToQueryField);
					}

					break;

				case QueryElementType.SqlQuery:
					{
						var hasParentheses = checkParentheses && StringBuilder[StringBuilder.Length - 1] == '(';

						if (!hasParentheses)
							StringBuilder.AppendLine(OpenParens);
						else
							StringBuilder.AppendLine();

						BuildSqlBuilder(nullability, (SelectQuery)expr, Indent + 1, BuildStep != Step.FromClause);

						AppendIndent();

						if (!hasParentheses)
							StringBuilder.Append(')');
					}

					break;

				case QueryElementType.SqlValue:
					var sqlval = (SqlValue)expr;
					var dt     = new SqlDataType(sqlval.ValueType);

					BuildValue(dt, sqlval.Value);
					break;

				case QueryElementType.SqlExpression:
					{
						var e = (SqlExpression)expr;

						if (e.Expr == "{0}")
							BuildExpression(nullability, e.Parameters[0], buildTableName, checkParentheses, alias, ref addAlias, throwExceptionIfTableNotFound);
						else
							BuildFormatValues(nullability, e.Expr, e.Parameters, GetPrecedence(e));
					}

					break;

				case QueryElementType.SqlNullabilityExpression:
					BuildExpression(nullability, ((SqlNullabilityExpression)expr).SqlExpression, buildTableName, checkParentheses, alias, ref addAlias, throwExceptionIfTableNotFound);
					break;

				case QueryElementType.SqlAnchor:
					BuildAnchor(nullability, (SqlAnchor)expr);
					break;

				case QueryElementType.SqlBinaryExpression:
					BuildBinaryExpression(nullability, (SqlBinaryExpression)expr);
					break;

				case QueryElementType.SqlFunction:
					BuildFunction(nullability, (SqlFunction)expr);
					break;

				case QueryElementType.SqlParameter:
					{
						var parm     = (SqlParameter)expr;
						var inlining = !parm.IsQueryParameter;

						if (inlining)
						{
							var paramValue = parm.GetParameterValue(OptimizationContext.Context.ParameterValues);
							if (!TryConvertParameterToSql(paramValue))
								inlining = false;
						}

						if (!inlining)
						{
							var newParm = OptimizationContext.AddParameter(parm);
							BuildParameter(newParm);
						}

						break;
					}
						
				case QueryElementType.SqlDataType:
					BuildDataType((SqlDataType)expr, forCreateTable: false, canBeNull: true);
					break;

				case QueryElementType.SearchCondition:
					BuildSearchCondition(nullability, expr.Precedence, (SqlSearchCondition)expr, wrapCondition: false);
					break;

				case QueryElementType.SqlTable:
				case QueryElementType.SqlRawSqlTable:
				case QueryElementType.TableSource:
					{
						var table = (ISqlTableSource) expr;
						var tableAlias = GetTableAlias(table) ?? GetPhysicalTableName(nullability, table, null, true);
						StringBuilder.Append(tableAlias);
					}

					break;

				case QueryElementType.GroupingSet:
					{
						var groupingSet = (SqlGroupingSet) expr;
						StringBuilder.Append('(');
						for (var index = 0; index < groupingSet.Items.Count; index++)
						{
							var setItem = groupingSet.Items[index];
							BuildExpression(nullability, setItem, buildTableName, checkParentheses, throwExceptionIfTableNotFound);
							if (index < groupingSet.Items.Count - 1)
								StringBuilder.Append(InlineComma);
						}

						StringBuilder.Append(')');
					}

					break;

				case QueryElementType.SqlRow:
					BuildSqlRow(nullability, (SqlRow) expr, buildTableName, checkParentheses, throwExceptionIfTableNotFound);
					break;

				default:
					throw new InvalidOperationException($"Unexpected expression type {expr.ElementType}");
			}

			return StringBuilder;
		}

		protected virtual void BuildAnchor(NullabilityContext nullability, SqlAnchor anchor)
		{
			var addAlias = false;
			switch (anchor.AnchorKind)
			{
				case SqlAnchor.AnchorKindEnum.Deleted:
					StringBuilder.Append(DeletedOutputTable)
						.Append('.');
					break;
				case SqlAnchor.AnchorKindEnum.Inserted:
					StringBuilder.Append(InsertedOutputTable)
						.Append('.');
					break;
				default:
					throw new ArgumentOutOfRangeException(anchor.AnchorKind.ToString());
			}

			BuildExpression(nullability, anchor.SqlExpression, false, false, null, ref addAlias, false);
		}

		protected virtual bool TryConvertParameterToSql(SqlParameterValue paramValue)
		{
			return MappingSchema.TryConvertToSql(StringBuilder, new (paramValue.DbDataType), DataOptions, paramValue.ProviderValue);
		}

		protected virtual void BuildParameter(SqlParameter parameter)
		{
			Convert(StringBuilder, parameter.Name!, ConvertType.NameToQueryParameter);
		}

		void BuildFormatValues(NullabilityContext nullability, string format, IReadOnlyList<ISqlExpression>? parameters, int precedence)
		{
			if (parameters == null || parameters.Count == 0)
				StringBuilder.Append(format);
			else
			{
				var values = new object[parameters.Count];

				for (var i = 0; i < values.Length; i++)
				{
					var value = parameters[i];

					values[i] = WithStringBuilderBuildExpression(nullability, precedence, value);
				}

				StringBuilder.AppendFormat(format, values);
			}
		}

		string IdentText(string text, int ident)
		{
			if (string.IsNullOrEmpty(text))
				return text;

			text = text.Replace("\r", "");

			var strArray = text.Split('\n');
			using var sb = Pools.StringBuilder.Allocate();

			for (var i = 0; i < strArray.Length; i++)
			{
				var s = strArray[i];
				sb.Value.Append('\t', ident).Append(s);
				if (i < strArray.Length - 1)
					sb.Value.AppendLine();
			}

			return sb.Value.ToString();
		}

		void BuildExpression(NullabilityContext nullability, int parentPrecedence, ISqlExpression expr, string? alias, ref bool addAlias)
		{
			var wrap = Wrap(GetPrecedence(expr), parentPrecedence);

			if (wrap) StringBuilder.Append('(');
			BuildExpression(nullability, expr, true, true, alias, ref addAlias);
			if (wrap) StringBuilder.Append(')');
		}

		protected StringBuilder BuildExpression(NullabilityContext nullability,ISqlExpression expr)
		{
			var dummy = false;
			return BuildExpression(nullability, expr, true, true, null, ref dummy);
		}

		public void BuildExpression(NullabilityContext nullability, ISqlExpression expr, bool buildTableName, bool checkParentheses, bool throwExceptionIfTableNotFound = true)
		{
			var dummy = false;
			BuildExpression(nullability, expr, buildTableName, checkParentheses, null, ref dummy, throwExceptionIfTableNotFound);
		}

		protected void BuildExpression(NullabilityContext nullability, int precedence, ISqlExpression expr)
		{
			var dummy = false;
			BuildExpression(nullability, precedence, expr, null, ref dummy);
		}

		protected virtual void BuildTypedExpression(NullabilityContext nullability, SqlDataType dataType, ISqlExpression value)
		{
			StringBuilder.Append("CAST(");
			BuildExpression(nullability, value);
			StringBuilder.Append(" AS ");
			BuildDataType(dataType, false, value.CanBeNullable(nullability));
			StringBuilder.Append(')');
		}

		protected virtual void BuildSqlRow(NullabilityContext nullability, SqlRow expr, bool buildTableName, bool checkParentheses, bool throwExceptionIfTableNotFound)
		{
			StringBuilder.Append('(');
			foreach (var value in expr.Values)
			{
				BuildExpression(nullability, value, buildTableName, checkParentheses, throwExceptionIfTableNotFound);
				StringBuilder.Append(InlineComma);
			}
			StringBuilder.Length -= InlineComma.Length; // Note that SqlRow are never empty
			StringBuilder.Append(')');
		}

		protected object? BuildExpressionContext;

		void ISqlBuilder.BuildExpression(NullabilityContext nullability, StringBuilder sb, ISqlExpression expr, bool buildTableName, object? context = null)
		{
			WithStringBuilder(sb, static ctx => ctx.this_.BuildExpression(ctx.nullability, ctx.expr, ctx.buildTableName, true), (this_: this, nullability, expr, buildTableName));
			BuildExpressionContext = null;
		}

		#endregion

		#region BuildValue

		protected void BuildValue(SqlDataType? dataType, object? value)
		{
			if (value is Sql.SqlID id)
				TryBuildSqlID(id);
			else
				MappingSchema.ConvertToSqlValue(StringBuilder, dataType, DataOptions, value);
		}

		#endregion

		#region BuildBinaryExpression

		protected virtual void BuildBinaryExpression(NullabilityContext nullability, SqlBinaryExpression expr)
		{
			BuildBinaryExpression(nullability, expr.Operation, expr);
		}

		void BuildBinaryExpression(NullabilityContext nullability, string op, SqlBinaryExpression expr)
		{
			if (expr.Operation == "*" && expr.Expr1 is SqlValue value)
			{
				if (value.Value is int i && i == -1)
				{
					StringBuilder.Append('-');
					BuildExpression(nullability, GetPrecedence(expr), expr.Expr2);
					return;
				}
			}

			BuildExpression(nullability, GetPrecedence(expr), expr.Expr1);
			StringBuilder.Append(' ').Append(op).Append(' ');
			BuildExpression(nullability, GetPrecedence(expr), expr.Expr2);
		}

		#endregion

		#region BuildFunction

		protected virtual void BuildFunction(NullabilityContext nullability, SqlFunction func)
		{
			if (func.Name == "CASE")
			{
				StringBuilder.Append(func.Name).AppendLine();

				Indent++;

				var i = 0;

				for (; i < func.Parameters.Length - 1; i += 2)
				{
					AppendIndent().Append("WHEN ");

					var len = StringBuilder.Length;

					BuildExpression(nullability, func.Parameters[i]);

					if (SqlExpression.NeedsEqual(func.Parameters[i]))
					{
						StringBuilder.Append(" = ");
						BuildValue(null, true);
					}

					if (StringBuilder.Length - len > 20)
					{
						StringBuilder.AppendLine();
						AppendIndent().Append("\tTHEN ");
					}
					else
						StringBuilder.Append(" THEN ");

					BuildExpression(nullability, func.Parameters[i + 1]);
					StringBuilder.AppendLine();
				}

				if (i < func.Parameters.Length)
				{
					AppendIndent().Append("ELSE ");
					BuildExpression(nullability, func.Parameters[i]);
					StringBuilder.AppendLine();
				}

				Indent--;

				AppendIndent().Append("END");
			}
			else
			{
				BuildFunction(nullability, func.Name, func.Parameters);
			}
		}

		void BuildFunction(NullabilityContext nullability, string name, ISqlExpression[] exprs)
		{
			StringBuilder.Append(name).Append('(');

			var first = true;

			foreach (var parameter in exprs)
			{
				if (!first)
					StringBuilder.Append(InlineComma);

				BuildExpression(nullability, parameter, true, !first || name == "EXISTS");

				first = false;
			}

			StringBuilder.Append(')');
		}

		#endregion

		#region BuildDataType

		/// <summary>
		/// Appends an <see cref="SqlDataType"/>'s String to a provided <see cref="StringBuilder"/>
		/// </summary>
		/// <param name="sb"></param>
		/// <param name="dataType"></param>
		/// <returns>The stringbuilder with the type information appended.</returns>
		public StringBuilder BuildDataType(StringBuilder sb, SqlDataType dataType)
		{
			WithStringBuilder(sb, static ctx =>
			{
				ctx.this_.BuildDataType(ctx.dataType, forCreateTable: false, canBeNull: false);
			}, (this_: this, dataType));
			return sb;
		}

		/// <param name="canBeNull">Type could store <c>NULL</c> values (could be used for column table type generation or for databases with explicit typee nullability like ClickHouse).</param>
		protected void BuildDataType(SqlDataType type, bool forCreateTable, bool canBeNull)
		{
			if (!string.IsNullOrEmpty(type.Type.DbType))
				StringBuilder.Append(type.Type.DbType);
			else
			{
				var systemType = type.Type.SystemType.FullName;
				if (type.Type.DataType == DataType.Undefined)
					type = MappingSchema.GetDataType(type.Type.SystemType);

				if (!string.IsNullOrEmpty(type.Type.DbType))
				{
					StringBuilder.Append(type.Type.DbType);
					return;
				}

				if (type.Type.DataType == DataType.Undefined)
					// give some hint to user that it is expected situation and he need to fix something on his side
					throw new LinqToDBException($"Database column type cannot be determined automatically and must be specified explicitly for system type {systemType}");

				BuildDataTypeFromDataType(type, forCreateTable, canBeNull);
			}
		}

		/// <param name="canBeNull">Type could store <c>NULL</c> values (could be used for column table type generation or for databases with explicit typee nullability like ClickHouse).</param>
		protected virtual void BuildDataTypeFromDataType(SqlDataType type, bool forCreateTable, bool canBeNull)
		{
			switch (type.Type.DataType)
			{
				case DataType.Double : StringBuilder.Append("Float");    return;
				case DataType.Single : StringBuilder.Append("Real");     return;
				case DataType.SByte  : StringBuilder.Append("TinyInt");  return;
				case DataType.UInt16 : StringBuilder.Append("Int");      return;
				case DataType.UInt32 : StringBuilder.Append("BigInt");   return;
				case DataType.UInt64 : StringBuilder.Append("Decimal");  return;
				case DataType.Byte   : StringBuilder.Append("TinyInt");  return;
				case DataType.Int16  : StringBuilder.Append("SmallInt"); return;
				case DataType.Int32  : StringBuilder.Append("Int");      return;
				case DataType.Int64  : StringBuilder.Append("BigInt");   return;
				case DataType.Boolean: StringBuilder.Append("Bit");      return;
			}

			StringBuilder.Append(type.Type.DataType);

			if (type.Type.Length > 0)
				StringBuilder.Append('(').Append(type.Type.Length).Append(')');

			if (type.Type.Precision > 0)
				StringBuilder.Append('(').Append(type.Type.Precision).Append(InlineComma).Append(type.Type.Scale).Append(')');
		}

		#endregion

		#region GetPrecedence

		static int GetPrecedence(ISqlExpression expr)
		{
			return expr.Precedence;
		}

		protected static int GetPrecedence(ISqlPredicate predicate)
		{
			return predicate.Precedence;
		}

		#endregion

		#region Comments

		protected virtual void BuildTag(SqlStatement statement)
		{
			if (statement.Tag != null)
				BuildSqlComment(StringBuilder, statement.Tag);
		}

		protected virtual StringBuilder BuildSqlComment(StringBuilder sb, SqlComment comment)
		{
			sb.Append("/* ");

			for (var i = 0; i < comment.Lines.Count; i++)
			{
				sb.Append(comment.Lines[i].Replace("/*", "").Replace("*/", ""));
				if (i < comment.Lines.Count - 1)
					sb.AppendLine();
			}

			sb.AppendLine(" */");

			return sb;
		}

		#endregion

		#endregion

		#region Internal Types

		protected enum Step
		{
			WithClause,
			SelectClause,
			DeleteClause,
			AlterDeleteClause,
			UpdateClause,
			InsertClause,
			FromClause,
			WhereClause,
			GroupByClause,
			HavingClause,
			OrderByClause,
			OffsetLimit,
			Tag,
			Output,
			QueryExtensions
		}

		#endregion

		#region Alternative Builders

		protected IEnumerable<SqlColumn> AlternativeGetSelectedColumns(SelectQuery selectQuery, IEnumerable<SqlColumn> columns)
		{
			foreach (var col in columns)
				yield return col;

			SkipAlias = false;

			var obys = GetTempAliases(selectQuery.OrderBy.Items.Count, "oby");

			for (var i = 0; i < obys.Length; i++)
				yield return new SqlColumn(selectQuery, selectQuery.OrderBy.Items[i].Expression, obys[i]);
		}

		protected static bool IsDateDataType(ISqlExpression expr, string dateName)
		{
			return expr.ElementType switch
			{
				QueryElementType.SqlDataType   => ((SqlDataType)expr).Type.DataType == DataType.Date,
				QueryElementType.SqlExpression => ((SqlExpression)expr).Expr == dateName,
				_                              => false,
			};
		}

		protected static bool IsTimeDataType(ISqlExpression expr)
		{
			return expr.ElementType switch
			{
				QueryElementType.SqlDataType   => ((SqlDataType)expr).Type.DataType == DataType.Time,
				QueryElementType.SqlExpression => ((SqlExpression)expr).Expr == "Time",
				_                              => false,
			};
		}

		#endregion

		#region Helpers

		protected SequenceNameAttribute? GetSequenceNameAttribute(SqlTable table, bool throwException)
		{
			var identityField = table.GetIdentityField();

			if (identityField == null)
				if (throwException)
					throw new SqlException("Identity field must be defined for '{0}'.", table.NameForLogging);
				else
					return null;

			if (table.ObjectType == null)
				if (throwException)
					throw new SqlException("Sequence name can not be retrieved for the '{0}' table.", table.NameForLogging);
				else
					return null;

			var attrs = table.SequenceAttributes;

			if (attrs.IsNullOrEmpty())
				if (throwException)
					throw new SqlException("Sequence name can not be retrieved for the '{0}' table.", table.NameForLogging);
				else
					return null;

			return attrs[0];
		}

		static bool Wrap(int precedence, int parentPrecedence)
		{
			return
				precedence == 0 ||
				/* maybe it will be no harm to put "<=" here? */
				precedence < parentPrecedence ||
				(precedence == parentPrecedence &&
					(parentPrecedence == Precedence.Subtraction ||
					 parentPrecedence == Precedence.Multiplicative ||
					 parentPrecedence == Precedence.LogicalNegation));
		}

		protected string? GetTableAlias(ISqlTableSource table)
		{
			switch (table.ElementType)
			{
				case QueryElementType.TableSource:
					{
						var ts    = (SqlTableSource)table;
						var alias = string.IsNullOrEmpty(ts.Alias) ? GetTableAlias(ts.Source) : ts.Alias;
						return alias is not ("$" or "$F") ? alias : null;
					}
				case QueryElementType.SqlTable        :
				case QueryElementType.SqlCteTable     :
					{
						var alias = ((SqlTable)table).Alias;
						return alias is not ("$" or "$F") ? alias : null;
					}
				case QueryElementType.SqlRawSqlTable  :
					{
						var ts = Statement.SelectQuery?.GetTableSource(table) ?? Statement.GetTableSource(table);

						if (ts != null)
							return GetTableAlias(ts);

						var alias = ((SqlTable)table).Alias;
						return alias is not ("$" or "$F") ? alias : null;
					}
				case QueryElementType.SqlTableLikeSource:
					return null;

				default:
					throw new InvalidOperationException($"Unexpected table type {table.ElementType}");
			}
		}

		protected virtual string GetPhysicalTableName(NullabilityContext nullability, ISqlTableSource table, string? alias, bool ignoreTableExpression = false, string? defaultDatabaseName = null)
		{
			switch (table.ElementType)
			{
				case QueryElementType.SqlTable:
					{
						var tbl = (SqlTable)table;

						var tableName = tbl.TableName;
						if (tableName.Database == null && defaultDatabaseName != null)
							tableName = tableName with { Database = defaultDatabaseName };

						using var sb = Pools.StringBuilder.Allocate();

						BuildObjectName(sb.Value, tableName, tbl.SqlTableType == SqlTableType.Function ? ConvertType.NameToProcedure : ConvertType.NameToQueryTable, true, tbl.TableOptions);

						if (!ignoreTableExpression && tbl.SqlTableType == SqlTableType.Expression)
						{
							var values = new object[2 + (tbl.TableArguments?.Length ?? 0)];

							values[0] = sb.Value.ToString();

							if (alias != null)
								values[1] = ConvertInline(alias, ConvertType.NameToQueryTableAlias);
							else
								values[1] = "";

							for (var i = 2; i < values.Length; i++)
							{
								var value = tbl.TableArguments![i - 2];

								values[i] = WithStringBuilderBuildExpression(nullability, Precedence.Primary, value);
							}

							sb.Value.Length = 0;
							sb.Value.AppendFormat(tbl.Expression!, values);
						}
						else if (tbl.SqlTableType == SqlTableType.Function)
						{
							sb.Value.Append('(');

							if (tbl.TableArguments != null && tbl.TableArguments.Length > 0)
							{
								var first = true;

								foreach (var arg in tbl.TableArguments)
								{
									if (!first)
										sb.Value.Append(InlineComma);

									WithStringBuilder(sb.Value, static ctx => ctx.this_.BuildExpression(ctx.nullability, ctx.arg, true, !ctx.first), (this_: this, nullability, arg, first));

									first = false;
								}
							}

							sb.Value.Append(')');
						}

						return sb.Value.ToString();
					}

				case QueryElementType.TableSource:
					return GetPhysicalTableName(nullability, ((SqlTableSource)table).Source, alias);

				case QueryElementType.SqlCteTable   :
				case QueryElementType.SqlRawSqlTable:
					return BuildObjectName(new (), ((SqlTable)table).TableName, ConvertType.NameToQueryTable, true, TableOptions.NotSet).ToString();

				case QueryElementType.SqlTableLikeSource:
					return ConvertInline(((SqlTableLikeSource)table).Name, ConvertType.NameToQueryTable);

				default:
					throw new InvalidOperationException($"Unexpected table type {table.ElementType}");
			}
		}

		protected StringBuilder AppendIndent()
		{
			if (Indent > 0)
				StringBuilder.Append('\t', Indent);

			return StringBuilder;
		}

		protected virtual bool IsReserved(string word)
		{
			return ReservedWords.IsReserved(word);
		}

		#endregion

		#region ISqlProvider Members

		public virtual ISqlExpression? GetIdentityExpression(SqlTable table)
		{
			return null;
		}

		protected virtual void PrintParameterName(StringBuilder sb, DbParameter parameter)
		{
			if (!parameter.ParameterName.StartsWith("@"))
				sb.Append('@');
			sb.Append(parameter.ParameterName);
		}

		protected virtual string? GetTypeName(IDataContext dataContext, DbParameter parameter)
		{
			return null;
		}

		protected virtual string? GetUdtTypeName(IDataContext dataContext, DbParameter parameter)
		{
			return null;
		}

		protected virtual string? GetProviderTypeName(IDataContext dataContext, DbParameter parameter)
		{
			return parameter.DbType switch
			{
				DbType.AnsiString            => "VarChar",
				DbType.AnsiStringFixedLength => "Char",
				DbType.String                => "NVarChar",
				DbType.StringFixedLength     => "NChar",
				DbType.Decimal               => "Decimal",
				DbType.Binary                => "Binary",
				_                            => null,
			};
		}

		protected virtual void PrintParameterType(IDataContext dataContext, StringBuilder sb, DbParameter parameter)
		{
			var typeName = GetTypeName(dataContext, parameter);
			if (!string.IsNullOrEmpty(typeName))
				sb.Append(typeName).Append(" -- ");

			var udtTypeName = GetUdtTypeName(dataContext, parameter);
			if (!string.IsNullOrEmpty(udtTypeName))
				sb.Append(udtTypeName).Append(" -- ");

			var t1 = GetProviderTypeName(dataContext, parameter);
			var t2 = parameter.DbType.ToString();

			sb.Append(t1);

			if (t1 != null)
			{
				if (parameter.Size > 0)
				{
					if (t1.IndexOf('(') < 0)
						sb.Append('(').Append(parameter.Size).Append(')');
				}
#if NET45
#pragma warning disable RS0030 // API missing from DbParameter in NET 4.5
				else if (((IDbDataParameter)parameter).Precision > 0)
				{
					if (t1.IndexOf('(') < 0)
						sb.Append('(').Append(((IDbDataParameter)parameter).Precision).Append(InlineComma).Append(((IDbDataParameter)parameter).Scale).Append(')');
				}
#pragma warning restore RS0030 // API missing from DbParameter in NET 4.5
#else
				else if (parameter.Precision > 0)
				{
					if (t1.IndexOf('(') < 0)
						sb.Append('(').Append(parameter.Precision).Append(InlineComma).Append(parameter.Scale).Append(')');
				}
#endif
				else
				{
					switch (parameter.DbType)
					{
						case DbType.AnsiString           :
						case DbType.AnsiStringFixedLength:
						case DbType.String               :
						case DbType.StringFixedLength    :
							{
								var value = parameter.Value as string;

								if (!string.IsNullOrEmpty(value))
									sb.Append('(').Append(value!.Length).Append(')');

								break;
							}
						case DbType.Decimal:
							{
								var value = parameter.Value;

								if (value is decimal dec)
								{
									var d = new SqlDecimal(dec);
									sb.Append('(').Append(d.Precision).Append(InlineComma).Append(d.Scale).Append(')');
								}

								break;
							}
						case DbType.Binary:
							{
								if (parameter.Value is byte[] value)
									sb.Append('(').Append(value.Length).Append(')');

								break;
							}
					}
				}
			}

			if (t1 != t2)
				sb.Append(" -- ").Append(t2);
		}

		public virtual StringBuilder PrintParameters(IDataContext dataContext, StringBuilder sb, IEnumerable<DbParameter>? parameters)
		{
			if (parameters != null)
			{
				foreach (var p in parameters)
				{
					sb.Append("DECLARE ");
					PrintParameterName(sb, p);
					sb.Append(' ');
					PrintParameterType(dataContext, sb, p);
					sb.AppendLine();

					sb.Append("SET     ");
					PrintParameterName(sb, p);
					sb.Append(" = ");
					if (p.Value is byte[] bytes                           &&
					    Configuration.MaxBinaryParameterLengthLogging >= 0 &&
					    bytes.Length > Configuration.MaxBinaryParameterLengthLogging &&
					    MappingSchema.ValueToSqlConverter.CanConvert(typeof(byte[])))
					{
						var trimmed =
							new byte[Configuration.MaxBinaryParameterLengthLogging];
						Array.Copy(bytes, 0, trimmed, 0,
							Configuration.MaxBinaryParameterLengthLogging);
						MappingSchema.ValueToSqlConverter.TryConvert(sb, MappingSchema, DataOptions, trimmed);
						sb.AppendLine();
						sb.Append(
							$"-- value above truncated for logging, actual length is {bytes.Length}");
					}
					else if (p.Value is Binary binaryData &&
					         Configuration.MaxBinaryParameterLengthLogging >= 0 &&
					         binaryData.Length > Configuration.MaxBinaryParameterLengthLogging &&
					         MappingSchema.ValueToSqlConverter.CanConvert(typeof(Binary)))
					{
						//We aren't going to create a new Binary here,
						//since ValueToSql always just .ToArray() anyway
						var trimmed =
							new byte[Configuration.MaxBinaryParameterLengthLogging];
						Array.Copy(binaryData.ToArray(), 0, trimmed, 0,
							Configuration.MaxBinaryParameterLengthLogging);
						MappingSchema.TryConvertToSql(sb, null, DataOptions, trimmed);
						sb.AppendLine();
						sb.Append(
							$"-- value above truncated for logging, actual length is {binaryData.Length}");
					}
					else if (p.Value is string s &&
					         Configuration.MaxStringParameterLengthLogging >= 0 &&
					         s.Length > Configuration.MaxStringParameterLengthLogging &&
					         MappingSchema.ValueToSqlConverter.CanConvert(typeof(string)))
					{
						var trimmed =
							s.Substring(0,
								Configuration.MaxStringParameterLengthLogging);
						MappingSchema.TryConvertToSql(sb, null, DataOptions, trimmed);
						sb.AppendLine();
						sb.Append(
							$"-- value above truncated for logging, actual length is {s.Length}");
					}
					else if (!MappingSchema.TryConvertToSql(sb, null, DataOptions, p.Value))
						FormatParameterValue(sb, p.Value);
					sb.AppendLine();
				}

				sb.AppendLine();
			}

			return sb;
		}

		// for values without literal support from provider we should generate debug string using fixed format
		// to avoid deviations on different locales or locale settings
		private static void FormatParameterValue(StringBuilder sb, object? value)
		{
			if (value is DateTime dt)
			{
				// ISO8601 format (with Kind-specific offset part)
				sb
					.Append('\'')
					.Append(dt.ToString("o"))
					.Append('\'');
			}
			else if (value is DateTimeOffset dto)
			{
				// ISO8601 format with offset
				sb
					.Append('\'')
					.Append(dto.ToString("o"))
					.Append('\'');
			}
			else
				sb.Append(value);
		}

		public string ApplyQueryHints(string sqlText, IReadOnlyCollection<string> queryHints)
		{
			using var sb = Pools.StringBuilder.Allocate();

			foreach (var hint in queryHints)
				if (hint?.Length >= 2 && hint.StartsWith("**"))
					sb.Value.AppendLine(hint.Substring(2));

			sb.Value.Append(sqlText);

			foreach (var hint in queryHints)
				if (!(hint?.Length >= 2 && hint.StartsWith("**")))
					sb.Value.AppendLine(hint);

			return sb.Value.ToString();
		}

		public virtual string GetReserveSequenceValuesSql(int count, string sequenceName)
		{
			throw new NotImplementedException();
		}

		public virtual string GetMaxValueSql(EntityDescriptor entity, ColumnDescriptor column)
		{
			using var sb = Pools.StringBuilder.Allocate();
			sb.Value.Append("SELECT Max(");

			Convert(sb.Value, column.ColumnName, ConvertType.NameToQueryField)
				.Append(") FROM ");

			return BuildObjectName(sb.Value, entity.Name, ConvertType.NameToQueryTable, true, entity.TableOptions).ToString();
		}

		private string? _name;

		public virtual string Name => _name ??= GetType().Name.Replace("SqlBuilder", "");

		#endregion

		#region Aliases

		HashSet<string>? _aliases;

		public void RemoveAlias(string alias)
		{
			_aliases?.Remove(alias);
		}

		string GetAlias(string desiredAlias, string defaultAlias)
		{
			_aliases ??= OptimizationContext.Aliases.GetUsedTableAliases();

			var alias = desiredAlias;

			if (string.IsNullOrEmpty(desiredAlias) || desiredAlias.Length > 25)
			{
				desiredAlias = defaultAlias;
				alias        = defaultAlias + "1";
			}

			for (var i = 1; ; i++)
			{
				if (!_aliases.Contains(alias) && !IsReserved(alias))
				{
					_aliases.Add(alias);
					break;
				}

				alias = desiredAlias + i;
			}

			return alias;
		}

		public string[] GetTempAliases(int n, string defaultAlias)
		{
			var aliases = new string[n];

			for (var i = 0; i < aliases.Length; i++)
				aliases[i] = GetAlias(defaultAlias, defaultAlias);

			foreach (var t in aliases)
				RemoveAlias(t);

			return aliases;
		}

		#endregion

		#region BuildQueryExtensions

		protected virtual void BuildQueryExtensions(NullabilityContext nullability, SqlStatement statement)
		{
		}

		#endregion

		#region TableID

		public Dictionary<string,TableIDInfo>? TableIDs  { get; set; }
		public string?                         TablePath { get; set; }
		public string?                         QueryName { get; set; }

		public string BuildSqlID(Sql.SqlID id)
		{
			if (TableIDs?.TryGetValue(id.ID, out var path) == true)
				return id.Type switch
				{
					Sql.SqlIDType.TableAlias => path!.TableAlias,
					Sql.SqlIDType.TableName  => path!.TableName,
					Sql.SqlIDType.TableSpec  => path!.TableSpec,
					_ => throw new InvalidOperationException($"Unknown SqlID Type '{id.Type}'.")
				};

			throw new InvalidOperationException($"Table ID '{id.ID}' is not defined.");
		}

		int _testReplaceNumber;

		void TryBuildSqlID(Sql.SqlID id)
		{
			if (TableIDs?.ContainsKey(id.ID) == true)
			{
				StringBuilder.Append(BuildSqlID(id));
			}
			else
			{
				var testToReplace = $"$$${++_testReplaceNumber}$$$";

				StringBuilder.Append(testToReplace);

				(_finalBuilders ??= new(1)).Add(() => StringBuilder.Replace(testToReplace, BuildSqlID(id)));
			}
		}

		#endregion
	}
}
