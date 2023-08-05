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
	[Table("Invoices", IsView = true)]
	public class Invoice
	{
		[Column("ShipName"      , DataType = DataType.NVarChar, DbType = "nvarchar(40)", Length = 40)] public SqlString?   ShipName       { get; set; } // nvarchar(40)
		[Column("ShipAddress"   , DataType = DataType.NVarChar, DbType = "nvarchar(60)", Length = 60)] public SqlString?   ShipAddress    { get; set; } // nvarchar(60)
		[Column("ShipCity"      , DataType = DataType.NVarChar, DbType = "nvarchar(15)", Length = 15)] public SqlString?   ShipCity       { get; set; } // nvarchar(15)
		[Column("ShipRegion"    , DataType = DataType.NVarChar, DbType = "nvarchar(15)", Length = 15)] public SqlString?   ShipRegion     { get; set; } // nvarchar(15)
		[Column("ShipPostalCode", DataType = DataType.NVarChar, DbType = "nvarchar(10)", Length = 10)] public SqlString?   ShipPostalCode { get; set; } // nvarchar(10)
		[Column("ShipCountry"   , DataType = DataType.NVarChar, DbType = "nvarchar(15)", Length = 15)] public SqlString?   ShipCountry    { get; set; } // nvarchar(15)
		[Column("CustomerID"    , DataType = DataType.NChar   , DbType = "nchar(5)"    , Length = 5 )] public SqlString?   CustomerId     { get; set; } // nchar(5)
		[Column("CustomerName"  , DataType = DataType.NVarChar, DbType = "nvarchar(40)", Length = 40)] public SqlString    CustomerName   { get; set; } // nvarchar(40)
		[Column("Address"       , DataType = DataType.NVarChar, DbType = "nvarchar(60)", Length = 60)] public SqlString?   Address        { get; set; } // nvarchar(60)
		[Column("City"          , DataType = DataType.NVarChar, DbType = "nvarchar(15)", Length = 15)] public SqlString?   City           { get; set; } // nvarchar(15)
		[Column("Region"        , DataType = DataType.NVarChar, DbType = "nvarchar(15)", Length = 15)] public SqlString?   Region         { get; set; } // nvarchar(15)
		[Column("PostalCode"    , DataType = DataType.NVarChar, DbType = "nvarchar(10)", Length = 10)] public SqlString?   PostalCode     { get; set; } // nvarchar(10)
		[Column("Country"       , DataType = DataType.NVarChar, DbType = "nvarchar(15)", Length = 15)] public SqlString?   Country        { get; set; } // nvarchar(15)
		[Column("Salesperson"   , DataType = DataType.NVarChar, DbType = "nvarchar(31)", Length = 31)] public SqlString    Salesperson    { get; set; } // nvarchar(31)
		[Column("OrderID"       , DataType = DataType.Int32   , DbType = "int"                      )] public SqlInt32     OrderId        { get; set; } // int
		[Column("OrderDate"     , DataType = DataType.DateTime, DbType = "datetime"                 )] public SqlDateTime? OrderDate      { get; set; } // datetime
		[Column("RequiredDate"  , DataType = DataType.DateTime, DbType = "datetime"                 )] public SqlDateTime? RequiredDate   { get; set; } // datetime
		[Column("ShippedDate"   , DataType = DataType.DateTime, DbType = "datetime"                 )] public SqlDateTime? ShippedDate    { get; set; } // datetime
		[Column("ShipperName"   , DataType = DataType.NVarChar, DbType = "nvarchar(40)", Length = 40)] public SqlString    ShipperName    { get; set; } // nvarchar(40)
		[Column("ProductID"     , DataType = DataType.Int32   , DbType = "int"                      )] public SqlInt32     ProductId      { get; set; } // int
		[Column("ProductName"   , DataType = DataType.NVarChar, DbType = "nvarchar(40)", Length = 40)] public SqlString    ProductName    { get; set; } // nvarchar(40)
		[Column("UnitPrice"     , DataType = DataType.Money   , DbType = "money"                    )] public SqlMoney     UnitPrice      { get; set; } // money
		[Column("Quantity"      , DataType = DataType.Int16   , DbType = "smallint"                 )] public SqlInt16     Quantity       { get; set; } // smallint
		[Column("Discount"      , DataType = DataType.Single  , DbType = "real"                     )] public SqlSingle    Discount       { get; set; } // real
		[Column("ExtendedPrice" , DataType = DataType.Money   , DbType = "money"                    )] public SqlMoney?    ExtendedPrice  { get; set; } // money
		[Column("Freight"       , DataType = DataType.Money   , DbType = "money"                    )] public SqlMoney?    Freight        { get; set; } // money
	}
}
