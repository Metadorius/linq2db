// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------


#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.NoMetadata.SqlServer
{
	public class TestSchemaY
	{
		public int TestSchemaXid       { get; set; } // int
		public int ParentTestSchemaXid { get; set; } // int
		public int OtherId             { get; set; } // int

		#region Associations
		/// <summary>
		/// FK_TestSchemaY_OtherID
		/// </summary>
		public TestSchemaX TestSchemaX { get; set; } = null!;

		/// <summary>
		/// FK_TestSchemaY_ParentTestSchemaX
		/// </summary>
		public TestSchemaX ParentTestSchemaX { get; set; } = null!;
		#endregion
	}
}
