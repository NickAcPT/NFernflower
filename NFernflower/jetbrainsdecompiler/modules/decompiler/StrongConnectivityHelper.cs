// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class StrongConnectivityHelper
	{
		private readonly List<List<Statement>> components;

		private readonly HashSet<Statement> setProcessed;

		private ListStack<Statement> lstack;

		private int ncounter;

		private HashSet<Statement> tset;

		private Dictionary<Statement, int> dfsnummap;

		private Dictionary<Statement, int> lowmap;

		public StrongConnectivityHelper(Statement stat)
		{
			components = new List<List<Statement>>();
			setProcessed = new HashSet<Statement>();
			VisitTree(stat.GetFirst());
			foreach (Statement st in stat.GetStats())
			{
				if (!setProcessed.Contains(st) && (st.GetPredecessorEdges(Statement.Statedge_Direct_All
					).Count == 0))
				{
					VisitTree(st);
				}
			}
			// should not find any more nodes! FIXME: ??
			foreach (Statement st in stat.GetStats())
			{
				if (!setProcessed.Contains(st))
				{
					VisitTree(st);
				}
			}
		}

		private void VisitTree(Statement stat)
		{
			lstack = new ListStack<Statement>();
			ncounter = 0;
			tset = new HashSet<Statement>();
			dfsnummap = new Dictionary<Statement, int>();
			lowmap = new Dictionary<Statement, int>();
			Visit(stat);
			Sharpen.Collections.AddAll(setProcessed, tset);
			setProcessed.Add(stat);
		}

		private void Visit(Statement stat)
		{
			lstack.Push(stat);
			Sharpen.Collections.Put(dfsnummap, stat, ncounter);
			Sharpen.Collections.Put(lowmap, stat, ncounter);
			ncounter++;
			List<Statement> lstSuccs = stat.GetNeighbours(StatEdge.Type_Regular, Statement.Direction_Forward
				);
			// TODO: set?
			lstSuccs.RemoveAll(setProcessed);
			foreach (Statement succ in lstSuccs)
			{
				int? secvalue;
				if (tset.Contains(succ))
				{
					secvalue = dfsnummap.GetOrNullable(succ);
				}
				else
				{
					tset.Add(succ);
					Visit(succ);
					secvalue = lowmap.GetOrNullable(succ);
				}
				Sharpen.Collections.Put(lowmap, stat, System.Math.Min(lowmap.GetOrNullable(stat) ?? 0, 
					secvalue ?? 0));
			}
			if (lowmap.GetOrNullable(stat) == dfsnummap.GetOrNullable(stat))
			{
				List<Statement> lst = new List<Statement>();
				Statement v;
				do
				{
					v = lstack.Pop();
					lst.Add(v);
				}
				while (v != stat);
				components.Add(lst);
			}
		}

		public static bool IsExitComponent(List<Statement> lst)
		{
			HashSet<Statement> set = new HashSet<Statement>();
			foreach (var stat in lst)
			{
				Sharpen.Collections.AddAll(set, stat.GetNeighbours(StatEdge.Type_Regular, Statement
					.Direction_Forward));
			}
			set.RemoveAll(lst);
			return (set.Count == 0);
		}

		public static List<Statement> GetExitReps(List<List<Statement>> lst)
		{
			List<Statement> res = new List<Statement>();
			foreach (var comp in lst)
			{
				if (IsExitComponent(comp))
				{
					res.Add(comp[0]);
				}
			}
			return res;
		}

		public virtual List<List<Statement>> GetComponents()
		{
			return components;
		}
	}
}
