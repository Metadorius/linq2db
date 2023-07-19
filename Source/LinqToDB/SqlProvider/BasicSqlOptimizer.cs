﻿using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable InconsistentNaming

namespace LinqToDB.SqlProvider
{
	using Common;
	using Expressions;
	using Extensions;
	using Linq;
	using Mapping;
	using SqlQuery;
	using SqlQuery.Visitors;

	public class BasicSqlOptimizer : ISqlOptimizer
	{
		#region Init

		protected BasicSqlOptimizer(SqlProviderFlags sqlProviderFlags)
		{
			SqlProviderFlags = sqlProviderFlags;
		}

		public SqlProviderFlags SqlProviderFlags { get; }

		#endregion

		#region ISqlOptimizer Members

		public virtual SqlExpressionOptimizerVisitor CreateOptimizerVisitor(bool allowModify)
		{
			return new SqlExpressionOptimizerVisitor(allowModify);
		}

		public virtual SqlExpressionConvertVisitor CreateConvertVisitor(bool allowModify)
		{
			return new SqlExpressionConvertVisitor(allowModify);
		}

		public virtual SqlStatement Finalize(MappingSchema mappingSchema, SqlStatement statement, DataOptions dataOptions)
		{
			FixRootSelect (statement);
			FixEmptySelect(statement);
			FinalizeCte   (statement);

			var evaluationContext = new EvaluationContext(null);

			using var visitor = QueryHelper.SelectOptimizer.Allocate();

#if DEBUG
			// ReSharper disable once NotAccessedVariable
			var sqlText = statement.SqlText;
#endif

			statement = (SqlStatement)visitor.Value.OptimizeQueries(statement, SqlProviderFlags, dataOptions,
				evaluationContext, statement, 0);

#if DEBUG
			// ReSharper disable once NotAccessedVariable
			var newSqlText = statement.SqlText;
#endif

//statement.EnsureFindTables();

			//statement.EnsureFindTables();

            if (dataOptions.LinqOptions.OptimizeJoins)
			{
				OptimizeJoins(statement);

				// Do it again after JOIN Optimization
				FinalizeCte(statement);
			}

			statement = FinalizeInsert(statement);
			statement = FinalizeSelect(statement);
			statement = CorrectUnionOrderBy(statement);
			statement = FixSetOperationNulls(statement);
			statement = OptimizeUpdateSubqueries(statement, dataOptions);

			// provider specific query correction
			statement = FinalizeStatement(statement, evaluationContext, dataOptions);

//statement.EnsureFindTables();

			return statement;
		}

		#endregion

		protected virtual SqlStatement FinalizeInsert(SqlStatement statement)
		{
			if (statement is SqlInsertStatement insertStatement)
			{
				var isSelfInsert =
					insertStatement.SelectQuery.From.Tables.Count     == 1 &&
					insertStatement.SelectQuery.From.Tables[0].Source == insertStatement.Insert.Into;

				if (isSelfInsert)
				{
					if (insertStatement.SelectQuery.IsSimple)
					{
						// simplify insert
						//
						insertStatement.Insert.Items.ForEach(item =>
						{
							if (item.Expression is SqlColumn column)
								item.Expression = column.Expression;
						});
						insertStatement.SelectQuery.From.Tables.Clear();
					}
				}
			}

			return statement;
		}

		internal static (SqlTableSource? tableSource, List<IQueryElement>? queryPath) FindTableSource(Stack<IQueryElement> currentPath, SqlTableSource source, SqlTable table)
		{
			if (source.Source == table)
				return (source, currentPath.ToList());

			if (source.Source is SelectQuery selectQuery)
			{
				var result = FindTableSource(currentPath, selectQuery, table);
				if (result.tableSource != null)
					return result;
			}

			foreach (var join in source.Joins)
			{
				currentPath.Push(join);
				var result = FindTableSource(currentPath, join.Table, table);
				currentPath.Pop();
				if (result.tableSource != null)
				{
					return result;
				}
			}

			return default;
		}

		internal static (SqlTableSource? tableSource, List<IQueryElement>? queryPath) FindTableSource(Stack<IQueryElement> currentPath, SelectQuery selectQuery, SqlTable table)
		{
			currentPath.Push(selectQuery);
			foreach (var source in selectQuery.From.Tables)
			{
				var result = FindTableSource(currentPath, source, table);
				if (result.tableSource != null)
					return result;
			}
			currentPath.Pop();

			return default;
		}

		static bool IsCompatibleForUpdate(SelectQuery selectQuery)
		{
			return !selectQuery.Select.IsDistinct && selectQuery.Select.GroupBy.IsEmpty;
		}

		static bool IsCompatibleForUpdate(SqlJoinedTable joinedTable)
		{
			return joinedTable.JoinType == JoinType.Inner || joinedTable.JoinType == JoinType.Left ||
			       joinedTable.JoinType == JoinType.Right;
		}

		public static bool IsCompatibleForUpdate(List<IQueryElement> path)
		{
			if (path.Count > 2)
				return false;

			var result = path.All(e =>
			{
				if (e is SelectQuery sc)
					return IsCompatibleForUpdate(sc);
				if (e is SqlJoinedTable jt)
					return IsCompatibleForUpdate(jt);
				return true;
			});

			return result;
		}

		public static bool IsCompatibleForUpdate(SelectQuery query, SqlTable updateTable, int level = 0)
		{
			if (!IsCompatibleForUpdate(query))
				return false;

			foreach (var ts in query.From.Tables)
			{
				if (ts.Source == updateTable)
					return true;

				foreach (var join in ts.Joins)
				{
					if (join.Table.Source == updateTable)
					{
						return IsCompatibleForUpdate(join);
					}
					
					if (IsCompatibleForUpdate(join) && join.Table.Source is SelectQuery sc)
					{
						if (IsCompatibleForUpdate(sc, updateTable))
							return true;
					}
				}
			}
			
			return false;
		}

		public static ISqlExpression? PopulateNesting(List<SelectQuery> queryPath, ISqlExpression expression, int ignoreCount)
		{
			var current = expression;
			for (var index = 0; index < queryPath.Count - ignoreCount; index++)
			{
				var selectQuery = queryPath[index];
				var idx         = selectQuery.Select.Columns.FindIndex(c => c.Expression == current);
				if (idx < 0)
				{
					if (selectQuery.Select.IsDistinct || !selectQuery.GroupBy.IsEmpty)
						return null;

					current = selectQuery.Select.AddNewColumn(current);
				}
				else
					current = selectQuery.Select.Columns[idx];
			}

			return current;
		}

		protected static void ApplyUpdateTableComparison(SelectQuery updateQuery, SqlUpdateClause updateClause, SqlTable inQueryTable, DataOptions dataOptions)
		{
			var compareKeys = inQueryTable.GetKeys(true);
			var tableKeys   = updateClause.Table!.GetKeys(true);

			var found = false;
			updateQuery.Where.EnsureConjunction();
			for (var i = 0; i < tableKeys.Count; i++)
			{
				var tableKey = tableKeys[i];

				var column = QueryHelper.NeedColumnForExpression(updateQuery, compareKeys[i], false);
				if (column == null)
					throw new LinqToDBException(
						$"Can not create query column for expression '{compareKeys[i]}'.");

				found = true;
				var compare       = QueryHelper.GenerateEquality(tableKey, column, dataOptions.LinqOptions.CompareNullsAsValues);
				updateQuery.Where.SearchCondition.Conditions.Add(compare);
			}

			if (!found)
				throw new LinqToDBException("Could not generate update statement.");
		}

