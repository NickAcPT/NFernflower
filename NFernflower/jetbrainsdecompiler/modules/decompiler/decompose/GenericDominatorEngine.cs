// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Decompose
{
	public class GenericDominatorEngine
	{
		private readonly IIGraph graph;

		private readonly VBStyleCollection<IIGraphNode, IIGraphNode> colOrderedIDoms = new 
			VBStyleCollection<IIGraphNode, IIGraphNode>();

		private HashSet<IIGraphNode> setRoots;

		public GenericDominatorEngine(IIGraph graph)
		{
			this.graph = graph;
		}

		public virtual void Initialize()
		{
			CalcIDoms();
		}

		private void OrderNodes()
		{
			setRoots = graph.GetRoots();
			foreach (var node in graph.GetReversePostOrderList())
			{
				colOrderedIDoms.AddWithKey(null, node);
			}
		}

		private static IIGraphNode GetCommonIDom(IIGraphNode node1, IIGraphNode node2, VBStyleCollection
			<IIGraphNode, IIGraphNode> orderedIDoms)
		{
			IIGraphNode nodeOld;
			if (node1 == null)
			{
				return node2;
			}
			else if (node2 == null)
			{
				return node1;
			}
			int index1 = orderedIDoms.GetIndexByKey(node1);
			int index2 = orderedIDoms.GetIndexByKey(node2);
			while (index1 != index2)
			{
				if (index1 > index2)
				{
					nodeOld = node1;
					node1 = orderedIDoms.GetWithKey(node1);
					if (nodeOld == node1)
					{
						// no idom - root or merging point
						return null;
					}
					index1 = orderedIDoms.GetIndexByKey(node1);
				}
				else
				{
					nodeOld = node2;
					node2 = orderedIDoms.GetWithKey(node2);
					if (nodeOld == node2)
					{
						// no idom - root or merging point
						return null;
					}
					index2 = orderedIDoms.GetIndexByKey(node2);
				}
			}
			return node1;
		}

		private void CalcIDoms()
		{
			OrderNodes();
			List<IIGraphNode> lstNodes = colOrderedIDoms.GetLstKeys();
			while (true)
			{
				bool changed = false;
				foreach (IIGraphNode node in lstNodes)
				{
					IIGraphNode idom = null;
					if (!setRoots.Contains(node))
					{
						foreach (var pred in node.GetPredecessors())
						{
							if (colOrderedIDoms.GetWithKey(pred) != null)
							{
								idom = GetCommonIDom(idom, pred, colOrderedIDoms);
								if (idom == null)
								{
									break;
								}
							}
						}
					}
					// no idom found: merging point of two trees
					if (idom == null)
					{
						idom = node;
					}
					IIGraphNode oldidom = colOrderedIDoms.PutWithKey(idom, node);
					if (!idom.Equals(oldidom))
					{
						// oldidom is null iff the node is touched for the first time
						changed = true;
					}
				}
				if (!changed)
				{
					break;
				}
			}
		}

		public virtual bool IsDominator(IIGraphNode node, IIGraphNode dom)
		{
			while (!node.Equals(dom))
			{
				IIGraphNode idom = colOrderedIDoms.GetWithKey(node);
				if (idom == node)
				{
					return false;
				}
				else if (idom == null)
				{
					// root node or merging point
					throw new Exception("Inconsistent idom sequence discovered!");
				}
				else
				{
					node = idom;
				}
			}
			return true;
		}
	}
}
