// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------


#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.NoMetadata.SqlServerNorthwind
{
	public class CustomerCustomerDemo
	{
		public string CustomerId     { get; set; } = null!; // nchar(5)
		public string CustomerTypeId { get; set; } = null!; // nchar(10)

		#region Associations
		/// <summary>
		/// FK_CustomerCustomerDemo
		/// </summary>
		public CustomerDemographic FkCustomerCustomerDemo { get; set; } = null!;

		/// <summary>
		/// FK_CustomerCustomerDemo_Customers
		/// </summary>
		public Customer Customers { get; set; } = null!;
		#endregion
	}
}
