﻿using System;
using System.Collections.Generic;
using System.Text;

namespace LinqToDB.Tools.Activity
{
	public class ActivityHierarchy : IActivity
	{
		readonly ActivityID              _activityID;
		readonly string                  _name;
		readonly Action<string>          _pushReport;
		readonly ActivityHierarchy?      _parent;
		readonly List<ActivityHierarchy> _children = new();

		int _count = 1;

		public ActivityHierarchy(ActivityID activityID, Action<string> pushReport)
		{
			_activityID = activityID;
			_pushReport = pushReport;
			_parent     = _current;
			_current    = this;
			_name       = ActivityStatistics.GetStat(activityID).Name.TrimStart();

			if (_parent != null)
			{
				if (_parent._children.Count == 0)
				{
					_parent._children.Add(this);
				}
				else
				{
					var p = _parent._children[^1];

					if (p._activityID == _activityID)
						p._count++;
					else
						_parent._children.Add(this);
				}
			}
			else if (!(activityID is ActivityID.QueryProviderExecuteT or ActivityID.QueryProviderGetEnumeratorT))
			{
			}
		}

#pragma warning disable RS0030
		[ThreadStatic]
		static ActivityHierarchy? _current;
#pragma warning restore RS0030

		public void Dispose()
		{
			_current = _parent;

			if (_parent == null)
			{
				var sb = new StringBuilder();

				Print(this, 0);

				_pushReport(sb.ToString());

				void Print(ActivityHierarchy a, int indent)
				{
					sb
						.Append(' ', indent)
						.Append(a._name);

					if (a._count > 1)
						sb.Append($" ({a._count})");

					sb.AppendLine();

					foreach (var c in a._children)
						Print(c, indent + 2);
				}
			}
		}
	}
}
