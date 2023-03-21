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

namespace Cli.All.SapHana
{
	[Table("Patient")]
	public class Patient : IEquatable<Patient>
	{
		[Column("PersonID" , DataType  = DataType.Int32, DbType   = "INTEGER"        , Length = 10             , Precision = 10 , Scale     = 0  , IsPrimaryKey = true)] public int    PersonId  { get; set; } // INTEGER
		[Column("Diagnosis", CanBeNull = false         , DataType = DataType.NVarChar, DbType = "NVARCHAR(256)", Length    = 256, Precision = 256, Scale        = 0   )] public string Diagnosis { get; set; } = null!; // NVARCHAR(256)

		#region IEquatable<T> support
		private static readonly IEqualityComparer<Patient> _equalityComparer = ComparerBuilder.GetEqualityComparer<Patient>(c => c.PersonId);

		public bool Equals(Patient? other)
		{
			return _equalityComparer.Equals(this, other!);
		}

		public override int GetHashCode()
		{
			return _equalityComparer.GetHashCode(this);
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as Patient);
		}
		#endregion

		#region Associations
		/// <summary>
		/// FK_Patient_Person
		/// </summary>
		[Association(CanBeNull = false, ThisKey = nameof(PersonId), OtherKey = nameof(SapHana.Person.PersonId))]
		public Person Person { get; set; } = null!;
		#endregion
	}
}
