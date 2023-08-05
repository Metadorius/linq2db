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
	[Table("Doctor")]
	public class Doctor : IEquatable<Doctor>
	{
		[Column("PersonID", DataType  = DataType.Int32, DbType   = "Int32"          , IsPrimaryKey = true    , SkipOnUpdate = true)] public int    PersonId { get; set; } // Int32
		[Column("Taxonomy", CanBeNull = false         , DataType = DataType.NVarChar, DbType       = "String"                     )] public string Taxonomy { get; set; } = null!; // String

		#region IEquatable<T> support
		private static readonly IEqualityComparer<Doctor> _equalityComparer = ComparerBuilder.GetEqualityComparer<Doctor>(c => c.PersonId);

		public bool Equals(Doctor? other)
		{
			return _equalityComparer.Equals(this, other!);
		}

		public override int GetHashCode()
		{
			return _equalityComparer.GetHashCode(this);
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as Doctor);
		}
		#endregion
	}
}
