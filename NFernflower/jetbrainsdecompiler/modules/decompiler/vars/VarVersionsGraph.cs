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
				LinkedList<VarVersionNode> lstNodes = new LinkedList<VarVersionNode>();
				lstNodes.AddLast(node);
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
							lstNodes.AddLast(pred);
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

			public LinkedList<IGraphNode> GetReversePostOrderList()
			{
				return VarVersionsGraph.GetReversedPostOrder(roots);
			}

			public HashSet<IGraphNode> GetRoots()
			{
				return new HashSet<IGraphNode>(roots);
			}

			private readonly HashSet<VarVersionNode> roots;
		}

		private static LinkedList<IGraphNode> GetReversedPostOrder(ICollection<VarVersionNode
			> roots)
		{
			LinkedList<IGraphNode> lst = new LinkedList<IGraphNode>();
			HashSet<VarVersionNode> setVisited = new HashSet<VarVersionNode>();
			foreach (VarVersionNode root in roots)
			{
				LinkedList<VarVersionNode> lstTemp = new LinkedList<VarVersionNode>();
				AddToReversePostOrderListIterative(root, lstTemp, setVisited);
				Sharpen.Collections.AddAll(lst, lstTemp);
			}
			return lst;
		}

		private static void AddToReversePostOrderListIterative(VarVersionNode root
			, LinkedList<VarVersionNode> lst, HashSet<VarVersionNode> setVisited)
		{
			Dictionary<VarVersionNode, List<VarVersionEdge>> mapNodeSuccs = new Dictionary<
				VarVersionNode, List<VarVersionEdge>>();
			LinkedList<VarVersionNode> stackNode = new LinkedList<VarVersionNode>();
			LinkedList<int> stackIndex = new LinkedList<int>();
			stackNode.AddLast(root);
			stackIndex.AddLast(0);
			while (!(stackNode.Count == 0))
			{
				VarVersionNode node = stackNode.Last.Value;
				int index = Sharpen.Collections.RemoveLast(stackIndex);
				setVisited.Add(node);
				List<VarVersionEdge> lstSuccs = mapNodeSuccs.ComputeIfAbsent(node, (VarVersionNode
					 n) => new List<VarVersionEdge>(n.succs));
				for (; index < lstSuccs.Count; index++)
				{
					VarVersionNode succ = lstSuccs[index].dest;
					if (!setVisited.Contains(succ))
					{
						stackIndex.AddLast(index + 1);
						stackNode.AddLast(succ);
						stackIndex.AddLast(0);
						break;
					}
				}
				if (index == lstSuccs.Count)
				{
					lst.AddFirst(node);
					Sharpen.Collections.RemoveLast(stackNode);
				}
			}
		}
	}
}