		protected virtual SqlUpdateStatement BasicCorrectUpdate(SqlUpdateStatement statement, DataOptions dataOptions, bool wrapForOutput)
		{
			if (statement.Update.Table != null)
			{
				var (tableSource, queryPath) = FindTableSource(new Stack<IQueryElement>(), statement.SelectQuery, statement.Update.Table);

				if (tableSource != null && queryPath != null)
				{
					statement.Update.TableSource = tableSource;

					var forceWrapping = wrapForOutput && statement.Output != null &&
					                    (statement.SelectQuery.From.Tables.Count != 1 ||
					                     statement.SelectQuery.From.Tables.Count          == 1 &&
					                     statement.SelectQuery.From.Tables[0].Joins.Count == 0);
					
					if (forceWrapping || !IsCompatibleForUpdate(queryPath))
					{
						// we have to create new Update table and join via Keys

						var queries = queryPath.OfType<SelectQuery>().ToList();
						var keys    = statement.Update.Table.GetKeys(true).ToArray();

						if (keys.Length == 0)
						{
							keys = queries[0].Select.Columns
								.Where(c => c.Expression is SqlField field && field.Table == statement.Update.Table)
								.Select(c => (SqlField)c.Expression)
								.ToArray();
						}

						if (keys.Length == 0)
						{
							throw new LinqToDBException("Invalid update query.");
						}

						var keysColumns = new List<ISqlExpression>(keys.Length);
						foreach(var key in keys)
						{
							var newColumn = PopulateNesting(queries, key, 1);
							if (newColumn == null)
							{
								throw new LinqToDBException("Invalid update query. Could not create comparision key. It can be GROUP BY or DISTINCT query modifier.");
							}

							keysColumns.Add(newColumn);
						}

						var originalTableForUpdate = statement.Update.Table;
						var newTable = CloneTable(originalTableForUpdate, out var objectMap);

						var sc    = new SqlSearchCondition();

						for (var index = 0; index < keys.Length; index++)
						{
							var originalField = keys[index];

							if (!objectMap.TryGetValue(originalField, out var newField))
							{
								throw new InvalidOperationException();
							}

							var originalColumn = keysColumns[index];

							sc.Conditions.Add(QueryHelper.GenerateEquality((ISqlExpression)newField, originalColumn, dataOptions.LinqOptions.CompareNullsAsValues));
						}

						if (!SqlProviderFlags.IsUpdateFromSupported)
						{
							// build join
							//

							var tsIndex = statement.SelectQuery.From.Tables.FindIndex(ts =>
								queries.Contains(ts.Source));

							if (tsIndex < 0)
								throw new InvalidOperationException();

							var ts   = statement.SelectQuery.From.Tables[tsIndex];
							var join = new SqlJoinedTable(JoinType.Inner, ts, false, sc);

							statement.SelectQuery.From.Tables.RemoveAt(tsIndex);
							statement.SelectQuery.From.Tables.Insert(0, new SqlTableSource(newTable, "t", join));
						}
						else
						{
							statement.SelectQuery.Where.EnsureConjunction().ConcatSearchCondition(sc);
						}

						for (var index = 0; index < statement.Update.Items.Count; index++)
						{
							var item = statement.Update.Items[index];
							if (item.Column is SqlColumn column)
								item.Column = QueryHelper.GetUnderlyingField(column.Expression) ?? column.Expression;

							item = item.ConvertAll(this, (v, e) =>
							{
								if (objectMap.TryGetValue(e, out var newValue))
								{
									return newValue;
								}

								return e;
							});

							statement.Update.Items[index] = item;
						}

						statement.Update.Table       = newTable;
						statement.Update.TableSource = null;
					}
					else
					{
						if (queryPath.Count > 0)
						{
							var ts = statement.SelectQuery.From.Tables.FirstOrDefault();
							if (ts != null)
							{
								if (ts.Source is SelectQuery)
									statement.Update.TableSource = ts;
							}
						}
					}

					CorrectUpdateSetters(statement);
				}
			}

			return statement;
		}

		protected virtual SqlStatement FinalizeUpdate(SqlStatement statement, DataOptions dataOptions)
		{
			if (statement is SqlUpdateStatement updateStatement)
			{
				// get from columns expression
				//
				updateStatement.Update.Items.ForEach(item =>
				{
					item.Expression = QueryHelper.SimplifyColumnExpression(item.Expression);
				});

			}

			return statement;
		}

		protected virtual SqlStatement FinalizeSelect(SqlStatement statement)
		{
			if (statement is SqlSelectStatement selectStatement)
			{
				// When selecting a SqlRow, expand the row into individual columns.

				var selectQuery = selectStatement.SelectQuery;
				var columns     = selectQuery.Select.Columns;
	
				for (var i = 0; i < columns.Count; i++)
				{
					var c = columns[i];
					if (c.Expression.ElementType == QueryElementType.SqlRow)
					{
						if (columns.Count > 1)
							throw new LinqToDBException("SqlRow expression must be the only result in a SELECT");
	
						var row = (SqlRow)columns[0].Expression;
						columns.Clear();
						foreach (var value in row.Values)
							selectQuery.Select.AddNew(value);
	
						break;
					}
				}
			}

			return statement;
		}

		protected virtual SqlStatement CorrectUnionOrderBy(SqlStatement statement)
		{
			var queriesToWrap = new HashSet<SelectQuery>();

			statement.Visit(queriesToWrap, (wrap, e) =>
			{
				if (e is SelectQuery sc && sc.HasSetOperators)
				{
					var prevQuery = sc;

					for (int i = 0; i < sc.SetOperators.Count; i++)
					{
						var currentOperator = sc.SetOperators[i];
						var currentQuery    = currentOperator.SelectQuery;

						if (currentOperator.Operation == SetOperation.Union)
						{
							if (!prevQuery.Select.HasModifier && !prevQuery.OrderBy.IsEmpty)
							{
								prevQuery.OrderBy.Items.Clear();
							}

							if (!currentQuery.Select.HasModifier && !currentQuery.OrderBy.IsEmpty)
							{
								currentQuery.OrderBy.Items.Clear();
							}
						}
						else
						{
							if (!prevQuery.OrderBy.IsEmpty)
							{
								wrap.Add(prevQuery);
							}

							if (!currentQuery.OrderBy.IsEmpty)
							{
								wrap.Add(currentQuery);
							}
						}

						prevQuery = currentOperator.SelectQuery;
					}
				}
			});

			if (queriesToWrap.Count == 0)
				return statement;

			return QueryHelper.WrapQuery(
				queriesToWrap,
				statement,
				static (wrap, q, parentElement) => wrap.Contains(q),
				null,
				allowMutation: true,
				withStack: true);
		}

		static void CorrelateNullValueTypes(ref ISqlExpression toCorrect, ISqlExpression reference)
		{
			if (toCorrect.ElementType == QueryElementType.Column)
			{
				var column     = (SqlColumn)toCorrect;
				var columnExpr = column.Expression;
				CorrelateNullValueTypes(ref columnExpr, reference);
				column.Expression = columnExpr;
			}
			else if (toCorrect.ElementType == QueryElementType.SqlValue)
			{
				var value = (SqlValue)toCorrect;
				if (value.Value == null)
				{
					var suggested = QueryHelper.SuggestDbDataType(reference);
					if (suggested != null)
					{
						toCorrect = new SqlValue(suggested.Value, null);
					}
				}
			}
		}

		protected virtual SqlStatement FixSetOperationNulls(SqlStatement statement)
		{
			statement.VisitParentFirst(static e =>
			{
				if (e.ElementType == QueryElementType.SqlQuery)
				{
					var query = (SelectQuery)e;
					if (query.HasSetOperators)
					{
						for (var i = 0; i < query.Select.Columns.Count; i++)
						{
							var column     = query.Select.Columns[i];
							var columnExpr = column.Expression;

							foreach (var setOperator in query.SetOperators)
							{
								var otherColumn = setOperator.SelectQuery.Select.Columns[i];
								var otherExpr   = otherColumn.Expression;

								CorrelateNullValueTypes(ref columnExpr, otherExpr);
								CorrelateNullValueTypes(ref otherExpr, columnExpr);

								otherColumn.Expression = otherExpr;
							}

							column.Expression = columnExpr;
						}
					}
				}

				return true;
			});

			return statement;
		}

