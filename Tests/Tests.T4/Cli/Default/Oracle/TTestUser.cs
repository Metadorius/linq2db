// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------

using LinqToDB.Mapping;
using System.Collections.Generic;

#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.Default.Oracle
{
	[Table("t_test_user")]
	public class TTestUser
	{
		[Column("user_id", IsPrimaryKey = true )] public decimal UserId { get; set; } // NUMBER
		[Column("name"   , CanBeNull    = false)] public string  Name   { get; set; } = null!; // VARCHAR2(255)

		#region Associations
		/// <summary>
		/// SYS_C007192 backreference
		/// </summary>
		[Association(ThisKey = nameof(UserId), OtherKey = nameof(TTestUserContract.UserId))]
		public IEnumerable<TTestUserContract> TTestUserContracts { get; set; } = null!;
		#endregion
	}
}
