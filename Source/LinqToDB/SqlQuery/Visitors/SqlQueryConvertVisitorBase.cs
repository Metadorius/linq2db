﻿using System.Collections.Generic;

namespace LinqToDB.SqlQuery.Visitors
{
	public abstract class SqlQueryConvertVisitorBase : SqlQueryVisitor
	{
		public SqlQueryConvertVisitorBase(bool allowMutation) : base(allowMutation ? VisitMode.Modify : VisitMode.Transform)
		{
		}

		public bool AllowMutation => VisitMode == VisitMode.Modify;

		public bool WithStack { get; protected set; }

		public List<IQueryElement>? Stack         { get; protected set; }
		public IQueryElement?       ParentElement => Stack?.Count > 0 ? Stack[^1] : null;

		public void RegisterReplacements(IReadOnlyDictionary<IQueryElement, IQueryElement> replacements)
		{
			AddReplacements(replacements);
		}

		public override IQueryElement NotifyReplaced(IQueryElement newElement, IQueryElement oldElement)
		{
			AddReplacement(oldElement, newElement);
			return base.NotifyReplaced(newElement, oldElement);
		}

		public override IQueryElement? Visit(IQueryElement? element)
		{
			if (element == null)
				return null;

			if (WithStack)
			{
				Stack ??= new List<IQueryElement>();
				Stack.Add(element);
			}

			if (GetReplacement(element, out var replacement))
				return replacement;

			var newElement = base.Visit(element);

			if (!ReferenceEquals(newElement, element))
			{
				NotifyReplaced(newElement, element);
			}

			var convertedElement = ConvertElement(newElement);

			if (!ReferenceEquals(convertedElement, newElement))
			{
				NotifyReplaced(convertedElement, newElement);

				// do convert again
				convertedElement = Visit(convertedElement);
			}

			if (WithStack)
			{
				Stack?.RemoveAt(Stack.Count - 1);
			}

			return convertedElement;
		}

		public abstract IQueryElement ConvertElement(IQueryElement element);
		
		public IQueryElement PerformConvert(IQueryElement element)
		{
			return ProcessElement(element);
		}
	}
}