		bool FixRootSelect(SqlStatement statement)
		{
			if (statement.SelectQuery is {} query         &&
				query.Select.HasModifier == false         &&
				query.DoNotRemove        == false         &&
				query.QueryName is null                   &&
				query.Where.  IsEmpty                     &&
				query.GroupBy.IsEmpty                     &&
				query.Having. IsEmpty                     &&
				query.OrderBy.IsEmpty                     &&
				query.From.Tables is { Count : 1 } tables &&
				tables[0].Source  is SelectQuery   child  &&
				tables[0].Joins.Count      == 0           &&
				child.DoNotRemove          == false       &&
				query.Select.Columns.Count == child.Select.Columns.Count)
			{
				for (var i = 0; i < query.Select.Columns.Count; i++)
				{
					var pc = query.Select.Columns[i];
					var cc = child.Select.Columns[i];

					if (pc.Expression != cc)
						return false;
				}

				if (statement is SqlSelectStatement)
				{
					if (statement.SelectQuery.SqlQueryExtensions != null)
						(child.SqlQueryExtensions ??= new()).AddRange(statement.SelectQuery.SqlQueryExtensions);
					statement.SelectQuery = child;
				}
				else
				{
					var dic = new Dictionary<ISqlExpression,ISqlExpression>(query.Select.Columns.Count + 1)
					{
						{ statement.SelectQuery, child }
					};

					foreach (var pc in query.Select.Columns)
						dic.Add(pc, pc.Expression);

					statement.Walk(WalkOptions.Default, dic, static (d, ex) => d.TryGetValue(ex, out var e) ? e : ex);
				}

				return true;
			}

			return false;
		}

		//TODO: move tis to standard optimizer
		protected virtual SqlStatement OptimizeUpdateSubqueries(SqlStatement statement, DataOptions dataOptions)
		{
			/*
			if (statement is SqlUpdateStatement updateStatement)
			{
				var evaluationContext = new EvaluationContext();
				foreach (var setItem in updateStatement.Update.Items) 
				{
					if (setItem.Expression is SelectQuery q)
					{
						var optimizer = new SelectQueryOptimizer(SqlProviderFlags, q, q, 0);
						optimizer.FinalizeAndValidate(SqlProviderFlags.IsApplyJoinSupported);
					}
				}
			}
			*/

			return statement;
		}

		protected virtual void FixEmptySelect(SqlStatement statement)
		{
			// avoid SELECT * top level queries, as they could create a lot of unwanted traffic
			// and such queries are not supported by remote context
			if (statement.QueryType == QueryType.Select && statement.SelectQuery!.Select.Columns.Count == 0)
				statement.SelectQuery!.Select.Add(new SqlValue(1));
		}

		/// <summary>
		/// Used for correcting statement and should return new statement if changes were made.
		/// </summary>
		/// <param name="statement"></param>
		/// <param name="dataOptions"></param>
		/// <returns></returns>
		public virtual SqlStatement TransformStatement(SqlStatement statement, DataOptions dataOptions)
		{
			return statement;
		}

		static void RegisterDependency(CteClause cteClause, Dictionary<CteClause, HashSet<CteClause>> foundCte)
		{
			if (foundCte.ContainsKey(cteClause))
				return;

			var dependsOn = new HashSet<CteClause>();
			cteClause.Body!.Visit(dependsOn, static (dependsOn, ce) =>
			{
				if (ce.ElementType == QueryElementType.SqlCteTable)
				{
					var subCte = ((SqlCteTable)ce).Cte!;
					dependsOn.Add(subCte);
				}

			});

			foundCte.Add(cteClause, dependsOn);

			foreach (var clause in dependsOn)
			{
				RegisterDependency(clause, foundCte);
			}
		}

		void FinalizeCte(SqlStatement statement)
		{
			if (statement is SqlStatementWithQueryBase select)
			{
				// one-field class is cheaper than dictionary instance
				var cteHolder = new WritableContext<Dictionary<CteClause, HashSet<CteClause>>?>();

				if (select is SqlMergeStatement merge)
				{
					merge.Target.Visit(cteHolder, static (foundCte, e) =>
						{
							if (e.ElementType == QueryElementType.SqlCteTable)
							{
								var cte = ((SqlCteTable)e).Cte!;
								RegisterDependency(cte, foundCte.WriteableValue ??= new());
							}
						}
					);
					merge.Source.Visit(cteHolder, static (foundCte, e) =>
						{
							if (e.ElementType == QueryElementType.SqlCteTable)
							{
								var cte = ((SqlCteTable)e).Cte!;
								RegisterDependency(cte, foundCte.WriteableValue ??= new());
							}
						}
					);
				}
				else
				{
					select.SelectQuery.Visit(cteHolder, static (foundCte, e) =>
						{
							if (e.ElementType == QueryElementType.SqlCteTable)
							{
								var cte = ((SqlCteTable)e).Cte!;
								CorrectEmptyCte(cte);
								RegisterDependency(cte, foundCte.WriteableValue ??= new());
							}
						}
					);
				}

				if (cteHolder.WriteableValue == null || cteHolder.WriteableValue.Count == 0)
					select.With = null;
				else
				{
					// TODO: Ideally if there is no recursive CTEs we can convert them to SubQueries
					if (!SqlProviderFlags.IsCommonTableExpressionsSupported)
						throw new LinqToDBException("DataProvider do not supports Common Table Expressions.");

					// basic detection of non-recursive CTEs
					// for more complex cases we will need dependency cycles detection
					foreach (var kvp in cteHolder.WriteableValue)
					{
						if (kvp.Value.Count == 0)
							kvp.Key.IsRecursive = false;

						// remove self-reference for topo-sort
						kvp.Value.Remove(kvp.Key);
					}

					var ordered = TopoSorting.TopoSort(cteHolder.WriteableValue.Keys, cteHolder, static (cteHolder, i) => cteHolder.WriteableValue![i]).ToList();

					Utils.MakeUniqueNames(ordered, null, static (n, a) => !ReservedWords.IsReserved(n), static c => c.Name, static (c, n, a) => c.Name = n,
						static c => string.IsNullOrEmpty(c.Name) ? "CTE_1" : c.Name, StringComparer.OrdinalIgnoreCase);

					select.With = new SqlWithClause();
					select.With.Clauses.AddRange(ordered);
				}
			}
		}

		static void CorrectEmptyCte(CteClause cte)
		{
			/*if (cte.Fields.Count == 0)
			{
				cte.Fields.Add(new SqlField(new DbDataType(typeof(int)), "any", false));
				cte.Body!.Select.AddNew(new SqlValue(1), "any");
			}*/
		}

		protected static bool HasParameters(ISqlExpression expr)
		{
			var hasParameters  = null != expr.Find(QueryElementType.SqlParameter);

			return hasParameters;
		}

		static T NormalizeExpressions<T>(T expression, bool allowMutation)
			where T : class, IQueryElement
		{
			var result = expression.ConvertAll(allowMutation: allowMutation, static (visitor, e) =>
			{
				if (e.ElementType == QueryElementType.SqlExpression)
				{
					var expr = (SqlExpression)e;
					var newExpression = expr;

					// we interested in modifying only expressions which have parameters
					if (HasParameters(expr))
					{
						if (string.IsNullOrEmpty(expr.Expr) || expr.Parameters.Length == 0)
							return expr;

						var newExpressions = new List<ISqlExpression>();

						var ctx = WritableContext.Create(false, (newExpressions, visitor, expr));

						var newExpr = QueryHelper.TransformExpressionIndexes(
							ctx,
							expr.Expr,
							static (context, idx) =>
							{
								if (idx >= 0 && idx < context.StaticValue.expr.Parameters.Length)
								{
									var paramExpr  = context.StaticValue.expr.Parameters[idx];
									var normalized = NormalizeExpressions(paramExpr, context.StaticValue.visitor.AllowMutation);

									if (!context.WriteableValue && !ReferenceEquals(normalized, paramExpr))
										context.WriteableValue = true;

									var newIndex   = context.StaticValue.newExpressions.Count;

									context.StaticValue.newExpressions.Add(normalized);
									return newIndex;
								}
								return idx;
							});

						var changed = ctx.WriteableValue || newExpr != expr.Expr;

						if (changed)
							newExpression = new SqlExpression(expr.SystemType, newExpr, expr.Precedence, expr.Flags, expr.NullabilityType, null, newExpressions.ToArray());

						return newExpression;
					}
				}
				return e;
			});

			return result;
		}

		#region Alternative Builders

