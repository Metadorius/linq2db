﻿using System;
using System.Data.Common;
using System.Linq.Expressions;

namespace LinqToDB.DataProvider
{
	using Expressions;

	public class OdbcProviderAdapter : IDynamicProviderAdapter
	{
		private static readonly object _syncRoot = new object();
		private static OdbcProviderAdapter? _instance;

		public const string AssemblyName    = "System.Data.Odbc";
		public const string ClientNamespace = "System.Data.Odbc";

		private OdbcProviderAdapter(
			Type connectionType,
			Type dataReaderType,
			Type parameterType,
			Type commandType,
			Type transactionType,
			Action<DbParameter, OdbcType> dbTypeSetter,
			Func  <DbParameter, OdbcType> dbTypeGetter,
			Func<string, OdbcConnection> connectionFactory)
		{
			ConnectionType  = connectionType;
			DataReaderType  = dataReaderType;
			ParameterType   = parameterType;
			CommandType     = commandType;
			TransactionType = transactionType;

			SetDbType = dbTypeSetter;
			GetDbType = dbTypeGetter;

			CreateConnection = connectionFactory;
		}

		public Type ConnectionType  { get; }
		public Type DataReaderType  { get; }
		public Type ParameterType   { get; }
		public Type CommandType     { get; }
		public Type TransactionType { get; }

		public Action<DbParameter, OdbcType> SetDbType { get; }
		public Func  <DbParameter, OdbcType> GetDbType { get; }

		internal Func<string, OdbcConnection> CreateConnection { get; }

		public static OdbcProviderAdapter GetInstance()
		{
			if (_instance == null)
				lock (_syncRoot)
					if (_instance == null)
					{
#if NETFRAMEWORK
						var assembly = typeof(System.Data.Odbc.OdbcConnection).Assembly;
#else
						var assembly = Common.Tools.TryLoadAssembly(AssemblyName, null);
						if (assembly == null)
							throw new InvalidOperationException($"Cannot load assembly {AssemblyName}");
#endif

						var connectionType  = assembly.GetType($"{ClientNamespace}.OdbcConnection" , true)!;
						var dataReaderType  = assembly.GetType($"{ClientNamespace}.OdbcDataReader" , true)!;
						var parameterType   = assembly.GetType($"{ClientNamespace}.OdbcParameter"  , true)!;
						var commandType     = assembly.GetType($"{ClientNamespace}.OdbcCommand"    , true)!;
						var transactionType = assembly.GetType($"{ClientNamespace}.OdbcTransaction", true)!;
						var dbType          = assembly.GetType($"{ClientNamespace}.OdbcType", true)!;

						var typeMapper = new TypeMapper();
						typeMapper.RegisterTypeWrapper<OdbcType>(dbType);
						typeMapper.RegisterTypeWrapper<OdbcParameter>(parameterType);
						typeMapper.FinalizeMappings();

						var connectionFactory  = typeMapper.BuildWrappedFactory((string connectionString) => new OdbcConnection(connectionString));

						var dbTypeBuilder = typeMapper.Type<OdbcParameter>().Member(p => p.OdbcType);
						var typeSetter    = dbTypeBuilder.BuildSetter<DbParameter>();
						var typeGetter    = dbTypeBuilder.BuildGetter<DbParameter>();

						_instance = new OdbcProviderAdapter(
							connectionType,
							dataReaderType,
							parameterType,
							commandType,
							transactionType,
							typeSetter,
							typeGetter,
							connectionFactory);
					}

			return _instance;
		}

		#region Wrappers

		[Wrapper]
		internal sealed class OdbcConnection : TypeWrapper, IConnectionWrapper
		{
			private static LambdaExpression[] Wrappers { get; }
				= new LambdaExpression[]
			{
					// [0]: Open
					(Expression<Action<OdbcConnection>>)((OdbcConnection this_) => this_.Open()),
					// [1]: Dispose
					(Expression<Action<OdbcConnection>>)((OdbcConnection this_) => this_.Dispose()),
			};

			public OdbcConnection(object instance, Delegate[] wrappers) : base(instance, wrappers)
			{
			}

			public OdbcConnection(string connectionString) => throw new NotImplementedException();

			public void Open()    => ((Action<OdbcConnection>)CompiledWrappers[0])(this);
			public void Dispose() => ((Action<OdbcConnection>)CompiledWrappers[1])(this);

			DbConnection IConnectionWrapper.Connection => (DbConnection)instance_;
		}

		[Wrapper]
		private sealed class OdbcParameter
		{
			public OdbcType OdbcType { get; set; }
		}

		[Wrapper]
		public enum OdbcType
		{
			BigInt           = 1,
			Binary           = 2,
			Bit              = 3,
			Char             = 4,
			Date             = 23,
			DateTime         = 5,
			Decimal          = 6,
			Double           = 8,
			Image            = 9,
			Int              = 10,
			NChar            = 11,
			NText            = 12,
			Numeric          = 7,
			NVarChar         = 13,
			Real             = 14,
			SmallDateTime    = 16,
			SmallInt         = 17,
			Text             = 18,
			Time             = 24,
			Timestamp        = 19,
			TinyInt          = 20,
			UniqueIdentifier = 15,
			VarBinary        = 21,
			VarChar          = 22
		}
		#endregion
	}
}
