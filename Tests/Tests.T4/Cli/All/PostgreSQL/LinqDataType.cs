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

namespace Cli.All.PostgreSQL
{
	[Table("LinqDataTypes")]
	public class LinqDataType
	{
		[Column("ID"            , DataType = DataType.Int32    , DbType = "integer"                        , Precision = 32, Scale = 0)] public int?      Id             { get; set; } // integer
		[Column("MoneyValue"    , DataType = DataType.Decimal  , DbType = "numeric(10,4)"                  , Precision = 10, Scale = 4)] public decimal?  MoneyValue     { get; set; } // numeric(10,4)
		[Column("DateTimeValue" , DataType = DataType.DateTime2, DbType = "timestamp (6) without time zone", Precision = 6            )] public DateTime? DateTimeValue  { get; set; } // timestamp (6) without time zone
		[Column("DateTimeValue2", DataType = DataType.DateTime2, DbType = "timestamp (6) without time zone", Precision = 6            )] public DateTime? DateTimeValue2 { get; set; } // timestamp (6) without time zone
		[Column("BoolValue"     , DataType = DataType.Boolean  , DbType = "boolean"                                                   )] public bool?     BoolValue      { get; set; } // boolean
		[Column("GuidValue"     , DataType = DataType.Guid     , DbType = "uuid"                                                      )] public Guid?     GuidValue      { get; set; } // uuid
		[Column("BinaryValue"   , DataType = DataType.Binary   , DbType = "bytea"                                                     )] public byte[]?   BinaryValue    { get; set; } // bytea
		[Column("SmallIntValue" , DataType = DataType.Int16    , DbType = "smallint"                       , Precision = 16, Scale = 0)] public short?    SmallIntValue  { get; set; } // smallint
		[Column("IntValue"      , DataType = DataType.Int32    , DbType = "integer"                        , Precision = 32, Scale = 0)] public int?      IntValue       { get; set; } // integer
		[Column("BigIntValue"   , DataType = DataType.Int64    , DbType = "bigint"                         , Precision = 64, Scale = 0)] public long?     BigIntValue    { get; set; } // bigint
		[Column("StringValue"   , DataType = DataType.NVarChar , DbType = "character varying(50)"          , Length    = 50           )] public string?   StringValue    { get; set; } // character varying(50)
	}
}