		protected SqlDeleteStatement GetAlternativeDelete(SqlDeleteStatement deleteStatement, DataOptions dataOptions)
		{
			if ((deleteStatement.SelectQuery.From.Tables.Count > 1 || deleteStatement.SelectQuery.From.Tables[0].Joins.Count > 0))
			{
				var table = deleteStatement.Table ?? deleteStatement.SelectQuery.From.Tables[0].Source as SqlTable;

				//TODO: probably we can improve this part
				if (table == null)
					throw new LinqToDBException("Could not deduce table for delete");

				if (deleteStatement.Output != null)
					throw new NotImplementedException("GetAlternativeDelete not implemented for delete with output");

				var sql = new SelectQuery { IsParameterDependent = deleteStatement.IsParameterDependent };

				var newDeleteStatement = new SqlDeleteStatement(sql);

				var copy      = new SqlTable(table) { Alias = null };
				var tableKeys = table.GetKeys(true);
				var copyKeys  = copy. GetKeys(true);

				if (deleteStatement.SelectQuery.Where.SearchCondition.Conditions.Any(static c => c.IsOr))
				{
					var sc1 = new SqlSearchCondition(deleteStatement.SelectQuery.Where.SearchCondition.Conditions);
					var sc2 = new SqlSearchCondition();

					for (var i = 0; i < tableKeys.Count; i++)
					{
						sc2.Conditions.Add(new SqlCondition(
							false,
							new SqlPredicate.ExprExpr(
								copyKeys[i],
								SqlPredicate.Operator.Equal,
								tableKeys[i],
								dataOptions.LinqOptions.CompareNullsAsValues ? true : null)));
					}

					deleteStatement.SelectQuery.Where.SearchCondition.Conditions.Clear();
					deleteStatement.SelectQuery.Where.SearchCondition.Conditions.Add(new SqlCondition(false, sc1));
					deleteStatement.SelectQuery.Where.SearchCondition.Conditions.Add(new SqlCondition(false, sc2));
				}
				else
				{
					for (var i = 0; i < tableKeys.Count; i++)
						deleteStatement.SelectQuery.Where.Expr(copyKeys[i]).Equal.Expr(tableKeys[i]);
				}

				newDeleteStatement.SelectQuery.From.Table(copy).Where.Exists(deleteStatement.SelectQuery);
				newDeleteStatement.With = deleteStatement.With;

				deleteStatement = newDeleteStatement;
			}

			return deleteStatement;
		}

		public static bool IsAggregationFunction(IQueryElement expr)
		{
			if (expr is SqlFunction func)
				return func.IsAggregate;

			if (expr is SqlExpression expression)
				return expression.IsAggregate;

			return false;
		}

		protected bool NeedsEnvelopingForUpdate(SelectQuery query)
		{
			if (query.Select.HasModifier || !query.GroupBy.IsEmpty)
				return true;

			if (!query.Where.IsEmpty)
			{
				if (query.Where.Find(IsAggregationFunction) != null)
					return true;
			}

			return false;
		}

		static bool HasComparisonInCondition(SqlSearchCondition search, SqlTable table)
		{
			return null != search.Find(e => e is SqlField field && field.Table == table);
		}

		protected static bool HasComparisonInQuery(SelectQuery query, SqlTable table)
		{
			if (query.Select.HasModifier || !query.GroupBy.IsEmpty)
				return false;

			if (HasComparisonInCondition(query.Where.SearchCondition, table))
				return true;

			foreach (var ts in query.From.Tables)
			{
				if (ts.Source is SelectQuery sc)
				{
					if (HasComparisonInQuery(sc, table))
						return true;
				}

				foreach (var join in ts.Joins)
				{
					if (join.JoinType == JoinType.Inner)
					{
						if (HasComparisonInCondition(join.Condition, table))
							return true;

						if (join.Table.Source is SelectQuery jq)
						{
							if (HasComparisonInQuery(jq, table))
								return true;
						}
					}
				}
						
			}

			return false;
		}

		protected static SqlOutputClause? RemapToOriginalTable(SqlOutputClause? outputClause)
		{
			//remapping to table back
			if (outputClause != null)
			{
				outputClause = outputClause.Convert((object?)null, (_, e) =>
				{
					if (e is SqlAnchor { AnchorKind: SqlAnchor.AnchorKindEnum.Deleted } anchor)
						return anchor.SqlExpression;
					return e;
				}, true);
			}

			return outputClause;
		}

		protected static bool RemoveUpdateTableIfPossible(SelectQuery query, SqlTable table, out SqlTableSource? source)
		{
			source = null;

			if (query.Select.HasModifier || !query.GroupBy.IsEmpty)
				return false;
				
			for (int i = 0; i < query.From.Tables.Count; i++)
			{
				var ts = query.From.Tables[i];
				if (ts.Joins.All(j => j.JoinType == JoinType.Inner || j.JoinType == JoinType.Left))
				{
					if (ts.Source == table)
					{
						source = ts;

						query.From.Tables.RemoveAt(i);
						for (int j = 0; j < ts.Joins.Count; j++)
						{
							query.From.Tables.Insert(i + j, ts.Joins[j].Table);
							query.Where.EnsureConjunction().ConcatSearchCondition(ts.Joins[j].Condition);
						}

						source.Joins.Clear();

						return true;
					}

					for (int j = 0; j < ts.Joins.Count; j++)
					{
						var join = ts.Joins[j];
						if (join.Table.Source == table)
						{
							if (ts.Joins.Skip(j + 1).Any(sj => QueryHelper.IsDependsOnSource(sj, table)))
								return false;

							source = join.Table;

							ts.Joins.RemoveAt(j);
							query.Where.EnsureConjunction().ConcatSearchCondition(join.Condition);

							for (int sj = 0; j < join.Table.Joins.Count; j++)
							{
								ts.Joins.Insert(j + sj, join.Table.Joins[sj]);
							}

							source.Joins.Clear();

							return true;
						}
					}
				}
			}

			return false;
		}

		static SelectQuery CloneQuery(
			SelectQuery                                  query, 
			SqlTable?                                    exceptTable,
			out Dictionary<IQueryElement, IQueryElement> replaceTree)
		{
			replaceTree = new Dictionary<IQueryElement, IQueryElement>();
			var clonedQuery = query.Clone(exceptTable, replaceTree, static (ut, e) =>
			{
				if ((e is SqlTable table && table == ut) || (e is SqlField field && field.Table == ut))
				{
					return false;
				}

				return true;
			});

			replaceTree = CorrectReplaceTree(replaceTree, exceptTable);

			return clonedQuery;
		}

		protected static SqlTable CloneTable(
			SqlTable                                     tableToClone,
			out Dictionary<IQueryElement, IQueryElement> replaceTree)
		{
			replaceTree = new Dictionary<IQueryElement, IQueryElement>();
			var clonedQuery = tableToClone.Clone(tableToClone, replaceTree,
				static (t, e) => (e is SqlTable table && table == t) || (e is SqlField field && field.Table == t));

			return clonedQuery;
		}

		static Dictionary<IQueryElement, IQueryElement> CorrectReplaceTree(Dictionary<IQueryElement, IQueryElement> replaceTree, SqlTable? exceptTable)
		{
			replaceTree = replaceTree
				.Where(pair =>
				{
					if (pair.Key is SqlTable table)
						return table != exceptTable;
					if (pair.Key is SqlColumn)
						return true;
					if (pair.Key is SqlField field)
						return field.Table != exceptTable;
					return false;
				})
				.ToDictionary(pair => pair.Key, pair => pair.Value);

			return replaceTree;
		}

		protected static TElement RemapCloned<TElement>(
			TElement                                  element,
			Dictionary<IQueryElement, IQueryElement>? mainTree,
			Dictionary<IQueryElement, IQueryElement>? innerTree = null,
			bool insideColumns = true)
		where TElement : class, IQueryElement
		{
			if (mainTree == null && innerTree == null) 
				return element;

			var newElement = element.Convert((mainTree, innerTree, insideColumns), static (v, expr) =>
			{
				var converted = v.Context.mainTree?.TryGetValue(expr, out var newValue) == true
					? newValue
					: expr;

				if (v.Context.innerTree != null)
				{
					converted = v.Context.innerTree.TryGetValue(converted, out newValue)
						? newValue
						: converted;
				}

				return converted;
			}, !insideColumns);

			return newElement;
		}

