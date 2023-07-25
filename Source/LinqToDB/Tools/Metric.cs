﻿using System;

namespace LinqToDB.Tools
{
	public enum Metric
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
			OnTraceInternal,
	}
}
