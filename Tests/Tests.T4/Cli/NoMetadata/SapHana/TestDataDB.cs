// ---------------------------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by LinqToDB scaffolding tool (https://github.com/linq2db/linq2db).
// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ---------------------------------------------------------------------------------------------------

using LinqToDB;
using LinqToDB.Common;
using LinqToDB.Data;
using LinqToDB.Expressions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable 1573, 1591
#nullable enable

namespace Cli.NoMetadata.SapHana
{
	public partial class TestDataDB : DataConnection
	{
		public TestDataDB()
		{
		}

		public TestDataDB(string configuration)
			: base(configuration)
		{
		}

		public TestDataDB(DataOptions<TestDataDB> options)
			: base(options.Options)
		{
		}

		public ITable<AllType>                   AllTypes                   => this.GetTable<AllType>();
		public ITable<AllTypesGeo>               AllTypesGeos               => this.GetTable<AllTypesGeo>();
		public ITable<BulkInsertLowerCaseColumn> BulkInsertLowerCaseColumns => this.GetTable<BulkInsertLowerCaseColumn>();
		public ITable<BulkInsertUpperCaseColumn> BulkInsertUpperCaseColumns => this.GetTable<BulkInsertUpperCaseColumn>();
		public ITable<Child>                     Children                   => this.GetTable<Child>();
		public ITable<CollatedTable>             CollatedTables             => this.GetTable<CollatedTable>();
		public ITable<Doctor>                    Doctors                    => this.GetTable<Doctor>();
		public ITable<GrandChild>                GrandChildren              => this.GetTable<GrandChild>();
		public ITable<IndexTable>                IndexTables                => this.GetTable<IndexTable>();
		public ITable<IndexTable2>               IndexTable2                => this.GetTable<IndexTable2>();
		public ITable<InheritanceChild>          InheritanceChildren        => this.GetTable<InheritanceChild>();
		public ITable<InheritanceParent>         InheritanceParents         => this.GetTable<InheritanceParent>();
		public ITable<LinqDataType>              LinqDataTypes              => this.GetTable<LinqDataType>();
		public ITable<Parent>                    Parents                    => this.GetTable<Parent>();
		public ITable<Patient>                   Patients                   => this.GetTable<Patient>();
		public ITable<Person>                    People                     => this.GetTable<Person>();
		public ITable<TestIdentity>              TestIdentities             => this.GetTable<TestIdentity>();
		public ITable<TestMerge1>                TestMerge1                 => this.GetTable<TestMerge1>();
		public ITable<TestMerge2>                TestMerge2                 => this.GetTable<TestMerge2>();
		public ITable<PrdGlobalEccCvMara>        PrdGlobalEccCvMaras        => this.GetTable<PrdGlobalEccCvMara>();
		public ITable<ParentChildView>           ParentChildViews           => this.GetTable<ParentChildView>();
		public ITable<ParentView>                ParentViews                => this.GetTable<ParentView>();

		#region Table Functions
		#region GetParentById
		private static readonly MethodInfo _getParentById = MemberHelper.MethodOf<TestDataDB>(ctx => ctx.GetParentById(default));

		public IQueryable<GetParentByIdResult> GetParentById(int? id)
		{
			return this.GetTable<GetParentByIdResult>(this, _getParentById, id);
		}

		public partial class GetParentByIdResult
		{
			public int? ParentId { get; set; }
			public int? Value1   { get; set; }
		}
		#endregion

		#region TestTableFunction
		private static readonly MethodInfo _testTableFunction = MemberHelper.MethodOf<TestDataDB>(ctx => ctx.TestTableFunction(default));

		public IQueryable<TestTableFunctionResult> TestTableFunction(int? i)
		{
			return this.GetTable<TestTableFunctionResult>(this, _testTableFunction, i);
		}

		public partial class TestTableFunctionResult
		{
			public int? O { get; set; }
		}
		#endregion
		#endregion
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

		#region IndexTable2 Associations
		/// <summary>
		/// FK_Patient2_IndexTable
		/// </summary>
		public static IndexTable Patient2IndexTable(this IndexTable2 obj, IDataContext db)
		{
			return db.GetTable<IndexTable>().First(t => obj.PkField1 == t.PkField1 && obj.PkField2 == t.PkField2);
		}
		#endregion

