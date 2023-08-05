// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------

using LinqToDB;
using LinqToDB.Mapping;
using System;

#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.All.Access.Odbc
{
	[Table("LinqDataTypes")]
	public class LinqDataType
	{
		[Column("ID"            , DataType = DataType.Int32   , DbType = "INTEGER"                                  )] public int?      Id             { get; set; } // INTEGER
		[Column("MoneyValue"    , DataType = DataType.Decimal , DbType = "DECIMAL(10, 4)", Precision = 10, Scale = 4)] public decimal?  MoneyValue     { get; set; } // DECIMAL(10, 4)
		[Column("DateTimeValue" , DataType = DataType.DateTime, DbType = "DATETIME"                                 )] public DateTime? DateTimeValue  { get; set; } // DATETIME
		[Column("DateTimeValue2", DataType = DataType.DateTime, DbType = "DATETIME"                                 )] public DateTime? DateTimeValue2 { get; set; } // DATETIME
		[Column("BoolValue"     , DataType = DataType.Boolean , DbType = "BIT"                                      )] public bool      BoolValue      { get; set; } // BIT
		[Column("GuidValue"     , DataType = DataType.Guid    , DbType = "GUID"                                     )] public Guid?     GuidValue      { get; set; } // GUID
		[Column("BinaryValue"   , DataType = DataType.Image   , DbType = "LONGBINARY"                               )] public byte[]?   BinaryValue    { get; set; } // LONGBINARY
		[Column("SmallIntValue" , DataType = DataType.Int16   , DbType = "SMALLINT"                                 )] public short?    SmallIntValue  { get; set; } // SMALLINT
		[Column("IntValue"      , DataType = DataType.Int32   , DbType = "INTEGER"                                  )] public int?      IntValue       { get; set; } // INTEGER
		[Column("BigIntValue"   , DataType = DataType.Int32   , DbType = "INTEGER"                                  )] public int?      BigIntValue    { get; set; } // INTEGER
		[Column("StringValue"   , DataType = DataType.VarChar , DbType = "VARCHAR(50)"   , Length    = 50           )] public string?   StringValue    { get; set; } // VARCHAR(50)
	}
}
