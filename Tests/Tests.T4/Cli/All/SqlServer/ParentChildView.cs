// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------

using LinqToDB;
using LinqToDB.Mapping;
using System.Data.SqlTypes;

#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.All.SqlServer
{
	[Table("ParentChildView", IsView = true)]
	public class ParentChildView
	{
		[Column("ParentID", DataType = LinqToDB.DataType.Int32, DbType = "int")] public SqlInt32? ParentId { get; set; } // int
		[Column("Value1"  , DataType = LinqToDB.DataType.Int32, DbType = "int")] public SqlInt32? Value1   { get; set; } // int
		[Column("ChildID" , DataType = LinqToDB.DataType.Int32, DbType = "int")] public SqlInt32? ChildId  { get; set; } // int
	}
}
