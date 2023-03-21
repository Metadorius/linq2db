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

namespace Cli.All.SqlServerNorthwind
{
	[Table("Order Details Extended", IsView = true)]
	public class OrderDetailsExtended
	{
		[Column("OrderID"      , DataType = DataType.Int32   , DbType = "int"                      )] public SqlInt32  OrderId       { get; set; } // int
		[Column("ProductID"    , DataType = DataType.Int32   , DbType = "int"                      )] public SqlInt32  ProductId     { get; set; } // int
		[Column("ProductName"  , DataType = DataType.NVarChar, DbType = "nvarchar(40)", Length = 40)] public SqlString ProductName   { get; set; } // nvarchar(40)
		[Column("UnitPrice"    , DataType = DataType.Money   , DbType = "money"                    )] public SqlMoney  UnitPrice     { get; set; } // money
		[Column("Quantity"     , DataType = DataType.Int16   , DbType = "smallint"                 )] public SqlInt16  Quantity      { get; set; } // smallint
		[Column("Discount"     , DataType = DataType.Single  , DbType = "real"                     )] public SqlSingle Discount      { get; set; } // real
		[Column("ExtendedPrice", DataType = DataType.Money   , DbType = "money"                    )] public SqlMoney? ExtendedPrice { get; set; } // money
	}
}
