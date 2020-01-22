// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Decompose;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Vars
{
	public class VarVersionsGraph
	{
		public readonly VBStyleCollection<VarVersionNode, VarVersionPair> nodes = new VBStyleCollection
			<VarVersionNode, VarVersionPair>();

		private GenericDominatorEngine engine;

		public virtual VarVersionNode CreateNode(VarVersionPair ver)
		{
			VarVersionNode node;
			nodes.AddWithKey(node = new VarVersionNode(ver.var, ver.version), ver);
			return node;
		}

		public virtual void AddNodes(ICollection<VarVersionNode> colnodes, ICollection<VarVersionPair
			> colpaars)
		{
			nodes.AddAllWithKey(colnodes, colpaars);
		}

		public virtual bool IsDominatorSet(VarVersionNode node, HashSet<VarVersionNode> domnodes
			)
		{
			if (domnodes.Count == 1)
			{
				return engine.IsDominator(node, domnodes.GetEnumerator().Current);
			}
			else
			{
				HashSet<VarVersionNode> marked = new HashSet<VarVersionNode>();
				if (domnodes.Contains(node))
				{
					return true;
				}
				List<VarVersionNode> lstNodes = new LinkedList<VarVersionNode>();
				lstNodes.Add(node);
				while (!(lstNodes.Count == 0))
				{
					VarVersionNode nd = lstNodes.RemoveAtReturningValue(0);
					if (marked.Contains(nd))
					{
						continue;
					}
					else
					{
						marked.Add(nd);
					}
					if ((nd.preds.Count == 0))
					{
						return false;
					}
					foreach (VarVersionEdge edge in nd.preds)
					{
						VarVersionNode pred = edge.source;
						if (!marked.Contains(pred) && !domnodes.Contains(pred))
						{
							lstNodes.Add(pred);
						}
					}
				}
			}
			return true;
		}

		public virtual void InitDominators()
		{
			HashSet<VarVersionNode> roots = new HashSet<VarVersionNode>();
			foreach (VarVersionNode node in nodes)
			{
				if ((node.preds.Count == 0))
				{
					roots.Add(node);
				}
			}
			engine = new GenericDominatorEngine(new _IIGraph_74(roots));
			engine.Initialize();
		}

		private sealed class _IIGraph_74 : IIGraph
		{
			public _IIGraph_74(HashSet<VarVersionNode> roots)
			{
				this.roots = roots;
			}

			public List<IIGraphNode> GetReversePostOrderList()
			{
				return VarVersionsGraph.GetReversedPostOrder(roots);
			}

			public HashSet<IIGraphNode> GetRoots()
			{
				return new HashSet<IIGraphNode>(roots);
			}

			private readonly HashSet<VarVersionNode> roots;
		}

		private static List<VarVersionNode> GetReversedPostOrder(ICollection<VarVersionNode
			> roots)
		{
			List<VarVersionNode> lst = new LinkedList<VarVersionNode>();
			HashSet<VarVersionNode> setVisited = new HashSet<VarVersionNode>();
			foreach (VarVersionNode root in roots)
			{
				List<VarVersionNode> lstTemp = new LinkedList<VarVersionNode>();
				AddToReversePostOrderListIterative(root, lstTemp, setVisited);
				Sharpen.Collections.AddAll(lst, lstTemp);
			}
			return lst;
		}

		private static void AddToReversePostOrderListIterative<_T0, _T0>(VarVersionNode root
			, List<_T0> lst, HashSet<_T0> setVisited)
		{
			IDictionary<VarVersionNode, List<VarVersionEdge>> mapNodeSuccs = new Dictionary<
				VarVersionNode, List<VarVersionEdge>>();
			LinkedList<VarVersionNode> stackNode = new LinkedList<VarVersionNode>();
			LinkedList<int> stackIndex = new LinkedList<int>();
			stackNode.Add(root);
			stackIndex.Add(0);
			while (!(stackNode.Count == 0))
			{
				VarVersionNode node = stackNode.GetLast();
				int index = Sharpen.Collections.RemoveLast(stackIndex);
				setVisited.Add(node);
				List<VarVersionEdge> lstSuccs = mapNodeSuccs.ComputeIfAbsent(node, (VarVersionNode
					 n) => new List<VarVersionEdge>(n.succs));
				for (; index < lstSuccs.Count; index++)
				{
					VarVersionNode succ = lstSuccs[index].dest;
					if (!setVisited.Contains(succ))
					{
						stackIndex.Add(index + 1);
						stackNode.Add(succ);
						stackIndex.Add(0);
						break;
					}
				}
				if (index == lstSuccs.Count)
				{
					lst.Add(0, node);
					Sharpen.Collections.RemoveLast(stackNode);
				}
			}
		}
	}
}
