// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------

using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.Fluent.ClickHouse.Client
{
	public partial class TestDataDB : DataConnection
	{
		static TestDataDB()
		{
			var builder = new FluentMappingBuilder(ContextSchema);

			builder
				.Entity<AllType>()
					.HasAttribute(new TableAttribute("AllTypes"))
					.Member(e => e.Id)
						.HasAttribute(new ColumnAttribute("ID")
						{
							IsPrimaryKey = true,
							SkipOnUpdate = true
						})
					.Member(e => e.IntDataType)
						.HasAttribute(new ColumnAttribute("intDataType"))
					.Member(e => e.SmallintDataType)
						.HasAttribute(new ColumnAttribute("smallintDataType"))
					.Member(e => e.FloatDataType)
						.HasAttribute(new ColumnAttribute("floatDataType"))
					.Member(e => e.DoubleDataType)
						.HasAttribute(new ColumnAttribute("doubleDataType"))
					.Member(e => e.NcharDataType)
						.HasAttribute(new ColumnAttribute("ncharDataType"))
					.Member(e => e.Char20DataType)
						.HasAttribute(new ColumnAttribute("char20DataType"))
					.Member(e => e.VarcharDataType)
						.HasAttribute(new ColumnAttribute("varcharDataType"))
					.Member(e => e.CharDataType)
						.HasAttribute(new ColumnAttribute("charDataType"));

			builder
				.Entity<Child>()
					.HasAttribute(new TableAttribute("Child"))
					.Member(e => e.ParentId)
						.HasAttribute(new ColumnAttribute("ParentID"))
					.Member(e => e.ChildId)
						.HasAttribute(new ColumnAttribute("ChildID"));

			builder
				.Entity<CollatedTable>()
					.HasAttribute(new TableAttribute("CollatedTable"))
					.Member(e => e.Id)
						.HasAttribute(new ColumnAttribute("Id")
						{
							IsPrimaryKey = true,
							SkipOnUpdate = true
						})
					.Member(e => e.CaseSensitive)
						.HasAttribute(new ColumnAttribute("CaseSensitive"))
					.Member(e => e.CaseInsensitive)
						.HasAttribute(new ColumnAttribute("CaseInsensitive"));

			builder
				.Entity<Doctor>()
					.HasAttribute(new TableAttribute("Doctor"))
					.Member(e => e.PersonId)
						.HasAttribute(new ColumnAttribute("PersonID")
						{
							IsPrimaryKey = true,
							SkipOnUpdate = true
						})
					.Member(e => e.Taxonomy)
						.HasAttribute(new ColumnAttribute("Taxonomy")
						{
							CanBeNull = false
						});

			builder
				.Entity<GrandChild>()
					.HasAttribute(new TableAttribute("GrandChild"))
					.Member(e => e.ParentId)
						.HasAttribute(new ColumnAttribute("ParentID"))
					.Member(e => e.ChildId)
						.HasAttribute(new ColumnAttribute("ChildID"))
					.Member(e => e.GrandChildId)
						.HasAttribute(new ColumnAttribute("GrandChildID"));

			builder
				.Entity<InheritanceChild>()
					.HasAttribute(new TableAttribute("InheritanceChild"))
					.Member(e => e.InheritanceChildId)
						.HasAttribute(new ColumnAttribute("InheritanceChildId")
						{
							IsPrimaryKey = true,
							SkipOnUpdate = true
						})
					.Member(e => e.InheritanceParentId)
						.HasAttribute(new ColumnAttribute("InheritanceParentId"))
					.Member(e => e.TypeDiscriminator)
						.HasAttribute(new ColumnAttribute("TypeDiscriminator"))
					.Member(e => e.Name)
						.HasAttribute(new ColumnAttribute("Name"));

			builder
				.Entity<InheritanceParent>()
					.HasAttribute(new TableAttribute("InheritanceParent"))
					.Member(e => e.InheritanceParentId)
						.HasAttribute(new ColumnAttribute("InheritanceParentId")
						{
							IsPrimaryKey = true,
							SkipOnUpdate = true
						})
					.Member(e => e.TypeDiscriminator)
						.HasAttribute(new ColumnAttribute("TypeDiscriminator"))
					.Member(e => e.Name)
						.HasAttribute(new ColumnAttribute("Name"));

			builder
				.Entity<LinqDataType>()
					.HasAttribute(new TableAttribute("LinqDataTypes"))
					.Member(e => e.Id)
						.HasAttribute(new ColumnAttribute("ID")
						{
							IsPrimaryKey = true,
							SkipOnUpdate = true
						})
					.Member(e => e.MoneyValue)
						.HasAttribute(new ColumnAttribute("MoneyValue"))
					.Member(e => e.DateTimeValue)
						.HasAttribute(new ColumnAttribute("DateTimeValue"))
					.Member(e => e.DateTimeValue2)
						.HasAttribute(new ColumnAttribute("DateTimeValue2"))
					.Member(e => e.BoolValue)
						.HasAttribute(new ColumnAttribute("BoolValue"))
					.Member(e => e.GuidValue)
						.HasAttribute(new ColumnAttribute("GuidValue"))
					.Member(e => e.BinaryValue)
						.HasAttribute(new ColumnAttribute("BinaryValue"))
					.Member(e => e.SmallIntValue)
						.HasAttribute(new ColumnAttribute("SmallIntValue"))
					.Member(e => e.IntValue)
						.HasAttribute(new ColumnAttribute("IntValue"))
					.Member(e => e.BigIntValue)
						.HasAttribute(new ColumnAttribute("BigIntValue"))
					.Member(e => e.StringValue)
						.HasAttribute(new ColumnAttribute("StringValue"));

			builder
				.Entity<Parent>()
					.HasAttribute(new TableAttribute("Parent"))
					.Member(e => e.ParentId)
						.HasAttribute(new ColumnAttribute("ParentID"))
					.Member(e => e.Value1)
						.HasAttribute(new ColumnAttribute("Value1"));

			builder
				.Entity<Patient>()
					.HasAttribute(new TableAttribute("Patient"))
					.Member(e => e.PersonId)
						.HasAttribute(new ColumnAttribute("PersonID")
						{
							IsPrimaryKey = true,
							SkipOnUpdate = true
						})
					.Member(e => e.Diagnosis)
						.HasAttribute(new ColumnAttribute("Diagnosis")
						{
							CanBeNull = false
						});

			builder
				.Entity<Person>()
					.HasAttribute(new TableAttribute("Person"))
					.Member(e => e.PersonId)
						.HasAttribute(new ColumnAttribute("PersonID")
						{
							IsPrimaryKey = true,
							SkipOnUpdate = true
						})
					.Member(e => e.FirstName)
						.HasAttribute(new ColumnAttribute("FirstName")
						{
							CanBeNull = false
						})
					.Member(e => e.LastName)
						.HasAttribute(new ColumnAttribute("LastName")
						{
							CanBeNull = false
						})
					.Member(e => e.MiddleName)
						.HasAttribute(new ColumnAttribute("MiddleName"))
					.Member(e => e.Gender)
						.HasAttribute(new ColumnAttribute("Gender")
						{
							CanBeNull = false
						});

			builder
				.Entity<TestMerge1>()
					.HasAttribute(new TableAttribute("TestMerge1"))
					.Member(e => e.Id)
						.HasAttribute(new ColumnAttribute("Id")
						{
							IsPrimaryKey = true,
							SkipOnUpdate = true
						})
					.Member(e => e.Field1)
						.HasAttribute(new ColumnAttribute("Field1"))
					.Member(e => e.Field2)
						.HasAttribute(new ColumnAttribute("Field2"))
					.Member(e => e.Field3)
						.HasAttribute(new ColumnAttribute("Field3"))
					.Member(e => e.Field4)
						.HasAttribute(new ColumnAttribute("Field4"))
					.Member(e => e.Field5)
						.HasAttribute(new ColumnAttribute("Field5"))
					.Member(e => e.FieldInt64)
						.HasAttribute(new ColumnAttribute("FieldInt64"))
					.Member(e => e.FieldBoolean)
						.HasAttribute(new ColumnAttribute("FieldBoolean"))
					.Member(e => e.FieldString)
						.HasAttribute(new ColumnAttribute("FieldString"))
					.Member(e => e.FieldNString)
						.HasAttribute(new ColumnAttribute("FieldNString"))
					.Member(e => e.FieldChar)
						.HasAttribute(new ColumnAttribute("FieldChar"))
					.Member(e => e.FieldNChar)
						.HasAttribute(new ColumnAttribute("FieldNChar"))
					.Member(e => e.FieldFloat)
						.HasAttribute(new ColumnAttribute("FieldFloat"))
					.Member(e => e.FieldDouble)
						.HasAttribute(new ColumnAttribute("FieldDouble"))
					.Member(e => e.FieldDateTime)
						.HasAttribute(new ColumnAttribute("FieldDateTime"))
					.Member(e => e.FieldDateTime2)
						.HasAttribute(new ColumnAttribute("FieldDateTime2"))
					.Member(e => e.FieldBinary)
						.HasAttribute(new ColumnAttribute("FieldBinary"))
					.Member(e => e.FieldGuid)
						.HasAttribute(new ColumnAttribute("FieldGuid"))
					.Member(e => e.FieldDecimal)
						.HasAttribute(new ColumnAttribute("FieldDecimal"))
					.Member(e => e.FieldDate)
						.HasAttribute(new ColumnAttribute("FieldDate"))
					.Member(e => e.FieldTime)
						.HasAttribute(new ColumnAttribute("FieldTime"))
					.Member(e => e.FieldEnumString)
						.HasAttribute(new ColumnAttribute("FieldEnumString"))
					.Member(e => e.FieldEnumNumber)
						.HasAttribute(new ColumnAttribute("FieldEnumNumber"));

			builder
				.Entity<TestMerge2>()
					.HasAttribute(new TableAttribute("TestMerge2"))
					.Member(e => e.Id)
						.HasAttribute(new ColumnAttribute("Id")
						{
							IsPrimaryKey = true,
							SkipOnUpdate = true
						})
					.Member(e => e.Field1)
						.HasAttribute(new ColumnAttribute("Field1"))
					.Member(e => e.Field2)
						.HasAttribute(new ColumnAttribute("Field2"))
					.Member(e => e.Field3)
						.HasAttribute(new ColumnAttribute("Field3"))
					.Member(e => e.Field4)
						.HasAttribute(new ColumnAttribute("Field4"))
					.Member(e => e.Field5)
						.HasAttribute(new ColumnAttribute("Field5"))
					.Member(e => e.FieldInt64)
						.HasAttribute(new ColumnAttribute("FieldInt64"))
					.Member(e => e.FieldBoolean)
						.HasAttribute(new ColumnAttribute("FieldBoolean"))
					.Member(e => e.FieldString)
						.HasAttribute(new ColumnAttribute("FieldString"))
					.Member(e => e.FieldNString)
						.HasAttribute(new ColumnAttribute("FieldNString"))
					.Member(e => e.FieldChar)
						.HasAttribute(new ColumnAttribute("FieldChar"))
					.Member(e => e.FieldNChar)
						.HasAttribute(new ColumnAttribute("FieldNChar"))
					.Member(e => e.FieldFloat)
						.HasAttribute(new ColumnAttribute("FieldFloat"))
					.Member(e => e.FieldDouble)
						.HasAttribute(new ColumnAttribute("FieldDouble"))
					.Member(e => e.FieldDateTime)
						.HasAttribute(new ColumnAttribute("FieldDateTime"))
					.Member(e => e.FieldDateTime2)
						.HasAttribute(new ColumnAttribute("FieldDateTime2"))
					.Member(e => e.FieldBinary)
						.HasAttribute(new ColumnAttribute("FieldBinary"))
					.Member(e => e.FieldGuid)
						.HasAttribute(new ColumnAttribute("FieldGuid"))
					.Member(e => e.FieldDecimal)
						.HasAttribute(new ColumnAttribute("FieldDecimal"))
					.Member(e => e.FieldDate)
						.HasAttribute(new ColumnAttribute("FieldDate"))
					.Member(e => e.FieldTime)
						.HasAttribute(new ColumnAttribute("FieldTime"))
					.Member(e => e.FieldEnumString)
						.HasAttribute(new ColumnAttribute("FieldEnumString"))
					.Member(e => e.FieldEnumNumber)
						.HasAttribute(new ColumnAttribute("FieldEnumNumber"));

			builder.Build();
		}
		public static MappingSchema ContextSchema { get; } = new MappingSchema();

		public TestDataDB()
			: base(new DataOptions().UseMappingSchema(ContextSchema))
		{
			InitDataContext();
		}

		public TestDataDB(string configuration)
			: base(new DataOptions().UseConfiguration(configuration, ContextSchema))
		{
			InitDataContext();
		}

		public TestDataDB(DataOptions<TestDataDB> options)
			: base(options.Options.UseMappingSchema(options.Options.ConnectionOptions.MappingSchema == null ? ContextSchema : MappingSchema.CombineSchemas(options.Options.ConnectionOptions.MappingSchema, ContextSchema)))
		{
			InitDataContext();
		}

		partial void InitDataContext();

		public ITable<AllType>           AllTypes            => this.GetTable<AllType>();
		public ITable<Child>             Children            => this.GetTable<Child>();
		public ITable<CollatedTable>     CollatedTables      => this.GetTable<CollatedTable>();
		public ITable<Doctor>            Doctors             => this.GetTable<Doctor>();
		public ITable<GrandChild>        GrandChildren       => this.GetTable<GrandChild>();
		public ITable<InheritanceChild>  InheritanceChildren => this.GetTable<InheritanceChild>();
		public ITable<InheritanceParent> InheritanceParents  => this.GetTable<InheritanceParent>();
		public ITable<LinqDataType>      LinqDataTypes       => this.GetTable<LinqDataType>();
		public ITable<Parent>            Parents             => this.GetTable<Parent>();
		public ITable<Patient>           Patients            => this.GetTable<Patient>();
		public ITable<Person>            People              => this.GetTable<Person>();
		public ITable<TestMerge1>        TestMerge1          => this.GetTable<TestMerge1>();
		public ITable<TestMerge2>        TestMerge2          => this.GetTable<TestMerge2>();
	}
}