		static IEnumerable<(ISqlExpression, ISqlExpression)> GenerateRows(
			ISqlExpression                            target, 
			ISqlExpression                            source, 
			Dictionary<IQueryElement, IQueryElement>? mainTree,
			Dictionary<IQueryElement, IQueryElement>? innerTree, 
			SelectQuery                               selectQuery)
		{
			if (target is SqlRow targetRow && source is SqlRow sourceRow)
			{
				if (targetRow.Values.Length != sourceRow.Values.Length)
					throw new InvalidOperationException("Target and Source SqlRows are different");

				for (int i = 0; i < targetRow.Values.Length; i++)
				{
					var tagetRowValue  = targetRow.Values[i];
					var sourceRowValue = sourceRow.Values[i];

					foreach (var r in GenerateRows(tagetRowValue, sourceRowValue, mainTree, innerTree, selectQuery))
						yield return r;
				}
			}
			else
			{
				var ex         = RemapCloned(source, mainTree, innerTree);
				var columnExpr = selectQuery.Select.AddNewColumn(ex);

				yield return (target, columnExpr);

			}
		}

		protected SqlUpdateStatement GetAlternativeUpdate(SqlUpdateStatement updateStatement, DataOptions dataOptions)
		{
			if (updateStatement.Update.Table == null)
				throw new InvalidOperationException();

			if (!updateStatement.SelectQuery.Select.HasModifier && updateStatement.SelectQuery.From.Tables.Count == 1)
			{
				var sqlTableSource = updateStatement.SelectQuery.From.Tables[0];
				if (sqlTableSource.Source == updateStatement.Update.Table && sqlTableSource.Joins.Count == 0)
				{
					// Simple variant
					CorrectUpdateSetters(updateStatement);
					updateStatement.Update.TableSource = null;
					return updateStatement;
				}
			}

			var needsComparison = !HasComparisonInQuery(updateStatement.SelectQuery, updateStatement.Update.Table);

			if (!needsComparison)
			{
				// trying to simplify query
				RemoveUpdateTableIfPossible(updateStatement.SelectQuery, updateStatement.Update.Table, out _);
			}

			if (NeedsEnvelopingForUpdate(updateStatement.SelectQuery))
				updateStatement = QueryHelper.WrapQuery(updateStatement, updateStatement.SelectQuery, allowMutation: true);

			// It covers subqueries also. Simple subquery will have sourcesCount == 2
			if (QueryHelper.EnumerateAccessibleTableSources(updateStatement.SelectQuery).Any())
			{
				var sql = new SelectQuery { IsParameterDependent = updateStatement.IsParameterDependent  };

				var newUpdateStatement = new SqlUpdateStatement(sql);

				Dictionary<IQueryElement, IQueryElement>? replaceTree = null;

				var clonedQuery = CloneQuery(updateStatement.SelectQuery, needsComparison ? null : updateStatement.Update.Table, out replaceTree);

				SqlTable? tableToCompare = null;
				if (replaceTree.TryGetValue(updateStatement.Update.Table, out var newTable))
				{
					tableToCompare = (SqlTable)newTable;
				}

				if (tableToCompare != null)
				{
					replaceTree = CorrectReplaceTree(replaceTree, updateStatement.Update.Table);

					ApplyUpdateTableComparison(clonedQuery, updateStatement.Update, tableToCompare, dataOptions);
				}

				CorrectUpdateSetters(updateStatement);

				clonedQuery.Select.Columns.Clear();
				var processUniversalUpdate = true;

				if (updateStatement.Update.Items.Count > 1 && SqlProviderFlags.RowConstructorSupport.HasFlag(RowFeature.Update))
				{
					// check that items depends just on update table
					//
					var isComplex = false;
					foreach (var item in updateStatement.Update.Items)
					{
						var usedSources = new HashSet<ISqlTableSource>();
						QueryHelper.GetUsedSources(item.Expression!, usedSources);
						usedSources.Remove(updateStatement.Update.Table);
						if (replaceTree?.TryGetValue(updateStatement.Update.Table, out var replaced) == true)
							usedSources.Remove((ISqlTableSource)replaced);

						if (usedSources.Count > 0)
						{
							isComplex = true;
							break;
						}
					}

					if (isComplex)
					{
						// generating Row constructor update

						processUniversalUpdate = false;

						var innerQuery = CloneQuery(clonedQuery, updateStatement.Update.Table, out var innerTree);
						innerQuery.Select.Columns.Clear();

						var rows = new List<(ISqlExpression, ISqlExpression)>(updateStatement.Update.Items.Count);
						foreach (var item in updateStatement.Update.Items)
						{
							if (item.Expression == null)
								continue;

							rows.AddRange(GenerateRows(item.Column, item.Expression, replaceTree, innerTree, innerQuery));
						}

						var sqlRow        = new SqlRow(rows.Select(r => r.Item1).ToArray());
						var newUpdateItem = new SqlSetExpression(sqlRow, innerQuery);

						newUpdateStatement.Update.Items.Clear();
						newUpdateStatement.Update.Items.Add(newUpdateItem);
					}
				}

				if (processUniversalUpdate)
				{
					foreach (var item in updateStatement.Update.Items)
					{
						if (item.Expression == null)
							continue;

						var usedSources = new HashSet<ISqlTableSource>();

						var ex = item.Expression;

						QueryHelper.GetUsedSources(ex, usedSources);
						usedSources.Remove(updateStatement.Update.Table);

						if (usedSources.Count > 0)
						{
							// it means that update value column depends on other tables and we have to generate more complicated query

							var innerQuery = CloneQuery(clonedQuery, updateStatement.Update.Table, out var iterationTree);

							ex = RemapCloned(ex, replaceTree, iterationTree);

							innerQuery.Select.Columns.Clear();

							innerQuery.Select.AddNew(ex);
							innerQuery.RemoveNotUnusedColumns();

							ex = innerQuery;
						}
						else
						{
							ex = RemapCloned(ex, replaceTree, null);
						}

						item.Expression = ex;
						newUpdateStatement.Update.Items.Add(item);
					}
				}

				if (updateStatement.Output != null)
				{
					newUpdateStatement.Output = RemapCloned(updateStatement.Output, replaceTree, null);
				}

				newUpdateStatement.Update.Table = updateStatement.Update.Table;
				newUpdateStatement.With         = updateStatement.With;

				clonedQuery.RemoveNotUnusedColumns();
				clonedQuery.OptimizeSelectQuery(updateStatement, SqlProviderFlags, dataOptions);
				newUpdateStatement.SelectQuery.Where.Exists(clonedQuery);

				updateStatement.Update.Items.Clear();

				updateStatement = newUpdateStatement;
			}

			var (tableSource, _) = FindTableSource(new Stack<IQueryElement>(), updateStatement.SelectQuery, updateStatement.Update.Table!);

			if (tableSource == null)
			{
				CorrectUpdateSetters(updateStatement);
			}

			return updateStatement;
		}

		protected static void CorrectUpdateSetters(SqlUpdateStatement updateStatement)
		{
			// remove current column wrapping
			foreach (var item in updateStatement.Update.Items)
			{
				if (item.Expression == null)
					continue;

				item.Expression = item.Expression.Convert(updateStatement.SelectQuery, (v, e) =>
				{
					if (e is SqlColumn column && column.Parent == v.Context)
						return column.Expression;
					return e;
				});
			}
		}

		void ReplaceTable(ISqlExpressionWalkable? element, SqlTable replacing, SqlTable withTable)
		{
			element?.Walk(WalkOptions.Default, (replacing, withTable), static (ctx, e) =>
			{
				if (e is SqlField field && field.Table == ctx.replacing)
					return ctx.withTable.FindFieldByMemberName(field.Name) ?? throw new LinqException($"Field {field.Name} not found in table {ctx.withTable}");

				return e;
			});
		}

		protected SqlTable? FindUpdateTable(SelectQuery selectQuery, SqlTable tableToFind)
		{
			foreach (var tableSource in selectQuery.From.Tables)
			{
				if (tableSource.Source is SqlTable table && QueryHelper.IsEqualTables(table, tableToFind))
				{
					return table;
				}
			}

			foreach (var tableSource in selectQuery.From.Tables)
			{
				foreach (var join in tableSource.Joins)
				{
					if (join.Table.Source is SqlTable table)
					{
						if (QueryHelper.IsEqualTables(table, tableToFind))
							return table;
					}
					else if (join.Table.Source is SelectQuery query)
					{
						var found = FindUpdateTable(query, tableToFind);
						if (found != null)
							return found;
					}
				}
			}

			return null;
		}

		class DetachUpdateTableVisitor : SqlQueryVisitor
		{
			SqlUpdateStatement? _updateStatement;
			SqlTable?           _newTable;