		#region IndexTable Associations
		/// <summary>
		/// FK_Patient2_IndexTable backreference
		/// </summary>
		public static IndexTable2? IndexTable2(this IndexTable obj, IDataContext db)
		{
			return db.GetTable<IndexTable2>().FirstOrDefault(t => t.PkField1 == obj.PkField1 && t.PkField2 == obj.PkField2);
		}
		#endregion
		#endregion

		#region Stored Procedures
		#region AddIssue792Record
		public static int AddIssue792Record(this TestDataDB dataConnection)
		{
			return dataConnection.ExecuteProc("\"AddIssue792Record\"");
		}

		public static Task<int> AddIssue792RecordAsync(this TestDataDB dataConnection, CancellationToken cancellationToken = default)
		{
			return dataConnection.ExecuteProcAsync("\"AddIssue792Record\"", cancellationToken);
		}
		#endregion

		#region Dropconstraintfromtable
		public static int Dropconstraintfromtable(this TestDataDB dataConnection, string? tablename, string? constraintname, string? schemaname)
		{
			var parameters = new []
			{
				new DataParameter("TABLENAME", tablename, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("CONSTRAINTNAME", constraintname, DataType.VarChar)
				{
					Size = 100
				},
				new DataParameter("SCHEMANAME", schemaname, DataType.VarChar)
				{
					Size = 50
				}
			};
			return dataConnection.ExecuteProc("\"DROPCONSTRAINTFROMTABLE\"", parameters);
		}

		public static Task<int> DropconstraintfromtableAsync(this TestDataDB dataConnection, string? tablename, string? constraintname, string? schemaname, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("TABLENAME", tablename, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("CONSTRAINTNAME", constraintname, DataType.VarChar)
				{
					Size = 100
				},
				new DataParameter("SCHEMANAME", schemaname, DataType.VarChar)
				{
					Size = 50
				}
			};
			return dataConnection.ExecuteProcAsync("\"DROPCONSTRAINTFROMTABLE\"", cancellationToken, parameters);
		}
		#endregion

		#region Dropexistingfunction
		public static int Dropexistingfunction(this TestDataDB dataConnection, string? functionname, string? schemaname)
		{
			var parameters = new []
			{
				new DataParameter("FUNCTIONNAME", functionname, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("SCHEMANAME", schemaname, DataType.VarChar)
				{
					Size = 50
				}
			};
			return dataConnection.ExecuteProc("\"DROPEXISTINGFUNCTION\"", parameters);
		}

		public static Task<int> DropexistingfunctionAsync(this TestDataDB dataConnection, string? functionname, string? schemaname, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("FUNCTIONNAME", functionname, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("SCHEMANAME", schemaname, DataType.VarChar)
				{
					Size = 50
				}
			};
			return dataConnection.ExecuteProcAsync("\"DROPEXISTINGFUNCTION\"", cancellationToken, parameters);
		}
		#endregion

		#region Dropexistingprocedure
		public static int Dropexistingprocedure(this TestDataDB dataConnection, string? procedurename, string? schemaname)
		{
			var parameters = new []
			{
				new DataParameter("PROCEDURENAME", procedurename, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("SCHEMANAME", schemaname, DataType.VarChar)
				{
					Size = 50
				}
			};
			return dataConnection.ExecuteProc("\"DROPEXISTINGPROCEDURE\"", parameters);
		}

		public static Task<int> DropexistingprocedureAsync(this TestDataDB dataConnection, string? procedurename, string? schemaname, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("PROCEDURENAME", procedurename, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("SCHEMANAME", schemaname, DataType.VarChar)
				{
					Size = 50
				}
			};
			return dataConnection.ExecuteProcAsync("\"DROPEXISTINGPROCEDURE\"", cancellationToken, parameters);
		}
		#endregion

		#region Dropexistingtable
		public static int Dropexistingtable(this TestDataDB dataConnection, string? tablename, string? schemaname)
		{
			var parameters = new []
			{
				new DataParameter("TABLENAME", tablename, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("SCHEMANAME", schemaname, DataType.VarChar)
				{
					Size = 50
				}
			};
			return dataConnection.ExecuteProc("\"DROPEXISTINGTABLE\"", parameters);
		}

		public static Task<int> DropexistingtableAsync(this TestDataDB dataConnection, string? tablename, string? schemaname, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("TABLENAME", tablename, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("SCHEMANAME", schemaname, DataType.VarChar)
				{
					Size = 50
				}
			};
			return dataConnection.ExecuteProcAsync("\"DROPEXISTINGTABLE\"", cancellationToken, parameters);
		}
		#endregion

		#region Dropexistingview
		public static int Dropexistingview(this TestDataDB dataConnection, string? viewname, string? schemaname)
		{
			var parameters = new []
			{
				new DataParameter("VIEWNAME", viewname, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("SCHEMANAME", schemaname, DataType.VarChar)
				{
					Size = 50
				}
			};
			return dataConnection.ExecuteProc("\"DROPEXISTINGVIEW\"", parameters);
		}

		public static Task<int> DropexistingviewAsync(this TestDataDB dataConnection, string? viewname, string? schemaname, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("VIEWNAME", viewname, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("SCHEMANAME", schemaname, DataType.VarChar)
				{
					Size = 50
				}
			};
			return dataConnection.ExecuteProcAsync("\"DROPEXISTINGVIEW\"", cancellationToken, parameters);
		}
		#endregion

		#region DuplicateColumnNames
		public static IEnumerable<DuplicateColumnNamesResult> DuplicateColumnNames(this TestDataDB dataConnection)
		{
			return dataConnection.QueryProc(dataReader => new DuplicateColumnNamesResult()
			{
				Id = Converter.ChangeTypeTo<int?>(dataReader.GetValue(0), dataConnection.MappingSchema),
				Id1 = Converter.ChangeTypeTo<string?>(dataReader.GetValue(1), dataConnection.MappingSchema)
			}, "\"DuplicateColumnNames\"");
		}

		public static Task<IEnumerable<DuplicateColumnNamesResult>> DuplicateColumnNamesAsync(this TestDataDB dataConnection, CancellationToken cancellationToken = default)
		{
			return dataConnection.QueryProcAsync(dataReader => new DuplicateColumnNamesResult()
			{
				Id = Converter.ChangeTypeTo<int?>(dataReader.GetValue(0), dataConnection.MappingSchema),
				Id1 = Converter.ChangeTypeTo<string?>(dataReader.GetValue(1), dataConnection.MappingSchema)
			}, "\"DuplicateColumnNames\"", cancellationToken);
		}

		public partial class DuplicateColumnNamesResult
		{
			public int?    Id  { get; set; }
			public string? Id1 { get; set; }
		}
		#endregion

		#region OutRefEnumTest
		public static int OutRefEnumTest(this TestDataDB dataConnection, string? str, out string? outputstr, ref string? inputoutputstr)
		{
			var parameters = new []
			{
				new DataParameter("STR", str, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("OUTPUTSTR", null, DataType.VarChar)
				{
					Direction = ParameterDirection.Output,
					Size = 50
				},
				new DataParameter("INPUTOUTPUTSTR", inputoutputstr, DataType.VarChar)
				{
					Direction = ParameterDirection.InputOutput,
					Size = 50
				}
			};
			var ret = dataConnection.ExecuteProc("\"OutRefEnumTest\"", parameters);
			outputstr = Converter.ChangeTypeTo<string?>(parameters[1].Value);
			inputoutputstr = Converter.ChangeTypeTo<string?>(parameters[2].Value);
			return ret;
		}

		public static async Task<OutRefEnumTestResults> OutRefEnumTestAsync(this TestDataDB dataConnection, string? str, string? outputstr, string? inputoutputstr, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("STR", str, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("OUTPUTSTR", null, DataType.VarChar)
				{
					Direction = ParameterDirection.Output,
					Size = 50
				},
				new DataParameter("INPUTOUTPUTSTR", inputoutputstr, DataType.VarChar)
				{
					Direction = ParameterDirection.InputOutput,
					Size = 50
				}
			};
			var result = await dataConnection.ExecuteProcAsync("\"OutRefEnumTest\"", cancellationToken, parameters);
			return new OutRefEnumTestResults()
			{
				Result = result,
				Inputoutputstr = Converter.ChangeTypeTo<string?>(parameters[2].Value),
				Outputstr = Converter.ChangeTypeTo<string?>(parameters[1].Value)
			};
		}

		public class OutRefEnumTestResults
		{
			public int     Result         { get; set; }
			public string? Inputoutputstr { get; set; }
			public string? Outputstr      { get; set; }
		}
		#endregion

		#region OutRefTest
		public static int OutRefTest(this TestDataDB dataConnection, int? id, out int? outputid, ref int? inputoutputid, string? str, out string? outputstr, ref string? inputoutputstr)
		{
			var parameters = new []
			{
				new DataParameter("ID", id, DataType.Int32)
				{
					Size = 10
				},
				new DataParameter("OUTPUTID", null, DataType.Int32)
				{
					Direction = ParameterDirection.Output,
					Size = 10
				},
				new DataParameter("INPUTOUTPUTID", inputoutputid, DataType.Int32)
				{
					Direction = ParameterDirection.InputOutput,
					Size = 10
				},
				new DataParameter("STR", str, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("OUTPUTSTR", null, DataType.VarChar)
				{
					Direction = ParameterDirection.Output,
					Size = 50
				},
				new DataParameter("INPUTOUTPUTSTR", inputoutputstr, DataType.VarChar)
				{
					Direction = ParameterDirection.InputOutput,
					Size = 50
				}
			};
			var ret = dataConnection.ExecuteProc("\"OutRefTest\"", parameters);
			outputid = Converter.ChangeTypeTo<int?>(parameters[1].Value);
			inputoutputid = Converter.ChangeTypeTo<int?>(parameters[2].Value);
			outputstr = Converter.ChangeTypeTo<string?>(parameters[4].Value);
			inputoutputstr = Converter.ChangeTypeTo<string?>(parameters[5].Value);
			return ret;
		}

		public static async Task<OutRefTestResults> OutRefTestAsync(this TestDataDB dataConnection, int? id, int? outputid, int? inputoutputid, string? str, string? outputstr, string? inputoutputstr, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("ID", id, DataType.Int32)
				{
					Size = 10
				},
				new DataParameter("OUTPUTID", null, DataType.Int32)
				{
					Direction = ParameterDirection.Output,
					Size = 10
				},
				new DataParameter("INPUTOUTPUTID", inputoutputid, DataType.Int32)
				{
					Direction = ParameterDirection.InputOutput,
					Size = 10
				},
				new DataParameter("STR", str, DataType.VarChar)
				{
					Size = 50
				},
				new DataParameter("OUTPUTSTR", null, DataType.VarChar)
				{
					Direction = ParameterDirection.Output,
					Size = 50
				},
				new DataParameter("INPUTOUTPUTSTR", inputoutputstr, DataType.VarChar)
				{
					Direction = ParameterDirection.InputOutput,
					Size = 50
				}
			};
			var result = await dataConnection.ExecuteProcAsync("\"OutRefTest\"", cancellationToken, parameters);
			return new OutRefTestResults()
			{
				Result = result,
				Inputoutputid = Converter.ChangeTypeTo<int?>(parameters[2].Value),
				Inputoutputstr = Converter.ChangeTypeTo<string?>(parameters[5].Value),
				Outputid = Converter.ChangeTypeTo<int?>(parameters[1].Value),
				Outputstr = Converter.ChangeTypeTo<string?>(parameters[4].Value)
			};
		}

		public class OutRefTestResults
		{
			public int     Result         { get; set; }
			public int?    Inputoutputid  { get; set; }
			public string? Inputoutputstr { get; set; }
			public int?    Outputid       { get; set; }
			public string? Outputstr      { get; set; }
		}
		#endregion

		#region PatientSelectAll
		public static IEnumerable<PatientSelectAllResult> PatientSelectAll(this TestDataDB dataConnection)
		{
			return dataConnection.QueryProc<PatientSelectAllResult>("\"Patient_SelectAll\"");
		}

		public static Task<IEnumerable<PatientSelectAllResult>> PatientSelectAllAsync(this TestDataDB dataConnection, CancellationToken cancellationToken = default)
		{
			return dataConnection.QueryProcAsync<PatientSelectAllResult>("\"Patient_SelectAll\"", cancellationToken);
		}

		public partial class PatientSelectAllResult
		{
			public int?    PersonId   { get; set; }
			public string? FirstName  { get; set; }
			public string? LastName   { get; set; }
			public string? MiddleName { get; set; }
			public string? Gender     { get; set; }
			public string? Diagnosis  { get; set; }
		}
		#endregion

		#region PatientSelectByName
		public static IEnumerable<PatientSelectByNameResult> PatientSelectByName(this TestDataDB dataConnection, string? firstname, string? lastname)
		{
			var parameters = new []
			{
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				}
			};
			return dataConnection.QueryProc<PatientSelectByNameResult>("\"Patient_SelectByName\"", parameters);
		}

		public static Task<IEnumerable<PatientSelectByNameResult>> PatientSelectByNameAsync(this TestDataDB dataConnection, string? firstname, string? lastname, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				}
			};
			return dataConnection.QueryProcAsync<PatientSelectByNameResult>("\"Patient_SelectByName\"", cancellationToken, parameters);
		}

		public partial class PatientSelectByNameResult
		{
			public int?    PersonId   { get; set; }
			public string? FirstName  { get; set; }
			public string? LastName   { get; set; }
			public string? MiddleName { get; set; }
			public string? Gender     { get; set; }
			public string? Diagnosis  { get; set; }
		}
		#endregion

		#region PersonDelete
		public static int PersonDelete(this TestDataDB dataConnection, int? personid)
		{
			var parameters = new []
			{
				new DataParameter("PERSONID", personid, DataType.Int32)
				{
					Size = 10
				}
			};
			return dataConnection.ExecuteProc("\"Person_Delete\"", parameters);
		}

		public static Task<int> PersonDeleteAsync(this TestDataDB dataConnection, int? personid, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("PERSONID", personid, DataType.Int32)
				{
					Size = 10
				}
			};
			return dataConnection.ExecuteProcAsync("\"Person_Delete\"", cancellationToken, parameters);
		}
		#endregion

		#region PersonInsert
		public static int PersonInsert(this TestDataDB dataConnection, string? firstname, string? lastname, string? middlename, char? gender)
		{
			var parameters = new []
			{
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("MIDDLENAME", middlename, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("GENDER", gender, DataType.Char)
				{
					Size = 1
				}
			};
			return dataConnection.ExecuteProc("\"Person_Insert\"", parameters);
		}

		public static Task<int> PersonInsertAsync(this TestDataDB dataConnection, string? firstname, string? lastname, string? middlename, char? gender, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("MIDDLENAME", middlename, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("GENDER", gender, DataType.Char)
				{
					Size = 1
				}
			};
			return dataConnection.ExecuteProcAsync("\"Person_Insert\"", cancellationToken, parameters);
		}
		#endregion

		#region PersonInsertOutputParameter
		public static int PersonInsertOutputParameter(this TestDataDB dataConnection, string? firstname, string? lastname, string? middlename, char? gender, out int? personid)
		{
			var parameters = new []
			{
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("MIDDLENAME", middlename, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("GENDER", gender, DataType.Char)
				{
					Size = 1
				},
				new DataParameter("PERSONID", null, DataType.Int32)
				{
					Direction = ParameterDirection.Output,
					Size = 10
				}
			};
			var ret = dataConnection.ExecuteProc("\"Person_Insert_OutputParameter\"", parameters);
			personid = Converter.ChangeTypeTo<int?>(parameters[4].Value);
			return ret;
		}

		public static async Task<PersonInsertOutputParameterResults> PersonInsertOutputParameterAsync(this TestDataDB dataConnection, string? firstname, string? lastname, string? middlename, char? gender, int? personid, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("MIDDLENAME", middlename, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("GENDER", gender, DataType.Char)
				{
					Size = 1
				},
				new DataParameter("PERSONID", null, DataType.Int32)
				{
					Direction = ParameterDirection.Output,
					Size = 10
				}
			};
			var result = await dataConnection.ExecuteProcAsync("\"Person_Insert_OutputParameter\"", cancellationToken, parameters);
			return new PersonInsertOutputParameterResults()
			{
				Result = result,
				Personid = Converter.ChangeTypeTo<int?>(parameters[4].Value)
			};
		}

		public class PersonInsertOutputParameterResults
		{
			public int  Result   { get; set; }
			public int? Personid { get; set; }
		}
		#endregion

		#region PersonSelectAll
		public static IEnumerable<PersonSelectAllResult> PersonSelectAll(this TestDataDB dataConnection)
		{
			return dataConnection.QueryProc<PersonSelectAllResult>("\"Person_SelectAll\"");
		}

		public static Task<IEnumerable<PersonSelectAllResult>> PersonSelectAllAsync(this TestDataDB dataConnection, CancellationToken cancellationToken = default)
		{
			return dataConnection.QueryProcAsync<PersonSelectAllResult>("\"Person_SelectAll\"", cancellationToken);
		}

		public partial class PersonSelectAllResult
		{
			public int     PersonId   { get; set; }
			public string  FirstName  { get; set; } = null!;
			public string  LastName   { get; set; } = null!;
			public string? MiddleName { get; set; }
			public string  Gender     { get; set; } = null!;
		}
		#endregion

		#region PersonSelectByKey
		public static IEnumerable<PersonSelectByKeyResult> PersonSelectByKey(this TestDataDB dataConnection, int? id)
		{
			var parameters = new []
			{
				new DataParameter("ID", id, DataType.Int32)
				{
					Size = 10
				}
			};
			return dataConnection.QueryProc<PersonSelectByKeyResult>("\"Person_SelectByKey\"", parameters);
		}

		public static Task<IEnumerable<PersonSelectByKeyResult>> PersonSelectByKeyAsync(this TestDataDB dataConnection, int? id, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("ID", id, DataType.Int32)
				{
					Size = 10
				}
			};
			return dataConnection.QueryProcAsync<PersonSelectByKeyResult>("\"Person_SelectByKey\"", cancellationToken, parameters);
		}

		public partial class PersonSelectByKeyResult
		{
			public int     PersonId   { get; set; }
			public string  FirstName  { get; set; } = null!;
			public string  LastName   { get; set; } = null!;
			public string? MiddleName { get; set; }
			public string  Gender     { get; set; } = null!;
		}
		#endregion

		#region PersonSelectByName
		public static IEnumerable<PersonSelectByNameResult> PersonSelectByName(this TestDataDB dataConnection, string? firstname, string? lastname)
		{
			var parameters = new []
			{
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				}
			};
			return dataConnection.QueryProc<PersonSelectByNameResult>("\"Person_SelectByName\"", parameters);
		}

		public static Task<IEnumerable<PersonSelectByNameResult>> PersonSelectByNameAsync(this TestDataDB dataConnection, string? firstname, string? lastname, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				}
			};
			return dataConnection.QueryProcAsync<PersonSelectByNameResult>("\"Person_SelectByName\"", cancellationToken, parameters);
		}

