// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------

using LinqToDB;
using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.All.Firebird
{
	[Table("PersonView", IsView = true)]
	public class PersonView
	{
		[Column("PersonID"  , DataType = DataType.Int32   , DbType = "integer"                 )] public int?    PersonId   { get; set; } // integer
		[Column("FirstName" , DataType = DataType.NVarChar, DbType = "varchar(50)", Length = 50)] public string? FirstName  { get; set; } // varchar(50)
		[Column("LastName"  , DataType = DataType.NVarChar, DbType = "varchar(50)", Length = 50)] public string? LastName   { get; set; } // varchar(50)
		[Column("MiddleName", DataType = DataType.NVarChar, DbType = "varchar(50)", Length = 50)] public string? MiddleName { get; set; } // varchar(50)
		[Column("Gender"    , DataType = DataType.NChar   , DbType = "char(1)"    , Length = 1 )] public char?   Gender     { get; set; } // char(1)
	}
}