			public DetachUpdateTableVisitor(): base(VisitMode.Modify)
			{
			}

			public override IQueryElement VisitSqlTableSource(SqlTableSource element)
			{
				if (element.Source == _updateStatement?.Update.Table)
				{
					if (_newTable == null)
					{
						var objectTree = new Dictionary<IQueryElement, IQueryElement>();
						_newTable = _updateStatement.Update.Table.Clone(objectTree);
						AddReplacements(objectTree);

						element.Source =_newTable!;
					}
				}

				return base.VisitSqlTableSource(element);
			}

			public override IQueryElement VisitSqlUpdateClause(SqlUpdateClause element)
			{
				// Do nothing
				return element;
			}

			public override IQueryElement VisitSqlSetExpression(SqlSetExpression element)
			{
				// Do nothing
				return element;
			}

			/*
			public override ISqlExpression VisitSqlColumnExpression(SqlColumn column, ISqlExpression expression)
			{
				// Do nothing
				return expression;
			}

			*/
			public SqlTable? DetachUpdateTable(SqlUpdateStatement updateStatement)
			{
				_newTable        = null;
				_updateStatement = updateStatement;

				Visit(updateStatement);

				return _newTable;
			}
		}

		protected SqlUpdateStatement DetachUpdateTableFromUpdateQuery(SqlUpdateStatement updateStatement, DataOptions dataOptions)
		{
			var updateTable = updateStatement.Update.Table;
			if (updateTable == null)
				throw new InvalidOperationException();

			// correct columns
			foreach (var item in updateStatement.Update.Items)
			{
				if (item.Column is SqlColumn column)
				{
					var field = QueryHelper.GetUnderlyingField(column.Expression);
					if (field == null)
						throw new InvalidOperationException("Expression {column.Expression} cannot be used for update field");
					item.Column = field;
				}
			}

			var visitor     = new DetachUpdateTableVisitor();
			var clonedTable = visitor.DetachUpdateTable(updateStatement);

			ApplyUpdateTableComparison(updateStatement.SelectQuery, updateStatement.Update, clonedTable, dataOptions);

			return updateStatement;
		}

		protected SqlStatement GetAlternativeUpdatePostgreSqlite(SqlUpdateStatement statement, DataOptions dataOptions)
		{
			if (statement.SelectQuery.Select.HasModifier)
			{
				statement = QueryHelper.WrapQuery(statement, statement.SelectQuery, allowMutation: true);
			}

			var tableToUpdate  = statement.Update.Table!;

			var hasUpdateTableInQuery = QueryHelper.HasTableInQuery(statement.SelectQuery, tableToUpdate);

			if (hasUpdateTableInQuery)
			{
				if (RemoveUpdateTableIfPossible(statement.SelectQuery, tableToUpdate, out _))
					hasUpdateTableInQuery = false;
			}

			if (hasUpdateTableInQuery)
			{
				statement = DetachUpdateTableFromUpdateQuery(statement, dataOptions);
			}

			CorrectUpdateSetters(statement);

			statement.Update.Table = tableToUpdate;

			return statement;
		}

		/// <summary>
		/// Corrects situation when update table is located in JOIN clause.
		/// Usually it is generated by associations.
		/// </summary>
		/// <param name="statement">Statement to examine.</param>
		/// <returns>Corrected statement.</returns>
		protected SqlUpdateStatement CorrectUpdateTable(SqlUpdateStatement statement, DataOptions dataOptions)
		{
			statement = BasicCorrectUpdate(statement, dataOptions, false);
			var tableToUpdate = statement.Update.Table;
			if (tableToUpdate != null)
			{
				var firstTable = statement.SelectQuery.From.Tables[0];

				if (firstTable.Source != tableToUpdate)
				{
					SqlTableSource? removedTableSource = null;

					if (QueryHelper.HasTableInQuery(statement.SelectQuery, tableToUpdate))
					{
						if (!RemoveUpdateTableIfPossible(statement.SelectQuery, tableToUpdate, out removedTableSource))
						{
							statement = DetachUpdateTableFromUpdateQuery(statement, dataOptions);
						}
					}

					var ts = removedTableSource ?? new SqlTableSource(tableToUpdate,
						QueryHelper.SuggestTableSourceAlias(statement.SelectQuery, "u"));

					statement.SelectQuery.From.Tables.Insert(0, ts);
				}

				statement.Update.TableSource = statement.SelectQuery.From.Tables[0];
			}

			CorrectUpdateSetters(statement);

			return statement;
		}

		#endregion

		#region Helpers

		static string? SetAlias(string? alias, int maxLen)
		{
			if (alias == null)
				return null;

			alias = alias.TrimStart('_');

			var cs      = alias.ToCharArray();
			var replace = false;

			for (var i = 0; i < cs.Length; i++)
			{
				var c = cs[i];

				if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '_')
					continue;

				cs[i] = ' ';
				replace = true;
			}

			if (replace)
				alias = new string(cs).Replace(" ", "");

			return alias.Length == 0 || alias.Length > maxLen ? null : alias;
		}

		protected void CheckAliases(SqlStatement statement, int maxLen)
		{
			statement.Visit(maxLen, static (maxLen, e) =>
			{
				switch (e.ElementType)
				{
					case QueryElementType.SqlField     : ((SqlField)      e).Alias = SetAlias(((SqlField)      e).Alias, maxLen); break;
					case QueryElementType.SqlTable     : ((SqlTable)      e).Alias = SetAlias(((SqlTable)      e).Alias, maxLen); break;
					case QueryElementType.Column       : ((SqlColumn)     e).Alias = SetAlias(((SqlColumn)     e).Alias, maxLen); break;
					case QueryElementType.TableSource  : ((SqlTableSource)e).Alias = SetAlias(((SqlTableSource)e).Alias, maxLen); break;
				}
			});
		}

		#endregion

		#region Optimizing Joins

		public void OptimizeJoins(SqlStatement statement)
		{
			((ISqlExpressionWalkable) statement).Walk(WalkOptions.Default, statement, static (statement, element) =>
			{
				if (element is SelectQuery query)
					new JoinOptimizer().OptimizeJoins(statement, query);
				return element;
			});
		}

		#endregion

		public virtual bool IsParameterDependedQuery(SelectQuery query)
		{
			var takeValue = query.Select.TakeValue;
			if (takeValue != null)
			{
				var supportsParameter = SqlProviderFlags.GetAcceptsTakeAsParameterFlag(query);

				if (!supportsParameter)
				{
					if (takeValue.ElementType != QueryElementType.SqlValue && takeValue.CanBeEvaluated(true))
						return true;
				}
				else if (takeValue.ElementType != QueryElementType.SqlParameter)
					return true;

			}

			var skipValue = query.Select.SkipValue;
			if (skipValue != null)
			{

				var supportsParameter = SqlProviderFlags.GetIsSkipSupportedFlag(query.Select.TakeValue, query.Select.SkipValue)
				                        && SqlProviderFlags.AcceptsTakeAsParameter;

				if (!supportsParameter)
				{
					if (skipValue.ElementType != QueryElementType.SqlValue && skipValue.CanBeEvaluated(true))
						return true;
				}
				else if (skipValue.ElementType != QueryElementType.SqlParameter)
					return true;
			}

			return false;
		}

