﻿using System;

namespace LinqToDB.Tools
{
	public enum ActivityID
	{
		QueryProviderExecuteT,
		QueryProviderExecute,
		QueryProviderGetEnumeratorT,
		QueryProviderGetEnumerator,
		GetQueryTotal,
			GetQueryFind,
				GetQueryFindExpose,
				GetQueryFindFind,
			GetQueryCreate,
				Build,
					BuildSequence,
						BuildSequenceCanBuild,
						BuildSequenceBuild,
					ReorderBuilders,
					BuildQuery,
						FinalizeQuery,
			GetIEnumerable,
		ExecuteQuery,
		ExecuteQueryAsync,
		ExecuteElement,
		ExecuteElementAsync,
		ExecuteScalar,
		ExecuteScalarAsync,
		ExecuteNonQuery,
		ExecuteNonQueryAsync,
		ExecuteNonQuery2,
		ExecuteNonQuery2Async,
		ExecuteScalar2,
		ExecuteScalar2Async,

		CreateTable,
		CreateTableAsync,
		DropTable,
		DropTableAsync,
		DeleteObject,
		DeleteObjectAsync,
		InsertObject,
		InsertObjectAsync,
		InsertOrReplaceObject,
		InsertOrReplaceObjectAsync,
		InsertWithIdentityObject,
		InsertWithIdentityObjectAsync,
		UpdateObject,
		UpdateObjectAsync,
		BulkCopy,
		BulkCopyAsync,

			BuildSql,

		CommandInfoExecute,
		CommandInfoExecuteT,
		CommandInfoExecuteCustom,
		CommandInfoExecuteAsync,
		CommandInfoExecuteAsyncT,

			ConnectionOpen,
			ConnectionOpenAsync,
			ConnectionClose,
			ConnectionCloseAsync,
			ConnectionDispose,
			ConnectionBeginTransaction,
			ConnectionBeginTransactionAsync,
			TransactionCommit,
			TransactionCommitAsync,
			TransactionRollback,
			TransactionRollbackAsync,
			TransactionDispose,
			TransactionDisposeAsync,
			CommandExecuteScalar,
			CommandExecuteScalarAsync,
			CommandExecuteReader,
			CommandExecuteReaderAsync,
			CommandExecuteNonQuery,
			CommandExecuteNonQueryAsync,

		GetSqlText,

			Materialization,
			OnTraceInternal,
	}
}
