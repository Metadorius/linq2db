﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace LinqToDB.SqlQuery
{
	public class SqlFromClause : ClauseBase, IQueryElement, ISqlExpressionWalkable
	{
		#region Join

		public class Join : ConditionBase<Join,Join.Next>
		{
			public class Next
			{
				internal Next(Join parent)
				{
					_parent = parent;
				}

				readonly Join _parent;

				public Join Or  => _parent.SetOr(true);
				public Join And => _parent.SetOr(false);

				public static implicit operator Join(Next next)
				{
					return next._parent;
				}
			}

			protected override SqlSearchCondition Search => JoinedTable.Condition;

			protected override Next GetNext()
			{
				return new Next(this);
			}

			internal Join(JoinType joinType, ISqlTableSource table, string? alias, bool isWeak, ICollection<Join>? joins)
			{
				JoinedTable = new SqlJoinedTable(joinType, table, alias, isWeak);

				if (joins != null && joins.Count > 0)
					foreach (var join in joins)
						JoinedTable.Table.Joins.Add(join.JoinedTable);
			}

			public SqlJoinedTable JoinedTable { get; }
		}

		#endregion

		internal SqlFromClause(SelectQuery selectQuery) : base(selectQuery)
		{
		}

		internal SqlFromClause(IEnumerable<SqlTableSource> tables)
			: base(null)
		{
			Tables.AddRange(tables);
		}

		public SqlFromClause Table(ISqlTableSource table, params Join[] joins)
		{
			return Table(table, null, joins);
		}

		public SqlFromClause Table(ISqlTableSource table, string? alias, params Join[] joins)
		{
			var ts = AddOrGetTable(table, alias);

			if (joins != null && joins.Length > 0)
				foreach (var join in joins)
					ts.Joins.Add(join.JoinedTable);

			return this;
		}

		SqlTableSource? GetTable(ISqlTableSource table, string? alias)
		{
			foreach (var ts in Tables)
				if (ts.Source == table)
					if (alias == null || ts.Alias == alias)
						return ts;
					else
						throw new ArgumentException($"Invalid alias: '{ts.Alias}' != '{alias}'");

			return null;
		}

		SqlTableSource AddOrGetTable(ISqlTableSource table, string? alias)
		{
			var ts = GetTable(table, alias);

			if (ts != null)
				return ts;

			var t = new SqlTableSource(table, alias);

			Tables.Add(t);

			return t;
		}

		public SqlTableSource? this[ISqlTableSource table] => this[table, null];

		public SqlTableSource? this[ISqlTableSource table, string? alias]
		{
			get
			{
				foreach (var ts in Tables)
				{
					var t = SelectQuery.CheckTableSource(ts, table, alias);

					if (t != null)
						return t;
				}

				return null;
			}
		}

		public bool IsChild(ISqlTableSource table)
		{
			foreach (var ts in Tables)
				if (ts.Source == table || CheckChild(ts.Joins, table))
					return true;
			return false;
		}

		static bool CheckChild(IEnumerable<SqlJoinedTable> joins, ISqlTableSource table)
		{
			foreach (var j in joins)
				if (j.Table.Source == table || CheckChild(j.Table.Joins, table))
					return true;
			return false;
		}

		public List<SqlTableSource> Tables { get; } = new List<SqlTableSource>();

		static IEnumerable<ISqlTableSource> GetJoinTables(SqlTableSource source, QueryElementType elementType)
		{
			if (source.Source.ElementType == elementType)
				yield return source.Source;

			foreach (var join in source.Joins)
			foreach (var table in GetJoinTables(join.Table, elementType))
				yield return table;
		}

		internal IEnumerable<ISqlTableSource> GetFromTables()
		{
			return Tables.SelectMany(_ => GetJoinTables(_, QueryElementType.SqlTable));
		}

		internal IEnumerable<ISqlTableSource> GetFromQueries()
		{
			return Tables.SelectMany(_ => GetJoinTables(_, QueryElementType.SqlQuery));
		}

		static SqlTableSource? FindTableSource(SqlTableSource source, SqlTable table)
		{
			if (source.Source == table)
				return source;

			foreach (var join in source.Joins)
			{
				var ts = FindTableSource(join.Table, table);
				if (ts != null)
					return ts;
			}

			return null;
		}

		public ISqlTableSource? FindTableSource(SqlTable table)
		{
			foreach (var source in Tables)
			{
				var ts = FindTableSource(source, table);
				if (ts != null)
					return ts;
			}

			return null;
		}

		#region Overrides

#if OVERRIDETOSTRING

			public override string ToString()
			{
				return this.ToDebugString(SelectQuery);
			}

#endif

		#endregion

		#region ISqlExpressionWalkable Members

		ISqlExpression? ISqlExpressionWalkable.Walk<TContext>(WalkOptions options, TContext context, Func<TContext, ISqlExpression, ISqlExpression> func)
		{
			foreach (var table in Tables)
				((ISqlExpressionWalkable)table).Walk(options, context, func);

			return null;
		}

		#endregion

		#region IQueryElement Members

#if DEBUG
		public string DebugText => this.ToDebugString();
#endif
		public QueryElementType ElementType => QueryElementType.FromClause;

		QueryElementTextWriter IQueryElement.ToString(QueryElementTextWriter writer)
		{
			if (writer.Length > 10240)
				return writer;

			writer
				.Append("FROM ");

			if (Tables.Count > 0)
			{
				using(writer.WithScope())
					for (var index = 0; index < Tables.Count; index++)
					{
						var ts = Tables[index];
						writer.AppendElement(ts);

						if (index < Tables.Count - 1)
							writer.AppendLine(",");
					}
			}

			return writer;
		}

		#endregion

		public void Cleanup()
		{
			Tables.Clear();
		}
	}
}
