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

namespace Cli.All.Informix
{
	[Table("collatedtable")]
	public class Collatedtable
	{
		[Column("id"             , DataType  = DataType.Int32, DbType   = "INTEGER"        , Length = 0                          )] public int    Id              { get; set; } // INTEGER
		[Column("casesensitive"  , CanBeNull = false         , DataType = DataType.VarChar , DbType = "VARCHAR(20)" , Length = 20)] public string Casesensitive   { get; set; } = null!; // VARCHAR(20)
		[Column("caseinsensitive", CanBeNull = false         , DataType = DataType.NVarChar, DbType = "NVARCHAR(20)", Length = 20)] public string Caseinsensitive { get; set; } = null!; // NVARCHAR(20)
	}
}
