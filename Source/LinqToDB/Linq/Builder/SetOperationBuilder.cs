﻿using System;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace LinqToDB.Linq.Builder
{
	using LinqToDB.Expressions;
	using Extensions;
	using SqlQuery;

	internal sealed class SetOperationBuilder : MethodCallBuilder
	{
		static readonly string[] MethodNames = { "Concat", "UnionAll", "Union", "Except", "Intersect", "ExceptAll", "IntersectAll" };

		#region Builder

		protected override bool CanBuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			return methodCall.Arguments.Count == 2 && methodCall.IsQueryable(MethodNames);
		}

		protected override IBuildContext BuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
		{
			var sequence1 = builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[0]));
			var sequence2 = builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[1], new SelectQuery()));

			SetOperation setOperation;
			switch (methodCall.Method.Name)
			{
				case "Concat"       : 
				case "UnionAll"     : setOperation = SetOperation.UnionAll;     break;
				case "Union"        : setOperation = SetOperation.Union;        break;
				case "Except"       : setOperation = SetOperation.Except;       break;
				case "ExceptAll"    : setOperation = SetOperation.ExceptAll;    break;
				case "Intersect"    : setOperation = SetOperation.Intersect;    break;
				case "IntersectAll" : setOperation = SetOperation.IntersectAll; break;
				default:
					throw new ArgumentException($"Invalid method name {methodCall.Method.Name}.");
			}

			var elementType = methodCall.Method.GetGenericArguments()[0];

			var needsEmulation = !builder.DataContext.SqlProviderFlags.IsAllSetOperationsSupported &&
			                     (setOperation == SetOperation.ExceptAll || setOperation == SetOperation.IntersectAll)
			                     ||
			                     !builder.DataContext.SqlProviderFlags.IsDistinctSetOperationsSupported &&
			                     (setOperation == SetOperation.Except || setOperation == SetOperation.Intersect);

			var set1 = new SubQueryContext(sequence1);
			var set2 = new SubQueryContext(sequence2);

			var setOperator = new SqlSetOperator(set2.SelectQuery, setOperation);

			set1.SelectQuery.SetOperators.Add(setOperator);

			var setContext = new SetOperationContext(setOperation, new SelectQuery(), set1, set2, methodCall);

			if (setOperation != SetOperation.UnionAll)
			{
				var sqlExpr = builder.BuildSqlExpression(setContext, new ContextRefExpression(elementType, setContext),
					buildInfo.GetFlags());
			}

			if (needsEmulation)
			{
				return setContext.Emulate();
			}

			return setContext;
		}

		#endregion

		#region Context

		sealed class SetOperationContext : SubQueryContext
		{
			public SetOperationContext(SetOperation setOperation, SelectQuery selectQuery, SubQueryContext sequence1, SubQueryContext sequence2,
				MethodCallExpression                methodCall)
				: base(sequence1, selectQuery, true)
			{
				_setOperation = setOperation;
				_sequence1    = sequence1;
				_sequence2    = sequence2;
				_methodCall   = methodCall;

				_sequence2.Parent = this;

				_type = _methodCall.Method.GetGenericArguments()[0];
			}

			readonly Type                 _type;
			readonly MethodCallExpression _methodCall;
			readonly SetOperation         _setOperation;
			readonly SubQueryContext      _sequence1;
			readonly SubQueryContext      _sequence2;
			SqlPlaceholderExpression?     _setIdPlaceholder;
			Expression?                   _setIdReference;

			int?                          _leftSetId;
			int?                          _rightSetId;

			Expression _projection1 = default!;
			Expression _projection2 = default!;

			Dictionary<Expression[], (SqlPlaceholderExpression placeholder1, SqlPlaceholderExpression placeholder2)> _pathMapping = default!;

			public override Expression MakeExpression(Expression path, ProjectFlags flags)
			{
				if (SequenceHelper.IsSameContext(path, this) &&
				    (flags.HasFlag(ProjectFlags.Root) || flags.HasFlag(ProjectFlags.AssociationRoot)))
				{
					return path;
				}

				if (flags.IsRoot() || flags.IsTraverse())
					return path;


				if (_setIdReference != null && ExpressionEqualityComparer.Instance.Equals(_setIdReference, path))
				{
					return _setIdPlaceholder!;
				}

				if (ReferenceEquals(_pathMapping, null))
				{
					InitializeProjections();
				}

				Expression projection1;
				Expression projection2;

				if (SequenceHelper.IsSameContext(path, this))
				{
					projection1 = _projection1;
					projection2 = _projection2;
				}
				else
				{
					projection1 = Builder.Project(this, path, null, 0, flags, _projection1, false);
					projection2 = Builder.Project(this, path, null, 0, flags, _projection2, false);

					// for Expression we can allow non translatable errors
					if (flags.IsExpression())
					{
						if (projection1 is SqlErrorExpression)
							projection1 = Expression.Default(path.Type);
						if (projection2 is SqlErrorExpression)
							projection2 = Expression.Default(path.Type);
					}

					if (projection1 is SqlErrorExpression || projection2 is SqlErrorExpression)
					{
						return ExpressionBuilder.CreateSqlError(this, path);
					}
				}

				var projection = MergeProjections(path, projection1, projection2, flags);

				// remap fields
				var result = RemapPathToPlaceholders(projection, _pathMapping!);

				return result;
			}

			static Expression RemapPathToPlaceholders(Expression expression,
				Dictionary<Expression[], (SqlPlaceholderExpression placeholder1, SqlPlaceholderExpression placeholder2)> pathMapping)
			{
				var result = expression.Transform(e =>
				{
					if (e is SqlPathExpression pathExpression)
					{
						if (!pathMapping!.TryGetValue(pathExpression.Path, out var pair))
						{
							throw new InvalidOperationException("Could not find required path for projection.");
						}

						Expression result = pair.placeholder1;

						if (result.Type != e.Type)
						{
							result = Expression.Convert(result, e.Type);
						}

						return result;
					}

					if (e is SqlEagerLoadExpression eager)
					{
						return eager.Update(RemapPathToPlaceholders(eager.SequenceExpression, pathMapping), eager.Predicate);
					}

					return e;
				});

				return result;
			}

			Expression MergeProjections(Expression path, Expression projection1, Expression projection2, ProjectFlags flags)
			{
				if (TryMergeProjections(projection1, projection2, flags, out var merged))
					return merged;

				if (_setOperation != SetOperation.UnionAll)
				{
					throw new LinqToDBException(
						$"Could not decide which construction type to use `query.Select(x => new {projection1.Type.Name} {{ ... }})` to specify projection.");
				}

				EnsureSetIdFieldCreated();

				var sequenceLeftSetId = _leftSetId!.Value;

				if (projection1.Type != path.Type)
				{
					projection1 = Expression.Convert(projection1, path.Type);
				}

				if (projection2.Type != path.Type)
				{
					projection2 = Expression.Convert(projection2, path.Type);
				}

				var resultExpr = Expression.Condition(
					Expression.Equal(_setIdReference!, Expression.Constant(sequenceLeftSetId)),
					projection1,
					projection2
				);

				return resultExpr;
			}

			bool IsNullValueOrSqlNull(Expression expression)
			{
				if (expression.IsNullValue())
					return true;

				if (expression is SqlPlaceholderExpression placeholder)
					return QueryHelper.IsNullValue(placeholder.Sql);

				return false;
			}

			bool TryMergeProjections(Expression projection1, Expression projection2, ProjectFlags flags, [NotNullWhen(true)] out Expression? merged)
			{
				merged = null;

				if (projection1.Type != projection2.Type)
					return false;

				if (ExpressionEqualityComparer.Instance.Equals(projection1, projection2))
				{
					merged = projection1;
					return true;
				}

				if (projection1 is SqlGenericConstructorExpression generic1 &&
				    projection2 is SqlGenericConstructorExpression generic2)
				{
					if (generic1.ConstructType == SqlGenericConstructorExpression.CreateType.Full)
					{
						if (generic2.ConstructType != SqlGenericConstructorExpression.CreateType.Full)
						{
							var constructed = Builder.TryConstruct(Builder.MappingSchema, generic1, this, flags);
							if (constructed == null)
								return false;
							if (TryMergeProjections(SqlGenericConstructorExpression.Parse(constructed), generic2, flags, out merged))
								return true;
							return false;
						}
					}

					if (generic2.ConstructType == SqlGenericConstructorExpression.CreateType.Full)
					{
						if (generic1.ConstructType != SqlGenericConstructorExpression.CreateType.Full)
						{
							var constructed = Builder.TryConstruct(Builder.MappingSchema, generic2, this, flags);
							if (constructed == null)
								return false;
							if (TryMergeProjections(generic1, SqlGenericConstructorExpression.Parse(constructed), flags, out merged))
								return true;
							return false;
						}
					}

					var resultAssignments = new List<SqlGenericConstructorExpression.Assignment>(generic1.Assignments.Count);

					foreach (var a1 in generic1.Assignments)
					{
						var found = generic2.Assignments.FirstOrDefault(a2 =>
							MemberInfoComparer.Instance.Equals(a2.MemberInfo, a1.MemberInfo));

						if (found == null)
							resultAssignments.Add(a1);
						else if (!TryMergeProjections(a1.Expression, found.Expression, flags, out var mergedAssignment))
							return false;
						else
							resultAssignments.Add(a1.WithExpression(mergedAssignment));
					}

					foreach (var a2 in generic2.Assignments)
					{
						var found = generic1.Assignments.FirstOrDefault(a1 =>
							MemberInfoComparer.Instance.Equals(a2.MemberInfo, a1.MemberInfo));

						if (found == null)
							resultAssignments.Add(a2);
					}

					if (generic1.Parameters.Count > 0 || generic2.Parameters.Count > 0)
					{
						throw new NotImplementedException("Handling parameters not implemented yet.");
					}

					var resultGeneric = generic1.ReplaceAssignments(resultAssignments);

					if (Builder.TryConstruct(Builder.MappingSchema, resultGeneric, this, flags) == null)
						return false;

					merged = resultGeneric;
					return true;
				}

				if (projection1 is ConditionalExpression cond1 && projection2 is ConditionalExpression cond2)
				{
					if (!ExpressionEqualityComparer.Instance.Equals(cond1.Test, cond2.Test))
						return false;

					if (!TryMergeProjections(cond1.IfTrue, cond2.IfTrue, flags, out var ifTrueMerged) ||
					    !TryMergeProjections(cond1.IfFalse, cond2.IfFalse, flags, out var ifFalseMerged))
					{
						return false;
					}

					merged = cond1.Update(cond1.Test, ifTrueMerged, ifFalseMerged);
					return true;
				}

				if (projection1 is SqlPathExpression && IsNullValueOrSqlNull(projection2))
				{
					merged = projection1;
					return true;
				}

				if (projection2 is SqlPathExpression && IsNullValueOrSqlNull(projection1))
				{
					merged = projection2;
					return true;
				}

				return false;
			}

			class PathComparer : IEqualityComparer<Expression[]>
			{
				public static readonly PathComparer Instance = new PathComparer();

				public bool Equals(Expression[]? x, Expression[]? y)
				{
					if (ReferenceEquals(x, y))
						return true;

					if (x == null || y == null) 
						return false;

					return x.SequenceEqual(y, ExpressionEqualityComparer.Instance);
				}

				public int GetHashCode(Expression[] obj)
				{
					return obj.Aggregate(0, (acc, val) => acc ^ ExpressionEqualityComparer.Instance.GetHashCode(val!));
				}
			}

			void InitializeProjections()
			{
				var ref1 = new ContextRefExpression(ElementType, _sequence1);

				_projection1 = BuildProjectionExpression(ref1, _sequence1, out var placeholders1, out var eager1);

				var ref2 = new ContextRefExpression(ElementType, _sequence2);

				_projection2 = BuildProjectionExpression(ref2, _sequence2, out var placeholders2, out var eager2);

				var pathMapping = new Dictionary<Expression[], (SqlPlaceholderExpression placeholder1, SqlPlaceholderExpression placeholder2)>(PathComparer.Instance);

				switch (_setOperation)
				{
					case SetOperation.Union:
						break;
					case SetOperation.UnionAll:
						break;
					case SetOperation.Except:
					case SetOperation.ExceptAll:
						_projection2 = _projection1;
						eager2       = eager1;
						break;
					case SetOperation.Intersect:
						break;
					case SetOperation.IntersectAll:
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				foreach (var p in placeholders1)
				{
					var placeholderPath = new SqlPathExpression(p.path, p.placeholder.Type);
					var alias           = GenerateColumnAlias(placeholderPath);

					var placeholder1 = (SqlPlaceholderExpression)Builder.UpdateNesting(_sequence1, p.placeholder);
					placeholder1 = (SqlPlaceholderExpression)SequenceHelper.CorrectSelectQuery(placeholder1, _sequence1.SelectQuery);
					placeholder1 = placeholder1.WithPath(placeholderPath).WithAlias(alias);

					var column1 = Builder.MakeColumn(SelectQuery, placeholder1.WithAlias(alias), true);

					var (placeholder2, _) = placeholders2.FirstOrDefault(p2 => PathComparer.Instance.Equals(p2.path, p.path));
					if (placeholder2 == null)
					{
						placeholder2 = ExpressionBuilder.CreatePlaceholder(_sequence2,
							new SqlValue(QueryHelper.GetDbDataType(p.placeholder.Sql), null), placeholderPath);
					}
					else
					{
						placeholder2 = (SqlPlaceholderExpression)Builder.UpdateNesting(_sequence2, placeholder2);
						placeholder2 = (SqlPlaceholderExpression)SequenceHelper.CorrectSelectQuery(placeholder2, _sequence2.SelectQuery);
					}

					placeholder2 = placeholder2.WithPath(placeholderPath).WithAlias(alias);
					var column2 = Builder.MakeColumn(SelectQuery, placeholder2.WithAlias(alias), true);

					pathMapping.Add(p.path, (column1, column2));
				}

				if (_setOperation != SetOperation.Except || _setOperation != SetOperation.ExceptAll)
				{
					foreach (var p2 in placeholders2)
					{
						if (pathMapping.ContainsKey(p2.path))
							continue;

						var placeholder2 = (SqlPlaceholderExpression)Builder.UpdateNesting(_sequence2, p2.placeholder);
						placeholder2 = (SqlPlaceholderExpression)SequenceHelper.CorrectSelectQuery(placeholder2, _sequence2.SelectQuery);

						var placeholderPath = new SqlPathExpression(p2.path, placeholder2.Type);
						var alias           = GenerateColumnAlias(placeholderPath);

						placeholder2 = placeholder2.WithAlias(alias);

						var column1 = ExpressionBuilder.CreatePlaceholder(_sequence1, new SqlValue(QueryHelper.GetDbDataType(placeholder2.Sql), null), placeholderPath);
						column1 = Builder.MakeColumn(SelectQuery, column1, true);

						var column2 = Builder.MakeColumn(SelectQuery, placeholder2, true);

						pathMapping.Add(p2.path, (column1, column2));
					}
				}

				Dictionary<SqlEagerLoadExpression, SqlEagerLoadExpression>? eagerMapping = null;
				foreach (var e1 in eager1)
				{
					if (eagerMapping?.ContainsKey(e1) == true)
						continue;

					var found = eager2.FirstOrDefault(e2 => ExpressionEqualityComparer.Instance.Equals(e2, e1));

					eagerMapping ??= new(ExpressionEqualityComparer.Instance);

					if (found != null)
					{
						eagerMapping.Add(e1, e1);
					}
					else
					{
						var predicate = Expression.Equal(GetSetIdReference(), Expression.Constant(_leftSetId));
						eagerMapping.Add(e1, e1.AppendPredicate(predicate));
					}
				}

				foreach (var e2 in eager2)
				{
					if (eagerMapping?.ContainsKey(e2) == true)
						continue;

					eagerMapping ??= new(ExpressionEqualityComparer.Instance);

					var predicate = Expression.Equal(GetSetIdReference(), Expression.Constant(_rightSetId));
					eagerMapping.Add(e2, e2.AppendPredicate(predicate));
				}

				if (eagerMapping != null)
				{
					_projection1 = ReplaceEagerExpressions(_projection1, eagerMapping);
					_projection2 = ReplaceEagerExpressions(_projection2, eagerMapping);
				}

				_pathMapping  = pathMapping;
			}

			static Expression ReplaceEagerExpressions(Expression expression, Dictionary<SqlEagerLoadExpression, SqlEagerLoadExpression> raplacements)
			{
				var result = expression.Transform(e =>
				{
					if (e is SqlEagerLoadExpression eager)
					{
						if (raplacements.TryGetValue(eager, out var newEager))
							return newEager;
					}

					return e;
				});

				return result;
			}

			static string? GenerateColumnAlias(Expression expr)
			{
				var     current = expr;
				string? alias   = null;
				while (current is MemberExpression memberExpression)
				{
					if (alias != null)
						alias = memberExpression.Member.Name + "_" + alias;
					else
						alias = memberExpression.Member.Name;
					current = memberExpression.Expression;
				}

				return alias;
			}

			const string ProjectionSetIdFieldName = "__projection__set_id__";

			Expression GetSetIdReference()
			{
				if (_setIdReference == null)
				{
					var thisRef = new ContextRefExpression(_type, this);
					_setIdReference = SequenceHelper.CreateSpecialProperty(thisRef, typeof(int), ProjectionSetIdFieldName);

					_leftSetId  = Builder.GenerateSetId(_sequence1.SubQuery.SelectQuery.SourceID);
					_rightSetId = Builder.GenerateSetId(_sequence2.SubQuery.SelectQuery.SourceID);
				}

				return _setIdReference;
			}

			void EnsureSetIdFieldCreated()
			{
				if (_setIdPlaceholder != null)
					return;

				var setIdReference = GetSetIdReference();

				var sqlValueLeft  = new SqlValue(_leftSetId!);
				var sqlValueRight = new SqlValue(_rightSetId!);

				var leftRef  = new ContextRefExpression(_type, _sequence1);
				var rightRef = new ContextRefExpression(_type, _sequence2);

				var keyLeft  = SequenceHelper.CreateSpecialProperty(leftRef, typeof(int), ProjectionSetIdFieldName);
				var keyRight = SequenceHelper.CreateSpecialProperty(rightRef, typeof(int), ProjectionSetIdFieldName);

				var leftIdPlaceholder =
					ExpressionBuilder.CreatePlaceholder(_sequence1, sqlValueLeft, keyLeft, alias : ProjectionSetIdFieldName);
				leftIdPlaceholder = (SqlPlaceholderExpression)Builder.UpdateNesting(this, leftIdPlaceholder);

				var rightIdPlaceholder = ExpressionBuilder.CreatePlaceholder(_sequence2, sqlValueRight,
					keyRight, alias : ProjectionSetIdFieldName);
				rightIdPlaceholder = Builder.MakeColumn(SelectQuery, rightIdPlaceholder, asNew : true);

				_setIdPlaceholder = leftIdPlaceholder.WithPath(setIdReference).WithTrackingPath(setIdReference);
			}

			Expression ResolveReferences(IBuildContext context, Expression expression, ProjectFlags flags, HashSet<SqlPlaceholderExpression> placeholders)
			{
				var transformed = expression.Transform(e =>
				{
					if (e.NodeType == ExpressionType.MemberAccess)
					{
						var newExpr = Builder.ConvertToSqlExpr(context, e, flags.SqlFlag());
						if (newExpr is SqlPlaceholderExpression placeholder)
							placeholders.Add(placeholder);
						if (newExpr.UnwrapConvert() is SqlEagerLoadExpression eager)
						{
							newExpr = eager.SequenceExpression;
							if (e.Type != newExpr.Type)
							{
								newExpr = new SqlAdjustTypeExpression(newExpr, e.Type, Builder.MappingSchema);
							}

							return new TransformInfo(newExpr, false, true);
						}

						if (newExpr is SqlErrorExpression)
							return new TransformInfo(e);
						return new TransformInfo(newExpr);;
					}

					return new TransformInfo(e);
				});

				return transformed;
			}

			class ExpressionOptimizerVisitor : ExpressionVisitorBase
			{
				protected override Expression VisitConditional(ConditionalExpression node)
				{
					var newNode = base.VisitConditional(node);
					if (!ReferenceEquals(newNode, node))
						return Visit(newNode);

					if (node.IfTrue is ConditionalExpression condTrue                        &&
					    ExpressionEqualityComparer.Instance.Equals(node.Test, condTrue.Test) &&  
					    ExpressionEqualityComparer.Instance.Equals(node.IfFalse, condTrue.IfFalse))
					{
						return condTrue;
					}

					return node;
				}
			}

			class ExpressionPathVisitor : ExpressionVisitorBase
			{
				Stack<Expression> _stack = new();

				bool _isDictionary;

				public List<(SqlPlaceholderExpression placeholder, Expression[] path)> FoundPlaceholders { get; } = new();

				//public List<(SqlEagerLoadExpression eager, Expression[] path)> FoundEager { get; } = new();
				public List<SqlEagerLoadExpression> FoundEager { get; } = new();

				protected override Expression VisitConditional(ConditionalExpression node)
				{
					_stack.Push(Expression.Constant("?"));
					_stack.Push(Visit(node.Test));
					
					_stack.Push(Expression.Constant(true));
					var ifTrue = Visit(node.IfTrue);
					_stack.Pop();

					_stack.Push(Expression.Constant(false));
					var ifFalse = Visit(node.IfFalse);
					_stack.Pop();

					var test = _stack.Pop();
					_stack.Pop();

					return node.Update(test, ifTrue, ifFalse);
				}

				protected override Expression VisitBinary(BinaryExpression node)
				{
					_stack.Push(Expression.Constant("binary"));
					_stack.Push(Expression.Constant(node.NodeType));
					_stack.Push(Visit(node.Left));
					_stack.Push(Visit(node.Right));

					var right = _stack.Pop();
					var left  = _stack.Pop();

					_stack.Pop();
					_stack.Pop();

					return node.Update(left, node.Conversion, right);
				}

				public override Expression VisitSqlPlaceholderExpression(SqlPlaceholderExpression node)
				{
					var stack = _stack.ToArray();
					Array.Reverse(stack);

					FoundPlaceholders.Add((node, stack));

					return new SqlPathExpression(stack, node.Type);
				}

				internal override Expression VisitSqlGenericConstructorExpression(SqlGenericConstructorExpression node)
				{
					_stack.Push(Expression.Constant("construct"));

					if (node.Assignments.Count > 0)
					{
						var newAssignments = new List<SqlGenericConstructorExpression.Assignment>(node.Assignments.Count);

						foreach(var a in node.Assignments)
						{
							var memberInfo = a.MemberInfo.DeclaringType?.GetMemberEx(a.MemberInfo) ?? a.MemberInfo;

							_stack.Push(Expression.Constant(memberInfo));
							newAssignments.Add(a.WithExpression(Visit(a.Expression)));
							_stack.Pop();
						}

						node = node.ReplaceAssignments(newAssignments);
					}

					if (node.Parameters.Count > 0)
					{
						var newParameters = new List<SqlGenericConstructorExpression.Parameter>(node.Parameters.Count);
						for (var index = 0; index < node.Parameters.Count; index++)
						{
							var param = node.Parameters[index];

							if (param.MemberInfo != null)
							{
								// mimic assignment
								var memberInfo = param.MemberInfo.DeclaringType?.GetMemberEx(param.MemberInfo) ?? param.MemberInfo;
								_stack.Push(Expression.Constant(memberInfo));
								newParameters.Add(param.WithExpression(Visit(param.Expression)));
								_stack.Pop();
							}
							else
							{
								_stack.Push(Expression.Constant(index));
								newParameters.Add(param.WithExpression(Visit(param.Expression)));
								_stack.Pop();
							}
						}

						node = node.ReplaceParameters(newParameters);
					}

					_stack.Pop();

					return node;
				}

				protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
				{
					_stack.Push(Expression.Constant(node.BindingType));
					_stack.Push(Expression.Constant(node.Member));

					var newNode = base.VisitMemberAssignment(node);

					_stack.Pop();
					_stack.Pop();
					return newNode;
				}

				protected override MemberBinding VisitMemberBinding(MemberBinding node)
				{
					_stack.Push(Expression.Constant(node.BindingType));
					_stack.Push(Expression.Constant(node.Member));

					var newNode = base.VisitMemberBinding(node);

					_stack.Pop();
					_stack.Pop();
					return newNode;
				}

				protected override MemberListBinding VisitMemberListBinding(MemberListBinding node)
				{
					_stack.Push(Expression.Constant(node.BindingType));
					_stack.Push(Expression.Constant(node.Member));

					var newNode = base.VisitMemberListBinding(node);

					_stack.Pop();
					_stack.Pop();
					return newNode;
				}

				protected override Expression VisitMemberInit(MemberInitExpression node)
				{
					_stack.Push(Expression.Constant("init"));

					var newExpr = base.VisitMemberInit(node);

					_stack.Pop();

					return newExpr;
				}

				protected override ElementInit VisitElementInit(ElementInit node)
				{
					_stack.Push(Expression.Constant(node.AddMethod));

					var arguments = new List<Expression>(node.Arguments.Count);

					for (int i = 0; i < node.Arguments.Count; i++)
					{
						_stack.Push(Expression.Constant(i));

						var nodeArgument = node.Arguments[i];

						/*
						if (_isDictionary && i == 0)
						{
							_stack.Push(nodeArgument);
						}
						*/

						var arg = Visit(nodeArgument);

						/*
						if (_isDictionary && i == 0)
						{
							_stack.Pop();
						}
						*/

						_stack.Pop();
						arguments.Add(arg);
					}

					var newNode = node.Update(arguments);

					_stack.Pop();

					return newNode;
				}

				protected override Expression VisitListInit(ListInitExpression node)
				{
					_stack.Push(Expression.Constant("list init"));

					var initializers = new List<ElementInit>(node.Initializers.Count);

					var saveIDictionary = _isDictionary;
					_isDictionary = typeof(IDictionary<,>).IsSameOrParentOf(node.Type);

					for (int i = 0; i < node.Initializers.Count; i++)
					{
						_stack.Push(Expression.Constant(i));
						initializers.Add(VisitElementInit(node.Initializers[i]));
						_stack.Pop();
					}

					_isDictionary = saveIDictionary;

					var newExpr = node.Update((NewExpression)Visit(node.NewExpression), initializers);

					_stack.Pop();

					return newExpr;
				}

				protected override Expression VisitMethodCall(MethodCallExpression node)
				{
					_stack.Push(Expression.Constant("call"));

					var obj = Visit(node.Object);
					if (obj != null)
						_stack.Push(Expression.Constant(obj));

					_stack.Push(Expression.Constant(node.Method));

					var args = new List<Expression>(node.Arguments.Count);

					for (var index = 0; index < node.Arguments.Count; index++)
					{
						var arg = node.Arguments[index];
						_stack.Push(Expression.Constant(index));
						args.Add(Visit(arg));
						_stack.Pop();
					}

					_stack.Pop();

					if (obj != null)
						_stack.Pop();

					_stack.Pop();

					return node.Update(obj, args);
				}

				internal override Expression VisitSqlEagerLoadExpression(SqlEagerLoadExpression node)
				{
					var saveStack = _stack;
					_stack = new();

					var newEager  = node.Update(Visit(node.SequenceExpression), Visit(node.Predicate));

					_stack = saveStack;

					FoundEager.Add(newEager);

					return newEager;
				}
			}

			Expression BuildProjectionExpression(Expression path, IBuildContext context, 
				out List<(SqlPlaceholderExpression placeholder, Expression[] path)> foundPlaceholders,
				out List<SqlEagerLoadExpression> foundEager)
			{
				var correctedPath = SequenceHelper.ReplaceContext(path, this, context);

				var current = correctedPath;
				do
				{
					var projected = Builder.BuildSqlExpression(context, current, ProjectFlags.Expression, buildFlags: ExpressionBuilder.BuildFlags.ForceAssignments);
				
					var projectVisitor = new ProjectionVisitor(context);
					projected = projectVisitor.Visit(projected);

					var lambdaResolver = new LambdaResolveVisitor(context);
					projected = lambdaResolver.Visit(projected);


					var optimizer = new ExpressionOptimizerVisitor();
					projected = optimizer.Visit(projected);

					if (ExpressionEqualityComparer.Instance.Equals(projected, current))
						break;

					current = projected;
				} while (true);


				var pathBuilder = new ExpressionPathVisitor();
				var withPath    = pathBuilder.Visit(current);

				foundPlaceholders = pathBuilder.FoundPlaceholders;

				foundEager = pathBuilder.FoundEager;

				return withPath;
			}

			// For Set we have to ensure hat columns are not optimized
			protected override bool OptimizeColumns => false;

			public override IBuildContext Clone(CloningContext context)
			{
				var cloned = new SetOperationContext(_setOperation, context.CloneElement(SelectQuery),
					context.CloneContext(_sequence1), context.CloneContext(_sequence2),
					context.CloneExpression(_methodCall));

				// for correct updating self-references below
				context.RegisterCloned(this, cloned);

				cloned._setIdPlaceholder = context.CloneExpression(_setIdPlaceholder);
				cloned._setIdReference   = context.CloneExpression(_setIdReference);
				cloned._leftSetId        = _leftSetId;
				cloned._rightSetId       = _rightSetId;
				
				return cloned;
			}

			public override IBuildContext? GetContext(Expression expression, BuildInfo buildInfo)
			{
				return this;
			}

			public IBuildContext Emulate()
			{
				// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
				if (_projection1 == null)
					InitializeProjections();

				var sequence = _sequence1;
				var query    = _sequence2;
				var except   = query.SelectQuery;

				var sql = sequence.SelectQuery;

				if (_setOperation == SetOperation.Except || _setOperation == SetOperation.Intersect)
					sql.Select.IsDistinct = true;

				if (_setOperation == SetOperation.Except || _setOperation == SetOperation.ExceptAll)
					sql.Where.Not.Exists(except);
				else
					sql.Where.Exists(except);

				var sc = new SqlSearchCondition();

				for (int i = 0; i < _sequence1.SelectQuery.Select.Columns.Count; i++)
				{
					var column1 = _sequence1.SelectQuery.Select.Columns[i];
					var column2 = _sequence2.SelectQuery.Select.Columns[i];

					sc.Conditions.Add(new SqlCondition(false,
						new SqlPredicate.ExprExpr(column1.Expression, SqlPredicate.Operator.Equal, column2.Expression,
							Builder.DataOptions.LinqOptions.CompareNullsAsValues)));
				}

				_sequence2.SelectQuery.Select.Columns.Clear();

				except.Where.EnsureConjunction().ConcatSearchCondition(sc);

				sequence.SelectQuery.SetOperators.Clear();

				SubQuery.Parent = null;

				return SubQuery;
			}
		}

		#endregion
	}
}