		public virtual bool IsParameterDependedElement(NullabilityContext nullability, IQueryElement element)
		{
			switch (element.ElementType)
			{
				case QueryElementType.SelectStatement:
				case QueryElementType.InsertStatement:
				case QueryElementType.InsertOrUpdateStatement:
				case QueryElementType.UpdateStatement:
				case QueryElementType.DeleteStatement:
				case QueryElementType.CreateTableStatement:
				case QueryElementType.DropTableStatement:
				case QueryElementType.MergeStatement:
				case QueryElementType.MultiInsertStatement:
				{
					var statement = (SqlStatement)element;
					return statement.IsParameterDependent;
				}
				case QueryElementType.SqlValuesTable:
				{
					return ((SqlValuesTable)element).Rows == null;
				}
				case QueryElementType.SqlParameter:
				{
					return !((SqlParameter)element).IsQueryParameter;
				}
				case QueryElementType.SqlQuery:
				{
					if (((SelectQuery)element).IsParameterDependent)
						return true;
					return IsParameterDependedQuery((SelectQuery)element);
				}
				case QueryElementType.SqlBinaryExpression:
				{
					return element.IsMutable();
				}
				case QueryElementType.ExprPredicate:
				{
					var exprExpr = (SqlPredicate.Expr)element;

					if (exprExpr.Expr1.IsMutable())
						return true;
					return false;
				}
				case QueryElementType.ExprExprPredicate:
				{
					var exprExpr = (SqlPredicate.ExprExpr)element;

					var isMutable1 = exprExpr.Expr1.IsMutable();
					var isMutable2 = exprExpr.Expr2.IsMutable();

					if (isMutable1 && isMutable2)
						return true;

					if (isMutable1 && exprExpr.Expr2.CanBeEvaluated(false))
						return true;

					if (isMutable2 && exprExpr.Expr1.CanBeEvaluated(false))
						return true;

					if (isMutable1 && exprExpr.Expr1.ShouldCheckForNull(nullability))
						return true;

					if (isMutable2 && exprExpr.Expr2.ShouldCheckForNull(nullability))
						return true;

					return false;
				}
				case QueryElementType.IsDistinctPredicate:
				{
					var expr = (SqlPredicate.IsDistinct)element;
					return expr.Expr1.IsMutable() || expr.Expr2.IsMutable();
				}
				case QueryElementType.IsTruePredicate:
				{
					var isTruePredicate = (SqlPredicate.IsTrue)element;

					if (isTruePredicate.Expr1.IsMutable())
						return true;
					return false;
				}
				case QueryElementType.InListPredicate:
				{
					return true;
				}
				case QueryElementType.SearchStringPredicate:
				{
					var searchString = (SqlPredicate.SearchString)element;
					if (searchString.Expr2.ElementType != QueryElementType.SqlValue)
						return true;

					return IsParameterDependedElement(nullability, searchString.CaseSensitive);
				}
				case QueryElementType.SqlFunction:
				{
					var sqlFunc = (SqlFunction)element;
					switch (sqlFunc.Name)
					{
						case "CASE":
						{
							for (int i = 0; i < sqlFunc.Parameters.Length - 2; i += 2)
							{
								var testParam = sqlFunc.Parameters[i];
								if (testParam.CanBeEvaluated(true))
									return true;
							}
							break;
						}
						case "Length":
						{
							if (sqlFunc.Parameters[0].CanBeEvaluated(true))
								return true;
							break;
						}
					}
					break;
				}
			}

			return false;
		}

		public bool IsParameterDependent(NullabilityContext nullability, SqlStatement statement)
		{
			return null != statement.Find((optimizer : this, nullability),
				static (ctx, e) => ctx.optimizer.IsParameterDependedElement(ctx.nullability, e));
		}

		public virtual SqlStatement FinalizeStatement(SqlStatement statement, EvaluationContext context, DataOptions dataOptions)
		{
			var newStatement = TransformStatement(statement, dataOptions);
			newStatement = FinalizeUpdate(newStatement, dataOptions);

			if (SqlProviderFlags.IsParameterOrderDependent)
			{
				// ensure that parameters in expressions are well sorted
				newStatement = NormalizeExpressions(newStatement, context.ParameterValues == null);
			}

			return newStatement;
		}

		public SqlStatement OptimizeAggregates(SqlStatement statement)
		{
			var newStatement = QueryHelper.JoinRemoval(statement, statement, static (statement, currentStatement, join) =>
			{
				if (join.JoinType == JoinType.CrossApply || join.JoinType == JoinType.OuterApply)
				{
					if (join.Table.Source is SelectQuery query && query.Select.Columns.Count > 0)
					{
						var isAggregateQuery =
							query.Select.Columns.All(static c => QueryHelper.IsAggregationOrWindowFunction(c.Expression));
						if (isAggregateQuery)
						{
							// remove unwanted join
							if (!QueryHelper.IsDependsOnSources(statement, new HashSet<ISqlTableSource> { query },
								new HashSet<IQueryElement> { join }))
								return true;
						}
					}
				}

				return false;
			});

			return newStatement;
		}

		public virtual void ConvertSkipTake(NullabilityContext nullability, MappingSchema mappingSchema, DataOptions dataOptions,
			SelectQuery selectQuery, OptimizationContext optimizationContext, out ISqlExpression? takeExpr,
			out ISqlExpression? skipExpr)
		{
			// make skip take as parameters or evaluate otherwise

			takeExpr = optimizationContext.ConvertAll(selectQuery.Select.TakeValue, nullability);
			skipExpr = optimizationContext.ConvertAll(selectQuery.Select.SkipValue, nullability);

			if (takeExpr != null)
			{
				var supportsParameter = SqlProviderFlags.GetAcceptsTakeAsParameterFlag(selectQuery);

				if (supportsParameter)
				{
					if (takeExpr.ElementType != QueryElementType.SqlParameter && takeExpr.ElementType != QueryElementType.SqlValue)
					{
						var takeValue = takeExpr.EvaluateExpression(optimizationContext.Context)!;
						var takeParameter = new SqlParameter(new DbDataType(takeValue.GetType()), "take", takeValue)
						{
							IsQueryParameter = dataOptions.LinqOptions.ParameterizeTakeSkip && !QueryHelper.NeedParameterInlining(takeExpr)
						};
						takeExpr = takeParameter;
					}
				}
				else if (takeExpr.ElementType != QueryElementType.SqlValue)
					takeExpr = new SqlValue(takeExpr.EvaluateExpression(optimizationContext.Context)!);
			}

			if (skipExpr != null)
			{
				var supportsParameter = SqlProviderFlags.GetIsSkipSupportedFlag(selectQuery.Select.TakeValue, selectQuery.Select.SkipValue)
				                        && SqlProviderFlags.AcceptsTakeAsParameter;

				if (supportsParameter)
				{
					if (skipExpr.ElementType != QueryElementType.SqlParameter && skipExpr.ElementType != QueryElementType.SqlValue)
					{
						var skipValue = skipExpr.EvaluateExpression(optimizationContext.Context)!;
						var skipParameter = new SqlParameter(new DbDataType(skipValue.GetType()), "skip", skipValue)
						{
							IsQueryParameter = dataOptions.LinqOptions.ParameterizeTakeSkip && !QueryHelper.NeedParameterInlining(skipExpr)
						};
						skipExpr = skipParameter;
					}
				}
				else if (skipExpr.ElementType != QueryElementType.SqlValue)
					skipExpr = new SqlValue(skipExpr.EvaluateExpression(optimizationContext.Context)!);
			}
		}

		/// <summary>
		/// Moves Distinct query into another subquery. Useful when preserving ordering is required, because some providers do not support DISTINCT ORDER BY.
		/// <code>
		/// -- before
		/// SELECT DISTINCT TAKE 10 c1, c2
		/// FROM A
		/// ORDER BY c1
		/// -- after
		/// SELECT TAKE 10 B.c1, B.c2
		/// FROM
		///   (
		///     SELECT DISTINCT c1, c2
		///     FROM A
		///   ) B
		/// ORDER BY B.c1
		/// </code>
		/// </summary>
		/// <param name="statement">Statement which may contain take/skip and Distinct modifiers.</param>
		/// <param name="queryFilter">Query filter predicate to determine if query needs processing.</param>
		/// <returns>The same <paramref name="statement"/> or modified statement when transformation has been performed.</returns>
		protected SqlStatement SeparateDistinctFromPagination(SqlStatement statement, Func<SelectQuery, bool> queryFilter)
		{
			return QueryHelper.WrapQuery(
				queryFilter,
				statement,
				static (queryFilter, q, _) => q.Select.IsDistinct && queryFilter(q),
				static (_, p, q) =>
				{
					p.Select.SkipValue = q.Select.SkipValue;
					p.Select.Take(q.Select.TakeValue, q.Select.TakeHints);

					q.Select.SkipValue = null;
					q.Select.Take(null, null);

					QueryHelper.MoveOrderByUp(p, q);
				},
				allowMutation: true,
				withStack: false);
		}

