// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Decompose
{
	public class DominatorEngine
	{
		private readonly Statement statement;

		private readonly VBStyleCollection<int?, int> colOrderedIDoms = new VBStyleCollection
			<int?, int>();

		public DominatorEngine(Statement statement)
		{
			this.statement = statement;
		}

		public virtual void Initialize()
		{
			CalcIDoms();
		}

		private void OrderStatements()
		{
			foreach (Statement stat in statement.GetReversePostOrderList())
			{
				colOrderedIDoms.AddWithKey(default, stat.id);
			}
		}

		private static int GetCommonIDom(int? key1, int? key2, VBStyleCollection<int?, int> 
			orderedIDoms)
		{
			if (key1 == null)
			{
				return key2.Value;
			}
			else if (key2 == null)
			{
				return key1.Value;
			}
			int index1 = orderedIDoms.GetIndexByKey(key1.Value);
			int index2 = orderedIDoms.GetIndexByKey(key2.Value);
			while (index1 != index2)
			{
				if (index1 > index2)
				{
					key1 = orderedIDoms.GetWithKey(key1.Value);
					index1 = orderedIDoms.GetIndexByKey(key1.Value);
				}
				else
				{
					key2 = orderedIDoms.GetWithKey(key2.Value);
					index2 = orderedIDoms.GetIndexByKey(key2.Value);
				}
			}
			return key1.Value;
		}

		private void CalcIDoms()
		{
			OrderStatements();
			colOrderedIDoms.PutWithKey(statement.GetFirst().id, statement.GetFirst().id);
			// exclude first statement
			List<int> lstIds = colOrderedIDoms.GetLstKeys().GetRange(1, colOrderedIDoms.GetLstKeys
				().Count - 1);
			while (true)
			{
				bool changed = false;
				foreach (int id in lstIds)
				{
					Statement stat = statement.GetStats().GetWithKey(id);
					int? idom = null;
					foreach (StatEdge edge in stat.GetAllPredecessorEdges())
					{
						if (colOrderedIDoms.GetWithKey(edge.GetSource().id) != null)
						{
							idom = GetCommonIDom(idom, edge.GetSource().id, colOrderedIDoms);
						}
					}
					int? oldidom = colOrderedIDoms.PutWithKey(idom, id);
					if (!idom.Equals(oldidom))
					{
						changed = true;
					}
				}
				if (!changed)
				{
					break;
				}
			}
		}

		public virtual VBStyleCollection<int?, int> GetOrderedIDoms()
		{
			return colOrderedIDoms;
		}

		public virtual bool IsDominator(int? node, int dom)
		{
			while (!node.Equals(dom))
			{
				int? idom = colOrderedIDoms.GetWithKey(node.Value);
				if (idom.Equals(node))
				{
					return false;
				}
				else
				{
					// root node
					node = idom;
				}
			}
			return true;
		}
	}
}
