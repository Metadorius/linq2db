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

namespace Cli.All.Sybase
{
	[Table("CollatedTable")]
	public class CollatedTable
	{
		[Column("Id"             , DataType  = DataType.Int32, DbType   = "int"            , Length = 4                          )] public int    Id              { get; set; } // int
		[Column("CaseSensitive"  , CanBeNull = false         , DataType = DataType.NVarChar, DbType = "nvarchar(20)", Length = 20)] public string CaseSensitive   { get; set; } = null!; // nvarchar(20)
		[Column("CaseInsensitive", CanBeNull = false         , DataType = DataType.NVarChar, DbType = "nvarchar(20)", Length = 20)] public string CaseInsensitive { get; set; } = null!; // nvarchar(20)
	}
}