		/// <summary>
		/// Replaces pagination by Window function ROW_NUMBER().
		/// </summary>
		/// <param name="context"><paramref name="predicate"/> context object.</param>
		/// <param name="statement">Statement which may contain take/skip modifiers.</param>
		/// <param name="supportsEmptyOrderBy">Indicates that database supports OVER () syntax.</param>
		/// <param name="predicate">Indicates when the transformation is needed</param>
		/// <returns>The same <paramref name="statement"/> or modified statement when transformation has been performed.</returns>
		protected SqlStatement ReplaceTakeSkipWithRowNumber<TContext>(TContext context, SqlStatement statement, Func<TContext, SelectQuery, bool> predicate, bool supportsEmptyOrderBy)
		{
			return QueryHelper.WrapQuery(
				(predicate, context, supportsEmptyOrderBy),
				statement,
				static (context, query, _) =>
				{
					if ((query.Select.TakeValue == null || query.Select.TakeHints != null) && query.Select.SkipValue == null)
						return 0;
					return context.predicate(context.context, query) ? 1 : 0;
				},
				static (context, queries) =>
				{
					var query = queries[queries.Count - 1];
					var processingQuery = queries[queries.Count - 2];

					IReadOnlyCollection<SqlOrderByItem>? orderByItems = null;
					if (!query.OrderBy.IsEmpty)
						orderByItems = query.OrderBy.Items;
					//else if (query.Select.Columns.Count > 0)
					//{
					//	orderByItems = query.Select.Columns
					//		.Select(static c => QueryHelper.NeedColumnForExpression(query, c, false))
					//		.Where(static e => e != null)
					//		.Take(1)
					//		.Select(static e => new SqlOrderByItem(e, false))
					//		.ToArray();
					//}

					if (orderByItems == null || orderByItems.Count == 0)
						orderByItems = context.supportsEmptyOrderBy ? Array<SqlOrderByItem>.Empty : new[] { new SqlOrderByItem(new SqlExpression("SELECT NULL"), false) };

					var orderBy = string.Join(", ",
						orderByItems.Select(static (oi, i) => oi.IsDescending ? $"{{{i}}} DESC" : $"{{{i}}}"));

					var parameters = orderByItems.Select(static oi => oi.Expression).ToArray();

					// careful here - don't clear it before orderByItems used
					query.OrderBy.Items.Clear();

					var rowNumberExpression = parameters.Length == 0
						? new SqlExpression(typeof(long), "ROW_NUMBER() OVER ()", Precedence.Primary, SqlFlags.IsWindowFunction, ParametersNullabilityType.NotNullable, null)
						: new SqlExpression(typeof(long), $"ROW_NUMBER() OVER (ORDER BY {orderBy})", Precedence.Primary, SqlFlags.IsWindowFunction, ParametersNullabilityType.NotNullable, null, parameters);

					var rowNumberColumn = query.Select.AddNewColumn(rowNumberExpression);
					rowNumberColumn.Alias = "RN";

					if (query.Select.SkipValue != null)
					{
						processingQuery.Where.EnsureConjunction().Expr(rowNumberColumn).Greater
							.Expr(query.Select.SkipValue);

						if (query.Select.TakeValue != null)
							processingQuery.Where.Expr(rowNumberColumn).LessOrEqual.Expr(
								new SqlBinaryExpression(query.Select.SkipValue.SystemType!,
									query.Select.SkipValue, "+", query.Select.TakeValue));
					}
					else
					{
						processingQuery.Where.EnsureConjunction().Expr(rowNumberColumn).LessOrEqual
							.Expr(query.Select.TakeValue!);
					}

					query.Select.SkipValue = null;
					query.Select.Take(null, null);

				},
				allowMutation: true,
				withStack: false);
		}

		/// <summary>
		/// Alternative mechanism how to prevent loosing sorting in Distinct queries.
		/// </summary>
		/// <param name="statement">Statement which may contain Distinct queries.</param>
		/// <param name="queryFilter">Query filter predicate to determine if query needs processing.</param>
		/// <returns>The same <paramref name="statement"/> or modified statement when transformation has been performed.</returns>
		protected SqlStatement ReplaceDistinctOrderByWithRowNumber(SqlStatement statement, Func<SelectQuery, bool> queryFilter)
		{
			return QueryHelper.WrapQuery(
				queryFilter,
				statement,
				static (queryFilter, q, _) => (q.Select.IsDistinct && !q.Select.OrderBy.IsEmpty && queryFilter(q)) /*|| q.Select.TakeValue != null || q.Select.SkipValue != null*/,
				static (_, p, q) =>
				{
					var columnItems  = q.Select.Columns.Select(static c => c.Expression).ToList();
					var orderItems   = q.Select.OrderBy.Items.Select(static o => o.Expression).ToList();

					var projectionItemsCount = columnItems.Union(orderItems).Count();
					if (projectionItemsCount < columnItems.Count)
					{
						// Sort columns not in projection, transforming to
						/*
							 SELECT {S.columnItems}, S.RN FROM
							 (
								  SELECT {columnItems + orderItems}, RN = ROW_NUMBER() OVER (PARTITION BY {columnItems} ORDER BY {orderItems}) FROM T
							 )
							 WHERE S.RN = 1
						*/

						var orderByItems = q.Select.OrderBy.Items;

						var partitionBy = string.Join(", ", columnItems.Select(static (oi, i) => $"{{{i}}}"));

						var columns = new string[orderByItems.Count];
						for (var i = 0; i < columns.Length; i++)
							columns[i] = orderByItems[i].IsDescending
								? $"{{{i + columnItems.Count}}} DESC"
								: $"{{{i + columnItems.Count}}}";
						var orderBy = string.Join(", ", columns);

						var parameters = columnItems.Concat(orderByItems.Select(static oi => oi.Expression)).ToArray();

						var rnExpr = new SqlExpression(typeof(long),
							$"ROW_NUMBER() OVER (PARTITION BY {partitionBy} ORDER BY {orderBy})", Precedence.Primary,
							SqlFlags.IsWindowFunction, ParametersNullabilityType.NotNullable, null, parameters);

						var additionalProjection = orderItems.Except(columnItems);
						foreach (var expr in additionalProjection)
						{
							q.Select.AddNew(expr);
						}

						var rnColumn = q.Select.AddNewColumn(rnExpr);
						rnColumn.Alias = "RN";

						q.Select.IsDistinct = false;
						q.OrderBy.Items.Clear();
						p.Select.Where.EnsureConjunction().Expr(rnColumn).Equal.Value(1);
					}
					else
					{
						// All sorting columns in projection, transforming to
						/*
							 SELECT {S.columnItems} FROM
							 (
								  SELECT DISTINCT {columnItems} FROM T
							 )
							 ORDER BY {orderItems}

						*/

						QueryHelper.MoveOrderByUp(p, q);
					}
				},
				allowMutation: true,
				withStack: false);
		}

		#region Helper functions

		protected static ISqlExpression TryConvertToValue(ISqlExpression expr, EvaluationContext context)
		{
			if (expr.ElementType != QueryElementType.SqlValue)
			{
				if (expr.TryEvaluateExpression(context, out var value))
					expr = new SqlValue(expr.GetExpressionType(), value);
			}

			return expr;
		}

		protected static bool IsBooleanParameter(ISqlExpression expr, int count, int i)
		{
			if ((i % 2 == 1 || i == count - 1) && expr.SystemType == typeof(bool) || expr.SystemType == typeof(bool?))
			{
				switch (expr.ElementType)
				{
					case QueryElementType.SearchCondition: return true;
				}
			}

			return false;
		}

		protected SqlFunction ConvertFunctionParameters(SqlFunction func, bool withParameters = false)
		{
			if (func.Name == "CASE")
			{
				ISqlExpression[]? parameters = null;
				for (var i = 0; i < func.Parameters.Length; i++)
				{
					var p = func.Parameters[i];
					if (IsBooleanParameter(p, func.Parameters.Length, i))
					{
						if (parameters == null)
						{
							parameters = new ISqlExpression[func.Parameters.Length];
							for (var j = 0; j < i; j++)
								parameters[j] = func.Parameters[j];
						}
						parameters[i] = new SqlFunction(typeof(bool), "CASE", p, new SqlValue(true), new SqlValue(false))
						{
							CanBeNull     = false,
							DoNotOptimize = true
						};
					}
					else if (parameters != null)
						parameters[i] = p;
				}

				if (parameters != null)
					return new SqlFunction(
						func.SystemType,
						func.Name,
						false,
						func.Precedence,
						parameters);
			}

			return func;
		}

		#endregion
	}
}
