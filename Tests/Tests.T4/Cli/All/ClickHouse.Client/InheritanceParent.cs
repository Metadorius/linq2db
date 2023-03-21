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

namespace Cli.All.ClickHouse.Client
{
	[Table("InheritanceParent")]
	public class InheritanceParent : IEquatable<InheritanceParent>
	{
		[Column("InheritanceParentId", DataType = DataType.Int32   , DbType = "Int32" , IsPrimaryKey = true, SkipOnUpdate = true)] public int     InheritanceParentId { get; set; } // Int32
		[Column("TypeDiscriminator"  , DataType = DataType.Int32   , DbType = "Int32"                                           )] public int?    TypeDiscriminator   { get; set; } // Int32
		[Column("Name"               , DataType = DataType.NVarChar, DbType = "String"                                          )] public string? Name                { get; set; } // String

		#region IEquatable<T> support
		private static readonly IEqualityComparer<InheritanceParent> _equalityComparer = ComparerBuilder.GetEqualityComparer<InheritanceParent>(c => c.InheritanceParentId);

		public bool Equals(InheritanceParent? other)
		{
			return _equalityComparer.Equals(this, other!);
		}

		public override int GetHashCode()
		{
			return _equalityComparer.GetHashCode(this);
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as InheritanceParent);
		}
		#endregion
	}
}
