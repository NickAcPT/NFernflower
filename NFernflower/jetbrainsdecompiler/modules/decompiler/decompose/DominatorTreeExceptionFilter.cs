// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Decompose
{
	public class DominatorTreeExceptionFilter
	{
		private readonly Statement statement;

		private readonly Dictionary<int?, HashSet<int>> mapTreeBranches = new Dictionary<
			int?, HashSet<int>>();

		private readonly Dictionary<int, HashSet<int>> mapExceptionRanges = new Dictionary
			<int, HashSet<int>>();

		private Dictionary<int, int> mapExceptionDoms = new Dictionary<int, int>();

		private readonly Dictionary<int?, Dictionary<int, int>> mapExceptionRangeUniqueExit
			 = new Dictionary<int?, Dictionary<int, int>>();

		private DominatorEngine domEngine;

		public DominatorTreeExceptionFilter(Statement statement)
		{
			// idom, nodes
			// handler, range nodes
			// handler, head dom
			// statement, handler, exit nodes
			this.statement = statement;
		}

		public virtual void Initialize()
		{
			domEngine = new DominatorEngine(statement);
			domEngine.Initialize();
			BuildDominatorTree();
			BuildExceptionRanges();
			BuildFilter(statement.GetFirst().id);
			// free resources
			mapTreeBranches.Clear();
			mapExceptionRanges.Clear();
		}

		public virtual bool AcceptStatementPair(int head, int exit)
		{
			Dictionary<int, int> filter = mapExceptionRangeUniqueExit.GetOrNull(head);
			foreach (KeyValuePair<int, int> entry in filter)
			{
				if (!head.Equals(mapExceptionDoms.GetOrNullable(entry.Key)))
				{
					int filterExit = entry.Value;
					if (filterExit == -1 || !filterExit.Equals(exit))
					{
						return false;
					}
				}
			}
			return true;
		}

		private void BuildDominatorTree()
		{
			VBStyleCollection<int?, int> orderedIDoms = domEngine.GetOrderedIDoms();
			List<int> lstKeys = orderedIDoms.GetLstKeys();
			for (int index = lstKeys.Count - 1; index >= 0; index--)
			{
				int key = lstKeys[index];
				int? idom = orderedIDoms[index];
				mapTreeBranches.ComputeIfAbsent(idom, (k) => new HashSet<int>()).Add(key);
			}
			int firstid = statement.GetFirst().id;
			mapTreeBranches.GetOrNull(firstid).Remove(firstid);
		}

		private void BuildExceptionRanges()
		{
			foreach (Statement stat in statement.GetStats())
			{
				List<Statement> lstPreds = stat.GetNeighbours(StatEdge.Type_Exception, Statement
					.Direction_Backward);
				if (!(lstPreds.Count == 0))
				{
					HashSet<int> set = new HashSet<int>();
					foreach (Statement st in lstPreds)
					{
						set.Add(st.id);
					}
					Sharpen.Collections.Put(mapExceptionRanges, stat.id, set);
				}
			}
			mapExceptionDoms = BuildExceptionDoms(statement.GetFirst().id);
		}

		private Dictionary<int, int> BuildExceptionDoms(int id)
		{
			Dictionary<int, int> map = new Dictionary<int, int>();
			HashSet<int> children = mapTreeBranches.GetOrNull(id);
			if (children != null)
			{
				foreach (int childid in children)
				{
					Dictionary<int, int> mapChild = BuildExceptionDoms(childid);
					foreach (int handler in mapChild.Keys)
					{
						Sharpen.Collections.Put(map, handler, map.ContainsKey(handler) ? id : mapChild.GetOrNullable
							(handler));
					}
				}
			}
			foreach (KeyValuePair<int, HashSet<int>> entry in mapExceptionRanges)
			{
				if (entry.Value.Contains(id))
				{
					Sharpen.Collections.Put(map, entry.Key, id);
				}
			}
			return map;
		}

		private void BuildFilter(int id)
		{
			Dictionary<int, int> map = new Dictionary<int, int>();
			HashSet<int> children = mapTreeBranches.GetOrNull(id);
			if (children != null)
			{
				foreach (int childid in children)
				{
					BuildFilter(childid);
					Dictionary<int, int> mapChild = mapExceptionRangeUniqueExit.GetOrNull(childid);
					foreach (KeyValuePair<int, HashSet<int>> entry in mapExceptionRanges)
					{
						int handler = entry.Key;
						HashSet<int> range = entry.Value;
						if (range.Contains(id))
						{
							int? exit;
							if (!range.Contains(childid))
							{
								exit = childid;
							}
							else
							{
								// after replacing 'new Integer(-1)' with '-1' Eclipse throws a NullPointerException on the following line
								// could be a bug in Eclipse or some obscure specification glitch, FIXME: needs further investigation
								exit = (map.ContainsKey(handler) ? -1 : mapChild.GetOrNullable(handler));
							}
							if (exit != null)
							{
								Sharpen.Collections.Put(map, handler, exit);
							}
						}
					}
				}
			}
			Sharpen.Collections.Put(mapExceptionRangeUniqueExit, id, map);
		}

		public virtual DominatorEngine GetDomEngine()
		{
			return domEngine;
		}
	}
}
