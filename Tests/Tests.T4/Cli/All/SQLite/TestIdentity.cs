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

namespace Cli.All.SQLite
{
	[Table("TestIdentity")]
	public class TestIdentity : IEquatable<TestIdentity>
	{
		[Column("ID", DataType = DataType.Int64, DbType = "integer", Length = 8, Precision = 19, Scale = 0, IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public long Id { get; set; } // integer

		#region IEquatable<T> support
		private static readonly IEqualityComparer<TestIdentity> _equalityComparer = ComparerBuilder.GetEqualityComparer<TestIdentity>(c => c.Id);

		public bool Equals(TestIdentity? other)
		{
			return _equalityComparer.Equals(this, other!);
		}

		public override int GetHashCode()
		{
			return _equalityComparer.GetHashCode(this);
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as TestIdentity);
		}
		#endregion
	}
}
