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

namespace Cli.All.MySql
{
	/// <summary>
	/// VIEW
	/// </summary>
	[Table("PersonView", IsView = true)]
	public class PersonView
	{
		[Column("ID", DataType = DataType.Int32, DbType = "int", Precision = 10, Scale = 0)] public int Id { get; set; } // int
	}
}