		public partial class PersonSelectByNameResult
		{
			public int     PersonId   { get; set; }
			public string  FirstName  { get; set; } = null!;
			public string  LastName   { get; set; } = null!;
			public string? MiddleName { get; set; }
			public string  Gender     { get; set; } = null!;
		}
		#endregion

		#region PersonSelectListByName
		public static IEnumerable<PersonSelectListByNameResult> PersonSelectListByName(this TestDataDB dataConnection, string? firstname, string? lastname)
		{
			var parameters = new []
			{
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				}
			};
			return dataConnection.QueryProc<PersonSelectListByNameResult>("\"Person_SelectListByName\"", parameters);
		}

		public static Task<IEnumerable<PersonSelectListByNameResult>> PersonSelectListByNameAsync(this TestDataDB dataConnection, string? firstname, string? lastname, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				}
			};
			return dataConnection.QueryProcAsync<PersonSelectListByNameResult>("\"Person_SelectListByName\"", cancellationToken, parameters);
		}

		public partial class PersonSelectListByNameResult
		{
			public int     PersonId   { get; set; }
			public string  FirstName  { get; set; } = null!;
			public string  LastName   { get; set; } = null!;
			public string? MiddleName { get; set; }
			public string  Gender     { get; set; } = null!;
		}
		#endregion

		#region PersonUpdate
		public static int PersonUpdate(this TestDataDB dataConnection, int? personid, string? firstname, string? lastname, string? middlename, char? gender)
		{
			var parameters = new []
			{
				new DataParameter("PERSONID", personid, DataType.Int32)
				{
					Size = 10
				},
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("MIDDLENAME", middlename, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("GENDER", gender, DataType.Char)
				{
					Size = 1
				}
			};
			return dataConnection.ExecuteProc("\"Person_Update\"", parameters);
		}

		public static Task<int> PersonUpdateAsync(this TestDataDB dataConnection, int? personid, string? firstname, string? lastname, string? middlename, char? gender, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("PERSONID", personid, DataType.Int32)
				{
					Size = 10
				},
				new DataParameter("FIRSTNAME", firstname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("LASTNAME", lastname, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("MIDDLENAME", middlename, DataType.NVarChar)
				{
					Size = 50
				},
				new DataParameter("GENDER", gender, DataType.Char)
				{
					Size = 1
				}
			};
			return dataConnection.ExecuteProcAsync("\"Person_Update\"", cancellationToken, parameters);
		}
		#endregion

		#region SelectImplicitColumn
		public static IEnumerable<SelectImplicitColumnResult> SelectImplicitColumn(this TestDataDB dataConnection)
		{
			return dataConnection.QueryProc<SelectImplicitColumnResult>("\"SelectImplicitColumn\"");
		}

		public static Task<IEnumerable<SelectImplicitColumnResult>> SelectImplicitColumnAsync(this TestDataDB dataConnection, CancellationToken cancellationToken = default)
		{
			return dataConnection.QueryProcAsync<SelectImplicitColumnResult>("\"SelectImplicitColumn\"", cancellationToken);
		}

		public partial class SelectImplicitColumnResult
		{
			public int? _123 { get; set; }
		}
		#endregion

		#region TestProcedure
		public static IEnumerable<TestProcedureResult> TestProcedure(this TestDataDB dataConnection, int? i)
		{
			var parameters = new []
			{
				new DataParameter("I", i, DataType.Int32)
				{
					Size = 10
				}
			};
			return dataConnection.QueryProc(dataReader => new TestProcedureResult()
			{
				Column = Converter.ChangeTypeTo<int?>(dataReader.GetValue(0), dataConnection.MappingSchema)
			}, "\"TEST_PROCEDURE\"", parameters);
		}

		public static Task<IEnumerable<TestProcedureResult>> TestProcedureAsync(this TestDataDB dataConnection, int? i, CancellationToken cancellationToken = default)
		{
			var parameters = new []
			{
				new DataParameter("I", i, DataType.Int32)
				{
					Size = 10
				}
			};
			return dataConnection.QueryProcAsync(dataReader => new TestProcedureResult()
			{
				Column = Converter.ChangeTypeTo<int?>(dataReader.GetValue(0), dataConnection.MappingSchema)
			}, "\"TEST_PROCEDURE\"", cancellationToken, parameters);
		}

		public partial class TestProcedureResult
		{
			public int? Column { get; set; }
		}
		#endregion

		#region Prd.Global.Ecc/CvMarAproc
		public static IEnumerable<PrdGlobalEccCvMarAprocResult> PrdGlobalEccCvMarAproc(this TestDataDB dataConnection)
		{
			return dataConnection.QueryProc(dataReader => new PrdGlobalEccCvMarAprocResult()
			{
				Id = Converter.ChangeTypeTo<int?>(dataReader.GetValue(0), dataConnection.MappingSchema),
				Id1 = Converter.ChangeTypeTo<string?>(dataReader.GetValue(1), dataConnection.MappingSchema)
			}, "\"prd.global.ecc/CV_MARAproc\"");
		}

		public static Task<IEnumerable<PrdGlobalEccCvMarAprocResult>> PrdGlobalEccCvMarAprocAsync(this TestDataDB dataConnection, CancellationToken cancellationToken = default)
		{
			return dataConnection.QueryProcAsync(dataReader => new PrdGlobalEccCvMarAprocResult()
			{
				Id = Converter.ChangeTypeTo<int?>(dataReader.GetValue(0), dataConnection.MappingSchema),
				Id1 = Converter.ChangeTypeTo<string?>(dataReader.GetValue(1), dataConnection.MappingSchema)
			}, "\"prd.global.ecc/CV_MARAproc\"", cancellationToken);
		}

		public partial class PrdGlobalEccCvMarAprocResult
		{
			public int?    Id  { get; set; }
			public string? Id1 { get; set; }
		}
		#endregion
		#endregion

		#region Scalar Functions
		#region TestFunction
		public static int? TestFunction(int? i)
		{
			throw new InvalidOperationException("Scalar function cannot be called outside of query");
		}
		#endregion
		#endregion
	}
}
