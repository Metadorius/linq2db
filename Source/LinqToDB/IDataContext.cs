﻿using System.Linq.Expressions;

namespace LinqToDB
{
	using System.Data.Common;
	using System.Threading.Tasks;
	using Linq;
	using Interceptors;
	using Mapping;
	using SqlProvider;

	/// <summary>
	/// Database connection abstraction interface.
	/// </summary>
	[PublicAPI]
	public interface IDataContext : IDisposable
#if NATIVE_ASYNC
		, IAsyncDisposable
#else
		, Async.IAsyncDisposable
#endif
	{
		/// <summary>
		/// Provider identifier.
		/// </summary>
		string              ContextName           { get; }
		int                 ContextID             { get; }
		/// <summary>
		/// Gets SQL builder service factory method for current context data provider.
		/// </summary>
		Func<ISqlBuilder>   CreateSqlProvider     { get; }
		/// <summary>
		/// Gets SQL optimizer service factory method for current context data provider.
		/// </summary>
		Func<ISqlOptimizer> GetSqlOptimizer       { get; }
		/// <summary>
		/// Gets SQL support flags for current context data provider.
		/// </summary>
		SqlProviderFlags    SqlProviderFlags      { get; }
		/// <summary>
		/// Gets supported table options for current context data provider.
		/// </summary>
		TableOptions        SupportedTableOptions { get; }
		/// <summary>
		/// Gets data reader implementation type for current context data provider.
		/// </summary>
		Type                DataReaderType        { get; }
		/// <summary>
		/// Gets mapping schema, used for current context.
		/// </summary>
		MappingSchema       MappingSchema         { get; }
		/// <summary>
		/// Gets or sets option to force inline parameter values as literals into command text. If parameter inlining not supported
		/// for specific value type, it will be used as parameter.
		/// </summary>
		bool                InlineParameters      { get; set; }
		/// <summary>
		/// Gets list of query hints (writable collection), that will be used for all queries, executed using current context.
		/// </summary>
		List<string>        QueryHints            { get; }
		/// <summary>
		/// Gets list of query hints (writable collection), that will be used only for next query, executed using current context.
		/// </summary>
		List<string>        NextQueryHints        { get; }
		/// <summary>
		/// Gets or sets flag to close context after query execution or leave it open.
		/// </summary>
		bool                CloseAfterUse         { get; set; }

		/// <summary>
		/// Returns column value reader expression.
		/// </summary>
		/// <param name="reader">Data reader instance.</param>
		/// <param name="idx">Column index.</param>
		/// <param name="readerExpression">Data reader accessor expression.</param>
		/// <param name="toType">Expected value type.</param>
		/// <returns>Column read expression.</returns>
		Expression          GetReaderExpression(DbDataReader reader, int idx, Expression readerExpression, Type toType);
		/// <summary>
		/// Returns true, of data reader column could contain <see cref="DBNull"/> value.
		/// </summary>
		/// <param name="reader">Data reader instance.</param>
		/// <param name="idx">Column index.</param>
		/// <returns><c>true</c> or <c>null</c> if column could contain <see cref="DBNull"/>.</returns>
		bool?               IsDBNullAllowed    (DbDataReader reader, int idx);

		/// <summary>
		/// Clones current context.
		/// </summary>
		/// <returns>Cloned context.</returns>
		IDataContext        Clone              (bool forNestedQuery);

		/// <summary>
		/// Closes context connection and disposes underlying resources.
		/// </summary>
		void                Close              ();

		/// <summary>
		/// Closes context connection and disposes underlying resources.
		/// </summary>
		Task                CloseAsync         ();

		/// <summary>
		/// Returns query runner service for current context.
		/// </summary>
		/// <param name="query">Query batch object.</param>
		/// <param name="queryNumber">Index of query in query batch.</param>
		/// <param name="expression">Query results mapping expression.</param>
		/// <param name="parameters">Query parameters.</param>
		/// <param name="preambles">Query preambles</param>
		/// <returns>Query runner service.</returns>
		IQueryRunner GetQueryRunner(Query query, int queryNumber, Expression expression, object?[]? parameters, object?[]? preambles);

		/// <summary>
		/// Adds interceptor instance to context.
		/// </summary>
		/// <param name="interceptor">Interceptor.</param>
		void AddInterceptor(IInterceptor interceptor);

		IUnwrapDataObjectInterceptor? UnwrapDataObjectInterceptor { get; }

		FluentMappingBuilder GetFluentMappingBuilder();
	}
}
