﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace LinqToDB.Linq.Builder
{
	using Async;
	using Common;
	using Common.Internal;
	using Extensions;
	using Reflection;
	using SqlQuery;
	using LinqToDB.Expressions;

	partial class ExpressionBuilder
	{

		void CollectDependencies(IBuildContext context, Expression expression, HashSet<Expression> dependencies)
		{
			var toIgnore     = new HashSet<Expression>();
			expression.Visit((dependencies, context, builder: this, toIgnore), static (ctx, e) =>
			{
				if (ctx.toIgnore.Contains(e))
					return false;

				if (e.NodeType == ExpressionType.MemberAccess)
				{
					var current = e;
					do
					{
						if (current is not MemberExpression me)
							break;

						current = me.Expression;
						if (current is ContextRefExpression)
						{
							break;
						}
					} while (true);

					if (current is ContextRefExpression)
					{
						var testExpr = ctx.builder.ConvertToSqlExpr(ctx.context, e, ProjectFlags.SQL | ProjectFlags.Keys | ProjectFlags.Test);
						if (testExpr is SqlPlaceholderExpression or SqlGenericConstructorExpression)
							ctx.dependencies.Add(e);

						return false;
					}
				}
				else if (e is BinaryExpression binary)
				{
					if (binary.Left is ContextRefExpression)
					{
						ctx.dependencies.Add(binary.Left);
						return false;
					}
					if (binary.Right is ContextRefExpression)
					{
						ctx.dependencies.Add(binary.Right);
						return false;
					}
				}
				else if (e is SqlPlaceholderExpression placeholder)
				{
					ctx.dependencies.Add(placeholder);
					return false;
				}

				return true;
			});
		}

		static Expression GenerateKeyExpression(Expression[] members, int startIndex)
		{
			var count = members.Length - startIndex;
			if (count == 0)
				throw new ArgumentOutOfRangeException(nameof(startIndex));

			Expression[] arguments;

			if (count > MutableTuple.MaxMemberCount)
			{
				count     = MutableTuple.MaxMemberCount;
				arguments = new Expression[count];
				Array.Copy(members, startIndex, arguments, 0, count - 1);
				arguments[count - 1] = GenerateKeyExpression(members, startIndex + count);
			}
			else
			{
				arguments = new Expression[count];
				Array.Copy(members, startIndex, arguments, 0, count);
			}

			var type         = MutableTuple.MTypes[count - 1];
			var concreteType = type.MakeGenericType(arguments.Select(a => a.Type).ToArray());
			var constructor = concreteType.GetConstructor(Type.EmptyTypes) ??
			                  throw new LinqToDBException($"Can not retrieve default constructor for '{type.Name}'");

			var newExpression = Expression.New(constructor);
			var initExpression = Expression.MemberInit(newExpression,
				arguments.Select((a, i) => Expression.Bind(concreteType.GetProperty("Item" + (i + 1))!, a)));
			return initExpression;
		}

		struct KeyDetailEnvelope<TKey, TDetail>
			where TKey: notnull
		{
			public TKey    Key;
			public TDetail Detail;
		}

		public static Type GetEnumerableElementType(Type type)
		{
			var genericType = typeof(IEnumerable<>).GetGenericType(type);
			if (genericType == null)
				throw new InvalidOperationException($"Type '{type.Name}' do not implement IEnumerable");

			return genericType.GetGenericArguments()[0];
		}

		Expression ExpandLambdas(IBuildContext currentContext, Expression expression)
		{
			var result = expression.Transform((builder: this, currentContext), static (ctx, e) =>
			{
				if (e.NodeType == ExpressionType.Lambda)
				{
					var lambda  = (LambdaExpression)e;
					if (lambda.Body.Find(1, (_, x) => x is ContextRefExpression) != null)
					{
						var newBody = ctx.builder.ExpandContexts(ctx.currentContext, lambda.Body);
						if (!ExpressionEqualityComparer.Instance.Equals(lambda.Body, newBody))
						{
							return new TransformInfo(Expression.Lambda(newBody, lambda.Parameters), false);
						}
					}
				}

				return new TransformInfo(e);
			});

			return result;
		}

		Expression ExpandContexts(IBuildContext currentContext, Expression expression)
		{
			var result = expression.Transform((builder: this, currentContext), static (ctx, e) =>
			{
				if (e.NodeType == ExpressionType.Extension || e.NodeType == ExpressionType.MemberAccess ||
				    e.NodeType == ExpressionType.Call)
				{
					var newExpr = ctx.builder.MakeExpression(ctx.currentContext, e, ProjectFlags.Expand);

					if (!ExpressionEqualityComparer.Instance.Equals(e, newExpr))
						return new TransformInfo(newExpr, false);
				}

				return new TransformInfo(e);
			});

			return result;
		}

		bool CanBeCompiledQueryableArguments(MethodCallExpression mc)
		{
			//TODO: revise CanBeCompiled

			/*
			for(var i = 1; i < mc.Arguments.Count; i++)
			{
				if (!CanBeCompiled(mc.Arguments[i], false))
					return false;
			}

			*/
			return true;
		}

		List<(LambdaExpression, bool)>? CollectOrderBy(Expression sequenceExpression)
		{
			sequenceExpression = sequenceExpression.UnwrapConvert();
			var current = sequenceExpression;

			List<(LambdaExpression, bool)>? result = null;

			while (current is MethodCallExpression mc && mc.IsQueryable())
			{
				if (mc.IsQueryable(nameof(Enumerable.ThenBy)))
				{
					if (!CanBeCompiledQueryableArguments(mc))
						break;
					result ??= new ();
					result.Add((mc.Arguments[1].UnwrapLambda(), false));
				}
				else if (mc.IsQueryable(nameof(Enumerable.ThenByDescending)))
				{
					if (!CanBeCompiledQueryableArguments(mc))
						break;
					result ??= new ();
					result.Add((mc.Arguments[1].UnwrapLambda(), true));
				}
				else if (mc.IsQueryable(nameof(Enumerable.OrderBy)))
				{
					if (!CanBeCompiledQueryableArguments(mc))
						break;
					result ??= new ();
					result.Add((mc.Arguments[1].UnwrapLambda(), false));
					break;
				}
				else if (mc.IsQueryable(nameof(Enumerable.OrderByDescending)))
				{
					if (!CanBeCompiledQueryableArguments(mc))
						break;
					result ??= new ();
					result.Add((mc.Arguments[1].UnwrapLambda(), true));
					break;
				}

				current = mc.Arguments[0];
				if (!mc.Type.IsSameOrParentOf(current.Type))
					break;
			}

			result?.Reverse();

			return result;
		}


		static string[] _passThroughMethodsForUnwrappingDefaultIfEmpty = { nameof(Enumerable.Where), nameof(Enumerable.Select) };

		static Expression UnwrapDefaultIfEmpty(Expression expression)
		{
			do
			{
				if (expression is MethodCallExpression mc)
				{
					if (mc.IsQueryable(nameof(Enumerable.DefaultIfEmpty)))
						expression = mc.Arguments[0];
					else if (mc.IsQueryable(_passThroughMethodsForUnwrappingDefaultIfEmpty))
					{
						return mc.Update(mc.Object, mc.Arguments.Select(UnwrapDefaultIfEmpty));
					}
					else if (mc.IsQueryable(nameof(Enumerable.SelectMany)))
					{
						return mc.Update(mc.Object, mc.Arguments.Select(UnwrapDefaultIfEmpty));
					}
					else
						break;
				}
				else if (expression is SqlAdjustTypeExpression adjust)
				{
					return adjust.Update(UnwrapDefaultIfEmpty(adjust.Expression));
				}
				else
					break;
			} while (true);

			return expression;
		}

		Expression ProcessEagerLoadingExpression(
			IBuildContext          buildContext,  
			SqlEagerLoadExpression eagerLoad,
			ParameterExpression    queryParameter, 
			List<Preamble>         preambles,
			Expression[]           previousKeys)
		{
			var cloningContext       = new CloningContext();
			var clonedParentContext  = cloningContext.CloneContext(buildContext);

			var itemType = eagerLoad.Type.GetItemType();

			if (itemType == null)
				throw new InvalidOperationException("Could not retrieve itemType for EagerLoading.");

			clonedParentContext = new EagerContext(clonedParentContext, buildContext.ElementType);
			
			var dependencies = new HashSet<Expression>(ExpressionEqualityComparer.Instance);

			var sequenceExpression = UnwrapDefaultIfEmpty(eagerLoad.SequenceExpression);

			sequenceExpression = ExpandContexts(buildContext, sequenceExpression);
			sequenceExpression = UnwrapDefaultIfEmpty(sequenceExpression);

			var correctedSequence  = cloningContext.CloneExpression(sequenceExpression);
			var correctedPredicate = cloningContext.CloneExpression(eagerLoad.Predicate);

			CollectDependencies(buildContext, sequenceExpression, dependencies);

			dependencies.AddRange(previousKeys);

			var mainKeys   = new Expression[dependencies.Count];
			var detailKeys = new Expression[dependencies.Count];

			int i = 0;
			foreach (var dependency in dependencies)
			{
				mainKeys[i]   = dependency;
				detailKeys[i] = cloningContext.CloneExpression(dependency);
				++i;
			}

			Expression resultExpression;

			var mainType   = clonedParentContext.ElementType;
			var detailType = GetEnumerableElementType(eagerLoad.Type);

			if (dependencies.Count == 0)
			{
				var detailSequence = BuildSequence(new BuildInfo((IBuildContext?)null, correctedSequence, new SelectQuery()));

				var parameters = new object[] { detailSequence, correctedSequence, queryParameter, preambles };

				resultExpression = (Expression)_buildPreambleQueryDetachedMethodInfo
					.MakeGenericMethod(detailType)
					.Invoke(this, parameters)!;
			}
			else
			{
				if (correctedPredicate != null)
				{
					var predicateSql = ConvertPredicate(clonedParentContext, correctedPredicate, ProjectFlags.SQL,
						out var error);

					if (predicateSql == null)
						throw error!.CreateError();

					clonedParentContext.SelectQuery.Where.ConcatSearchCondition(new SqlSearchCondition(new SqlCondition(false, predicateSql)));
				}

				var orderByToApply = CollectOrderBy(correctedSequence);

				var mainKeyExpression   = GenerateKeyExpression(mainKeys, 0);
				var detailKeyExpression = GenerateKeyExpression(detailKeys, 0);

				var keyDetailType   = typeof(KeyDetailEnvelope<,>).MakeGenericType(mainKeyExpression.Type, detailType);
				var mainParameter   = Expression.Parameter(mainType, "m");
				var detailParameter = Expression.Parameter(detailType, "d");

				var keyDetailExpression = Expression.MemberInit(Expression.New(keyDetailType),
					Expression.Bind(keyDetailType.GetField(nameof(KeyDetailEnvelope<int, int>.Key)), detailKeyExpression),
					Expression.Bind(keyDetailType.GetField(nameof(KeyDetailEnvelope<int, int>.Detail)), detailParameter));

				var clonedParentContextRef = new ContextRefExpression(typeof(IQueryable<>).MakeGenericType(clonedParentContext.ElementType), clonedParentContext);

				Expression sourceQuery = clonedParentContextRef;

				if (!typeof(IQueryable<>).IsSameOrParentOf(sourceQuery.Type))
				{
					sourceQuery = Expression.Call(Methods.Queryable.AsQueryable.MakeGenericMethod(mainType), sourceQuery);
				}

				sourceQuery = Expression.Call(Methods.LinqToDB.SelectDistinct.MakeGenericMethod(mainType), sourceQuery);

				var selector = Expression.Lambda(keyDetailExpression, mainParameter, detailParameter);

				var detailSelectorBody = correctedSequence;

				var detailSelector = (LambdaExpression)_buildSelectManyDetailSelectorInfo
					.MakeGenericMethod(mainType, detailType).Invoke(null, new object[] { detailSelectorBody, mainParameter })!;

				var selectManyCall =
					Expression.Call(
						Methods.Queryable.SelectManyProjection.MakeGenericMethod(mainType, detailType, keyDetailType),
						sourceQuery, Expression.Quote(detailSelector), Expression.Quote(selector));

				var saveExpressionCache = _expressionCache;

				_expressionCache = saveExpressionCache.ToDictionary(p =>
						new SqlCacheKey(
							cloningContext.CorrectExpression(p.Key.Expression),
							cloningContext.CorrectContext(p.Key.Context), p.Key.ColumnDescriptor,
							cloningContext.CorrectElement(p.Key.SelectQuery), p.Key.Flags),
					p => cloningContext.CorrectExpression(p.Value), SqlCacheKey.SqlCacheKeyComparer);

				var saveColumnsCache = _columnCache;

				_columnCache = _columnCache.ToDictionary(p =>
						new ColumnCacheKey(
							cloningContext.CorrectExpression(p.Key.Expression),
							p.Key.ResultType,
							cloningContext.CorrectElement(p.Key.SelectQuery),
							cloningContext.CorrectElement(p.Key.ParentQuery)),
					p => cloningContext.CorrectExpression(p.Value), ColumnCacheKey.ColumnCacheKeyComparer);

				var saveAssociationsCache = _associations;

				_associations = _associations?.ToDictionary(p =>
						new SqlCacheKey(
							cloningContext.CorrectExpression(p.Key.Expression),
							cloningContext.CorrectContext(p.Key.Context), p.Key.ColumnDescriptor,
							cloningContext.CorrectElement(p.Key.SelectQuery), p.Key.Flags),

					p => cloningContext.CorrectExpression(p.Value), SqlCacheKey.SqlCacheKeyComparer);
				
				var saveSqlCache = _cachedSql;

				_cachedSql = _cachedSql.ToDictionary(p =>
						new SqlCacheKey(
							cloningContext.CorrectExpression(p.Key.Expression),
							cloningContext.CorrectContext(p.Key.Context), p.Key.ColumnDescriptor,
							cloningContext.CorrectElement(p.Key.SelectQuery), p.Key.Flags),
					p => cloningContext.CorrectExpression(p.Value), SqlCacheKey.SqlCacheKeyComparer);

				cloningContext.UpdateContextParents();

				var detailSequence = BuildSequence(new BuildInfo((IBuildContext?)null, selectManyCall,
					clonedParentContextRef.BuildContext.SelectQuery));

				var parameters = new object?[] { detailSequence, mainKeyExpression, selectManyCall, queryParameter, preambles, orderByToApply, detailKeys };

				resultExpression = (Expression)_buildPreambleQueryAttachedMethodInfo
					.MakeGenericMethod(mainKeyExpression.Type, detailType)
					.Invoke(this, parameters)!;

				_expressionCache = saveExpressionCache;
				_columnCache     = saveColumnsCache;
				_cachedSql       = saveSqlCache;
				_associations    = saveAssociationsCache;
			}

			if (resultExpression.Type != eagerLoad.Type)
			{
				resultExpression = new SqlAdjustTypeExpression(resultExpression, eagerLoad.Type, MappingSchema);
			}

			return resultExpression;
		}

		static Expression ApplyEnumerableOrderBy(Expression queryExpr, List<(LambdaExpression, bool)> orderBy)
		{
			var isFirst = true;
			foreach (var order in orderBy)
			{
				var methodName =
					isFirst ? order.Item2 ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy)
					: order.Item2 ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy);

				var lambda = order.Item1;
				queryExpr = Expression.Call(typeof(Enumerable), methodName, new[] { lambda.Parameters[0].Type, lambda.Body.Type }, queryExpr, lambda);
				isFirst = false;
			}

			return queryExpr;
		}

		static MethodInfo _buildSelectManyDetailSelectorInfo =
			typeof(ExpressionBuilder).GetMethod(nameof(BuildSelectManyDetailSelector), BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException();

		static LambdaExpression BuildSelectManyDetailSelector<TMain, TDetail>(Expression body, ParameterExpression mainParam)
		{
			return Expression.Lambda<Func<TMain, IEnumerable<TDetail>>>(body, mainParam);
		}

		static MethodInfo _buildPreambleQueryAttachedMethodInfo =
			typeof(ExpressionBuilder).GetMethod(nameof(BuildPreambleQueryAttached), BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException();

		Expression BuildPreambleQueryAttached<TKey, T>(
			IBuildContext       sequence, 
			Expression          keyExpression,
			Expression          queryExpression, 
			ParameterExpression queryParameter,
			List<Preamble>      preambles,
			List<(LambdaExpression, bool)>? additionalOrderBy,
			Expression[]        previousKeys) 
			where TKey : notnull
		{
			var query = new Query<KeyDetailEnvelope<TKey, T>>(DataContext, queryExpression);

			query.Init(sequence, _parametersContext.CurrentSqlParameters);

			BuildQuery(query, sequence, queryParameter, ref preambles!, previousKeys);

			var idx      = preambles.Count;
			var preamble = new Preamble<TKey, T>(query);
			preambles.Add(preamble);

			var getListMethod = MemberHelper.MethodOf((PreambleResult<TKey, T> c) => c.GetList(default!));

			Expression resultExpression =
				Expression.Call(
					Expression.Convert(Expression.ArrayIndex(PreambleParam, ExpressionInstances.Int32(idx)),
						typeof(PreambleResult<TKey, T>)), getListMethod, keyExpression);

			if (additionalOrderBy != null)
			{
				resultExpression = ApplyEnumerableOrderBy(resultExpression, additionalOrderBy);
			}


			return resultExpression;
		}

		static MethodInfo _buildPreambleQueryDetachedMethodInfo =
			typeof(ExpressionBuilder).GetMethod(nameof(BuildPreambleQueryDetached), BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException();

		Expression BuildPreambleQueryDetached<T>(
			IBuildContext       sequence, 
			Expression          queryExpression, 
			ParameterExpression queryParameter,
			List<Preamble>      preambles) 
		{
			var query = new Query<T>(DataContext, queryExpression);

			query.Init(sequence, _parametersContext.CurrentSqlParameters);

			BuildQuery(query, sequence, queryParameter, ref preambles!, Array<Expression>.Empty);

			var idx      = preambles.Count;
			var preamble = new DatachedPreamble<T>(query);
			preambles.Add(preamble);

			var resultExpression = Expression.Convert(Expression.ArrayIndex(PreambleParam, ExpressionInstances.Int32(idx)), typeof(List<T>));

			return resultExpression;
		}

		Expression CompleteEagerLoadingExpressions(
			Expression          expression,     
			IBuildContext       buildContext,
			ParameterExpression queryParameter,
			ref List<Preamble>? preambles,
			Expression[]        previousKeys)
		{
			Dictionary<Expression, Expression>? eagerLoadingCache = null;

			var preamblesLocal = preambles;

			var updatedEagerLoading = expression.Transform(e =>
			{
				if (e.NodeType == ExpressionType.Extension && e is SqlEagerLoadExpression eagerLoad)
				{
					eagerLoadingCache ??= new Dictionary<Expression, Expression>(ExpressionEqualityComparer.Instance);
					if (!eagerLoadingCache.TryGetValue(eagerLoad.SequenceExpression, out var preambleExpression))
					{
						preamblesLocal     ??= new List<Preamble>();

						preambleExpression = ProcessEagerLoadingExpression(buildContext, eagerLoad, queryParameter, preamblesLocal, previousKeys);
						eagerLoadingCache.Add(eagerLoad.SequenceExpression, preambleExpression);
					}

					return preambleExpression;
				}

				return e;
			});

			preambles = preamblesLocal;

			return updatedEagerLoading;
		}

		class DatachedPreamble<T> : Preamble
		{
			readonly Query<T> _query;

			public DatachedPreamble(Query<T> query)
			{
				_query = query;
			}

			public override object Execute(IDataContext dataContext, Expression expression, object?[]? parameters, object?[]? preambles)
			{
				return _query.GetResultEnumerable(dataContext, expression, preambles, preambles).ToList();
			}

			public override async Task<object> ExecuteAsync(IDataContext dataContext, Expression expression, object?[]? parameters, object[]? preambles, CancellationToken cancellationToken)
			{
				return await _query.GetResultEnumerable(dataContext, expression, preambles, preambles)
					.ToListAsync(cancellationToken)
					.ConfigureAwait(Configuration.ContinueOnCapturedContext);
			}
		}

		class Preamble<TKey, T> : Preamble
			where TKey : notnull
		{
			readonly Query<KeyDetailEnvelope<TKey, T>> _query;

			public Preamble(Query<KeyDetailEnvelope<TKey, T>> query)
			{
				_query = query;
			}

			public override object Execute(IDataContext dataContext, Expression expression, object?[]? parameters, object?[]? preambles)
			{
				var result = new PreambleResult<TKey, T>();
				foreach (var e in _query.GetResultEnumerable(dataContext, expression, preambles, preambles))
				{
					result.Add(e.Key, e.Detail);
				}

				return result;
			}

			public override async Task<object> ExecuteAsync(IDataContext dataContext, Expression expression, object?[]? parameters, object[]? preambles,
				CancellationToken                                  cancellationToken)
			{
				var result = new PreambleResult<TKey, T>();

				var enumerator = _query.GetResultEnumerable(dataContext, expression, preambles, preambles)
					.GetAsyncEnumerator(cancellationToken);

				while (await enumerator.MoveNextAsync().ConfigureAwait(Configuration.ContinueOnCapturedContext))
				{
					var e = enumerator.Current;
					result.Add(e.Key, e.Detail);
				}

				return result;
			}
		}

		class PreambleResult<TKey, T>
			where TKey : notnull
		{
			Dictionary<TKey, List<T>>? _items;
			TKey                       _prevKey = default!;
			List<T>?                   _prevList;

			public void Add(TKey key, T item)
			{
				List<T>? list;

				if (_prevList != null && _prevKey!.Equals(key))
				{
					list = _prevList;
				}
				else
				{
					if (_items == null)
					{
						_items = new Dictionary<TKey, List<T>>();
						list   = new List<T>();
						_items.Add(key, list);
					}
					else if (!_items.TryGetValue(key, out list))
					{
						list = new List<T>();
						_items.Add(key, list);
					}

					_prevKey  = key;
					_prevList = list;
				}

				list.Add(item);
			}

			public List<T> GetList(TKey key)
			{
				if (_items == null || !_items.TryGetValue(key, out var list))
					return new List<T>();
				return list;
			}
		}

	}
}
