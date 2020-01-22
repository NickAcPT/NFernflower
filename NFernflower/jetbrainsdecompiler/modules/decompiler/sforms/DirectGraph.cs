// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Sforms
{
	public class DirectGraph
	{
		public readonly VBStyleCollection<DirectNode, string> nodes = new VBStyleCollection
			<DirectNode, string>();

		public DirectNode first;

		public readonly Dictionary<string, List<FlattenStatementsHelper.FinallyPathWrapper
			>> mapShortRangeFinallyPaths = new Dictionary<string, List<FlattenStatementsHelper.FinallyPathWrapper
			>>();

		public readonly Dictionary<string, List<FlattenStatementsHelper.FinallyPathWrapper
			>> mapLongRangeFinallyPaths = new Dictionary<string, List<FlattenStatementsHelper.FinallyPathWrapper
			>>();

		public readonly Dictionary<string, string> mapNegIfBranch = new Dictionary<string
			, string>();

		public readonly Dictionary<string, string> mapFinallyMonitorExceptionPathExits = 
			new Dictionary<string, string>();

		// exit, [source, destination]
		// exit, [source, destination]
		// negative if branches (recorded for handling of && and ||)
		// nodes, that are exception exits of a finally block with monitor variable
		public virtual void SortReversePostOrder()
		{
			LinkedList<DirectNode> res = new LinkedList<DirectNode>();
			AddToReversePostOrderListIterative(first, res);
			nodes.Clear();
			foreach (DirectNode node in res)
			{
				nodes.AddWithKey(node, node.id);
			}
		}

		private static void AddToReversePostOrderListIterative(DirectNode root, LinkedList<DirectNode> lst)
		{
			LinkedList<DirectNode> stackNode = new LinkedList<DirectNode>();
			LinkedList<int> stackIndex = new LinkedList<int>();
			HashSet<DirectNode> setVisited = new HashSet<DirectNode>();
			stackNode.AddLast(root);
			stackIndex.AddLast(0);
			while (!(stackNode.Count == 0))
			{
				DirectNode node = stackNode.Last.Value;
				int index = Sharpen.Collections.RemoveLast(stackIndex);
				setVisited.Add(node);
				for (; index < node.succs.Count; index++)
				{
					DirectNode succ = node.succs[index];
					if (!setVisited.Contains(succ))
					{
						stackIndex.AddLast(index + 1);
						stackNode.AddLast(succ);
						stackIndex.AddLast(0);
						break;
					}
				}
				if (index == node.succs.Count)
				{
					lst.AddFirst(node);
					Sharpen.Collections.RemoveLast(stackNode);
				}
			}
		}

		public virtual bool IterateExprents(DirectGraph.IExprentIterator iter)
		{
			return IterateExprents(iter.ProcessExprent);
		}

		public virtual bool IterateExprents(Func<Exprent, int> iter)
		{
			LinkedList<DirectNode> stack = new LinkedList<DirectNode>();
			stack.AddLast(first);
			HashSet<DirectNode> setVisited = new HashSet<DirectNode>();
			while (!(stack.Count == 0))
			{
				DirectNode node = Sharpen.Collections.RemoveFirst(stack);
				if (setVisited.Contains(node))
				{
					continue;
				}
				setVisited.Add(node);
				for (int i = 0; i < node.exprents.Count; i++)
				{
					int res = iter(node.exprents[i]);
					if (res == 1)
					{
						return false;
					}
					if (res == 2)
					{
						node.exprents.RemoveAtReturningValue(i);
						i--;
					}
				}
				Sharpen.Collections.AddAll(stack, node.succs);
			}
			return true;
		}

		public interface IExprentIterator
		{
			// 0 - success, do nothing
			// 1 - cancel iteration
			// 2 - success, delete exprent
			int ProcessExprent(Exprent exprent);
		}
	}
}
