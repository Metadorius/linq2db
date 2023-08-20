﻿using System;

namespace LinqToDB
{
	using Common;
	using Common.Internal;

	/// <param name="IgnoreConstantExpressionInOrderBy">
	/// If <c>true</c>, linq2db will ignore any constant expressions in ORDER BY clause.
	/// Default value: <c>false</c>.
	/// </param>
	/// <param name="GenerateFinalAliases">
	/// Indicates whether SQL Builder should generate aliases for final projection.
	/// It is not required for correct query processing but simplifies SQL analysis.
	/// <para>
	/// Default value: <c>false</c>.
	/// </para>
	/// <example>
	/// For the query
	/// <code>
	/// var query = from child in db.Child
	///	   select new
	///	   {
	///       TrackId = child.ChildID,
	///	   };
	/// </code>
	/// When property is <c>true</c>
	/// <code>
	/// SELECT
	///	   [child].[ChildID] as [TrackId]
	/// FROM
	///	   [Child] [child]
	/// </code>
	/// Otherwise alias will be removed
	/// <code>
	/// SELECT
	///	   [child].[ChildID]
	/// FROM
	///	   [Child] [child]
	/// </code>
	/// </example>
	/// </param>
	public sealed record SqlOptions
	(
		bool IgnoreConstantExpressionInOrderBy = default,
		bool GenerateFinalAliases              = false

	)
		: IOptionSet
	{
		public SqlOptions() : this(false)
		{
		}

		SqlOptions(SqlOptions original)
		{
			IgnoreConstantExpressionInOrderBy = original.IgnoreConstantExpressionInOrderBy;
			GenerateFinalAliases              = original.GenerateFinalAliases;
		}

		int? _configurationID;
		int IConfigurationID.ConfigurationID
		{
			get
			{
				if (_configurationID == null)
				{
					using var idBuilder = new IdentifierBuilder();
					_configurationID = idBuilder
						.Add(IgnoreConstantExpressionInOrderBy)
						.Add(GenerateFinalAliases)
						.CreateID();
				}

				return _configurationID.Value;
			}
		}

		#region IEquatable implementation

		public bool Equals(SqlOptions? other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;

			return ((IOptionSet)this).ConfigurationID == ((IOptionSet)other).ConfigurationID;
		}

		public override int GetHashCode()
		{
			return ((IOptionSet)this).ConfigurationID;
		}

		#endregion
	}
}
