// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------

using LinqToDB;

#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.NoMetadata.PostgreSQL
{
	public static partial class TestSchemaSchema
	{
		public partial class DataContext
		{
			private readonly IDataContext _dataContext;

			public ITable<TestSchemaIdentity> TestSchemaIdentities => _dataContext.GetTable<TestSchemaIdentity>();
			public ITable<Testsamename>       Testsamenames        => _dataContext.GetTable<Testsamename>();
			public ITable<Testserialidentity> Testserialidentities => _dataContext.GetTable<Testserialidentity>();

			public DataContext(IDataContext dataContext)
			{
				_dataContext = dataContext;
			}
		}

		public class TestSchemaIdentity
		{
			public int Id { get; set; } // integer
		}

		public class Testsamename
		{
			public int Id { get; set; } // integer
		}

		public class Testserialidentity
		{
			public int Id { get; set; } // integer
		}
	}
}
