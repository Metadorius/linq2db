// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------

using LinqToDB;
using LinqToDB.Common;
using LinqToDB.Data;
using LinqToDB.Mapping;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.Fluent.Sybase
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
							IsIdentity = true,
							SkipOnInsert = true,
							SkipOnUpdate = true
						})
					.Member(e => e.BigintDataType)
						.HasAttribute(new ColumnAttribute("bigintDataType"))
					.Member(e => e.UBigintDataType)
						.HasAttribute(new ColumnAttribute("uBigintDataType"))
					.Member(e => e.NumericDataType)
						.HasAttribute(new ColumnAttribute("numericDataType"))
					.Member(e => e.BitDataType)
						.HasAttribute(new ColumnAttribute("bitDataType"))
					.Member(e => e.SmallintDataType)
						.HasAttribute(new ColumnAttribute("smallintDataType"))
					.Member(e => e.USmallintDataType)
						.HasAttribute(new ColumnAttribute("uSmallintDataType"))
					.Member(e => e.DecimalDataType)
						.HasAttribute(new ColumnAttribute("decimalDataType"))
					.Member(e => e.SmallmoneyDataType)
						.HasAttribute(new ColumnAttribute("smallmoneyDataType"))
					.Member(e => e.IntDataType)
						.HasAttribute(new ColumnAttribute("intDataType"))
					.Member(e => e.UIntDataType)
						.HasAttribute(new ColumnAttribute("uIntDataType"))
					.Member(e => e.TinyintDataType)
						.HasAttribute(new ColumnAttribute("tinyintDataType"))
					.Member(e => e.MoneyDataType)
						.HasAttribute(new ColumnAttribute("moneyDataType"))
					.Member(e => e.FloatDataType)
						.HasAttribute(new ColumnAttribute("floatDataType"))
					.Member(e => e.RealDataType)
						.HasAttribute(new ColumnAttribute("realDataType"))
					.Member(e => e.DatetimeDataType)
						.HasAttribute(new ColumnAttribute("datetimeDataType"))
					.Member(e => e.SmalldatetimeDataType)
						.HasAttribute(new ColumnAttribute("smalldatetimeDataType"))
					.Member(e => e.DateDataType)
						.HasAttribute(new ColumnAttribute("dateDataType"))
					.Member(e => e.TimeDataType)
						.HasAttribute(new ColumnAttribute("timeDataType"))
					.Member(e => e.CharDataType)
						.HasAttribute(new ColumnAttribute("charDataType"))
					.Member(e => e.Char20DataType)
						.HasAttribute(new ColumnAttribute("char20DataType"))
					.Member(e => e.VarcharDataType)
						.HasAttribute(new ColumnAttribute("varcharDataType"))
					.Member(e => e.TextDataType)
						.HasAttribute(new ColumnAttribute("textDataType"))
					.Member(e => e.NcharDataType)
						.HasAttribute(new ColumnAttribute("ncharDataType"))
					.Member(e => e.NvarcharDataType)
						.HasAttribute(new ColumnAttribute("nvarcharDataType"))
					.Member(e => e.NtextDataType)
						.HasAttribute(new ColumnAttribute("ntextDataType"))
					.Member(e => e.BinaryDataType)
						.HasAttribute(new ColumnAttribute("binaryDataType"))
					.Member(e => e.VarbinaryDataType)
						.HasAttribute(new ColumnAttribute("varbinaryDataType"))
					.Member(e => e.ImageDataType)
						.HasAttribute(new ColumnAttribute("imageDataType"))
					.Member(e => e.TimestampDataType)
						.HasAttribute(new ColumnAttribute("timestampDataType")
						{
							SkipOnInsert = true,
							SkipOnUpdate = true
						});

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
						.HasAttribute(new ColumnAttribute("Id"))
					.Member(e => e.CaseSensitive)
						.HasAttribute(new ColumnAttribute("CaseSensitive")
						{
							CanBeNull = false
						})
					.Member(e => e.CaseInsensitive)
						.HasAttribute(new ColumnAttribute("CaseInsensitive")
						{
							CanBeNull = false
						});

			builder
				.Entity<Doctor>()
					.HasAttribute(new TableAttribute("Doctor"))
					.Member(e => e.PersonId)
						.HasAttribute(new ColumnAttribute("PersonID")
						{
							IsPrimaryKey = true
						})
					.Member(e => e.Taxonomy)
						.HasAttribute(new ColumnAttribute("Taxonomy")
						{
							CanBeNull = false
						})
					.Member(e => e.Person)
						.HasAttribute(new AssociationAttribute()
						{
							CanBeNull = false,
							ThisKey = nameof(Doctor.PersonId),
							OtherKey = nameof(Person.PersonId)
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
							IsPrimaryKey = true
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
							IsPrimaryKey = true
						})
					.Member(e => e.TypeDiscriminator)
						.HasAttribute(new ColumnAttribute("TypeDiscriminator"))
					.Member(e => e.Name)
						.HasAttribute(new ColumnAttribute("Name"));

			builder
				.Entity<KeepIdentityTest>()
					.HasAttribute(new TableAttribute("KeepIdentityTest"))
					.Member(e => e.Id)
						.HasAttribute(new ColumnAttribute("ID")
						{
							IsIdentity = true,
							SkipOnInsert = true,
							SkipOnUpdate = true
						})
					.Member(e => e.Value)
						.HasAttribute(new ColumnAttribute("Value"));

			builder
				.Entity<LinqDataType>()
					.HasAttribute(new TableAttribute("LinqDataTypes"))
					.Member(e => e.Id)
						.HasAttribute(new ColumnAttribute("ID"))
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
							IsPrimaryKey = true
						})
					.Member(e => e.Diagnosis)
						.HasAttribute(new ColumnAttribute("Diagnosis")
						{
							CanBeNull = false
						})
					.Member(e => e.Person)
						.HasAttribute(new AssociationAttribute()
						{
							CanBeNull = false,
							ThisKey = nameof(Patient.PersonId),
							OtherKey = nameof(Person.PersonId)
						});

			builder
				.Entity<Person>()
					.HasAttribute(new TableAttribute("Person"))
					.Member(e => e.PersonId)
						.HasAttribute(new ColumnAttribute("PersonID")
						{
							IsPrimaryKey = true,
							IsIdentity = true,
							SkipOnInsert = true,
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
						.HasAttribute(new ColumnAttribute("Gender"))
					.Member(e => e.Doctor)
						.HasAttribute(new AssociationAttribute()
						{
							ThisKey = nameof(Person.PersonId),
							OtherKey = nameof(Doctor.PersonId)
						})
					.Member(e => e.Patient)
						.HasAttribute(new AssociationAttribute()
						{
							ThisKey = nameof(Person.PersonId),
							OtherKey = nameof(Patient.PersonId)
						});

			builder
				.Entity<TestIdentity>()
					.HasAttribute(new TableAttribute("TestIdentity"))
					.Member(e => e.Id)
						.HasAttribute(new ColumnAttribute("ID")
						{
							IsPrimaryKey = true,
							IsIdentity = true,
							SkipOnInsert = true,
							SkipOnUpdate = true
						});

			builder
				.Entity<TestMerge1>()
					.HasAttribute(new TableAttribute("TestMerge1"))
					.Member(e => e.Id)
						.HasAttribute(new ColumnAttribute("Id")
						{
							IsPrimaryKey = true
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
							IsPrimaryKey = true
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
				.Entity<TestMergeIdentity>()
					.HasAttribute(new TableAttribute("TestMergeIdentity"))
					.Member(e => e.Id)
						.HasAttribute(new ColumnAttribute("Id")
						{
							IsPrimaryKey = true,
							IsIdentity = true,
							SkipOnInsert = true,
							SkipOnUpdate = true
						})
					.Member(e => e.Field)
						.HasAttribute(new ColumnAttribute("Field"));

			builder
				.Entity<Sysquerymetric>()
					.HasAttribute(new TableAttribute("sysquerymetrics")
					{
						IsView = true
					})
					.Member(e => e.Uid)
						.HasAttribute(new ColumnAttribute("uid"))
					.Member(e => e.Gid)
						.HasAttribute(new ColumnAttribute("gid"))
					.Member(e => e.Hashkey)
						.HasAttribute(new ColumnAttribute("hashkey"))
					.Member(e => e.Id)
						.HasAttribute(new ColumnAttribute("id"))
					.Member(e => e.Sequence)
						.HasAttribute(new ColumnAttribute("sequence"))
					.Member(e => e.ExecMin)
						.HasAttribute(new ColumnAttribute("exec_min"))
					.Member(e => e.ExecMax)
						.HasAttribute(new ColumnAttribute("exec_max"))
					.Member(e => e.ExecAvg)
						.HasAttribute(new ColumnAttribute("exec_avg"))
					.Member(e => e.ElapMin)
						.HasAttribute(new ColumnAttribute("elap_min"))
					.Member(e => e.ElapMax)
						.HasAttribute(new ColumnAttribute("elap_max"))
					.Member(e => e.ElapAvg)
						.HasAttribute(new ColumnAttribute("elap_avg"))
					.Member(e => e.LioMin)
						.HasAttribute(new ColumnAttribute("lio_min"))
					.Member(e => e.LioMax)
						.HasAttribute(new ColumnAttribute("lio_max"))
					.Member(e => e.LioAvg)
						.HasAttribute(new ColumnAttribute("lio_avg"))
					.Member(e => e.PioMin)
						.HasAttribute(new ColumnAttribute("pio_min"))
					.Member(e => e.PioMax)
						.HasAttribute(new ColumnAttribute("pio_max"))
					.Member(e => e.PioAvg)
						.HasAttribute(new ColumnAttribute("pio_avg"))
					.Member(e => e.Cnt)
						.HasAttribute(new ColumnAttribute("cnt"))
					.Member(e => e.AbortCnt)
						.HasAttribute(new ColumnAttribute("abort_cnt"))
					.Member(e => e.Qtext)
						.HasAttribute(new ColumnAttribute("qtext"));

			builder
				.Entity<ExtensionMethods.PersonSelectAllResult>()
					.Member(e => e.PersonId)
						.HasAttribute(new ColumnAttribute("PersonID"))
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

			builder.HasAttribute<Doctor>(e => ExtensionMethods.Person(e, default(IDataContext)!), new AssociationAttribute()
			{
				CanBeNull = false,
				ThisKey = nameof(Doctor.PersonId),
				OtherKey = nameof(Person.PersonId)
			});

			builder.HasAttribute<Person>(e => ExtensionMethods.Doctor(e, default(IDataContext)!), new AssociationAttribute()
			{
				ThisKey = nameof(Person.PersonId),
				OtherKey = nameof(Doctor.PersonId)
			});

			builder.HasAttribute<Patient>(e => ExtensionMethods.Person(e, default(IDataContext)!), new AssociationAttribute()
			{
				CanBeNull = false,
				ThisKey = nameof(Patient.PersonId),
				OtherKey = nameof(Person.PersonId)
			});

			builder.HasAttribute<Person>(e => ExtensionMethods.Patient(e, default(IDataContext)!), new AssociationAttribute()
			{
				ThisKey = nameof(Person.PersonId),
				OtherKey = nameof(Patient.PersonId)
			});

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
		public ITable<KeepIdentityTest>  KeepIdentityTests   => this.GetTable<KeepIdentityTest>();
		public ITable<LinqDataType>      LinqDataTypes       => this.GetTable<LinqDataType>();
		public ITable<Parent>            Parents             => this.GetTable<Parent>();
		public ITable<Patient>           Patients            => this.GetTable<Patient>();
		public ITable<Person>            People              => this.GetTable<Person>();
		public ITable<TestIdentity>      TestIdentities      => this.GetTable<TestIdentity>();
		public ITable<TestMerge1>        TestMerge1          => this.GetTable<TestMerge1>();
		public ITable<TestMerge2>        TestMerge2          => this.GetTable<TestMerge2>();
		public ITable<TestMergeIdentity> TestMergeIdentities => this.GetTable<TestMergeIdentity>();
		public ITable<Sysquerymetric>    Sysquerymetrics     => this.GetTable<Sysquerymetric>();
	}

	public static partial class ExtensionMethods
	{
		#region Associations
		#region Doctor Associations
		/// <summary>
		/// FK_Doctor_Person
		/// </summary>
		public static Person Person(this Doctor obj, IDataContext db)
		{
			return db.GetTable<Person>().First(t => obj.PersonId == t.PersonId);
		}
		#endregion

		#region Person Associations
		/// <summary>
		/// FK_Doctor_Person backreference
		/// </summary>
		public static Doctor? Doctor(this Person obj, IDataContext db)
		{
			return db.GetTable<Doctor>().FirstOrDefault(t => t.PersonId == obj.PersonId);
		}

		/// <summary>
		/// FK_Patient_Person backreference
		/// </summary>
		public static Patient? Patient(this Person obj, IDataContext db)
		{
			return db.GetTable<Patient>().FirstOrDefault(t => t.PersonId == obj.PersonId);
		}
		#endregion

		#region Patient Associations
		/// <summary>
		/// FK_Patient_Person
		/// </summary>
		public static Person Person(this Patient obj, IDataContext db)
		{
			return db.GetTable<Person>().First(t => obj.PersonId == t.PersonId);
		}
		#endregion
		#endregion

		#region Stored Procedures
		#region AddIssue792Record
		public static int AddIssue792Record(this TestDataDB dataConnection, out int? returnValue)
		{
			var parameters = new []
			{
				new DataParameter("RETURN_VALUE", null, DataType.Int32)
				{
					Direction = ParameterDirection.ReturnValue,
					Size = 10
				}
			};
			var ret = dataConnection.ExecuteProc("[AddIssue792Record]", parameters);
			returnValue = Converter.ChangeTypeTo<int?>(parameters[0].Value);
			return ret;
		}

		public static async Task<AddIssue792RecordResults> AddIssue792RecordAsync(this TestDataDB dataConnection, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("RETURN_VALUE", null, DataType.Int32)
				{
					Direction = ParameterDirection.ReturnValue,
					Size = 10
				}
			};
			var result = await dataConnection.ExecuteProcAsync("[AddIssue792Record]", cancellationToken, parameters);
			return new AddIssue792RecordResults()
			{
				Result = result,
				ReturnValue = Converter.ChangeTypeTo<int?>(parameters[0].Value)
			};
		}

		public class AddIssue792RecordResults
		{
			public int  Result      { get; set; }
			public int? ReturnValue { get; set; }
		}
		#endregion

		#region PersonSelectAll
		public static IEnumerable<PersonSelectAllResult> PersonSelectAll(this TestDataDB dataConnection, out int? returnValue)
		{
			var parameters = new []
			{
				new DataParameter("RETURN_VALUE", null, DataType.Int32)
				{
					Direction = ParameterDirection.ReturnValue,
					Size = 10
				}
			};
			var ret = dataConnection.QueryProc<PersonSelectAllResult>("[Person_SelectAll]", parameters).ToList();
			returnValue = Converter.ChangeTypeTo<int?>(parameters[0].Value);
			return ret;
		}

		public static async Task<PersonSelectAllResults> PersonSelectAllAsync(this TestDataDB dataConnection, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("RETURN_VALUE", null, DataType.Int32)
				{
					Direction = ParameterDirection.ReturnValue,
					Size = 10
				}
			};
			var result = await dataConnection.QueryProcAsync<PersonSelectAllResult>("[Person_SelectAll]", cancellationToken, parameters);
			return new PersonSelectAllResults()
			{
				Result = result.ToList(),
				ReturnValue = Converter.ChangeTypeTo<int?>(parameters[0].Value)
			};
		}

		public partial class PersonSelectAllResult
		{
			public int     PersonId   { get; set; }
			public string  FirstName  { get; set; } = null!;
			public string  LastName   { get; set; } = null!;
			public string? MiddleName { get; set; }
			public string  Gender     { get; set; } = null!;
		}

		public class PersonSelectAllResults
		{
			public IEnumerable<PersonSelectAllResult> Result      { get; set; } = null!;
			public int?                               ReturnValue { get; set; }
		}
		#endregion
		#endregion
	}
}
