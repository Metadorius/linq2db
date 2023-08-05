// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------

using LinqToDB;
using LinqToDB.Mapping;
using LinqToDB.Tools.Comparers;
using System;
using System.Collections.Generic;

#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.All.SQLiteNorthwind
{
	[Table("Territories")]
	public class Territory : IEquatable<Territory>
	{
		[Column("TerritoryID"         , CanBeNull = false         , DataType = DataType.VarChar, DbType = "varchar(20)", Length    = 20, Precision = 0, Scale = 0, IsPrimaryKey = true)] public string TerritoryId          { get; set; } = null!; // varchar(20)
		[Column("TerritoryDescription", CanBeNull = false         , DataType = DataType.VarChar, DbType = "varchar(50)", Length    = 50, Precision = 0, Scale = 0                     )] public string TerritoryDescription { get; set; } = null!; // varchar(50)
		[Column("RegionID"            , DataType  = DataType.Int32, DbType   = "int"           , Length = 4            , Precision = 10, Scale     = 0                                )] public int    RegionId             { get; set; } // int

		#region IEquatable<T> support
		private static readonly IEqualityComparer<Territory> _equalityComparer = ComparerBuilder.GetEqualityComparer<Territory>(c => c.TerritoryId);

		public bool Equals(Territory? other)
		{
			return _equalityComparer.Equals(this, other!);
		}

		public override int GetHashCode()
		{
			return _equalityComparer.GetHashCode(this);
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as Territory);
		}
		#endregion
	}
}
