// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------


#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.NoMetadata.Firebird
{
	public class Patient
	{
		public int    PersonId  { get; set; } // integer
		public string Diagnosis { get; set; } = null!; // varchar(256)

		#region Associations
		/// <summary>
		/// INTEG_52
		/// </summary>
		public Person Integ { get; set; } = null!;
		#endregion
	}
}
