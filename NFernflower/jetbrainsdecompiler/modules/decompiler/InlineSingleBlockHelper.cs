// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class InlineSingleBlockHelper
	{
		public static bool InlineSingleBlocks(RootStatement root)
		{
			bool res = InlineSingleBlocksRec(root);
			if (res)
			{
				SequenceHelper.CondenseSequences(root);
			}
			return res;
		}

		private static bool InlineSingleBlocksRec(Statement stat)
		{
			bool res = false;
			foreach (Statement st in stat.GetStats())
			{
				res |= InlineSingleBlocksRec(st);
			}
			if (stat.type == Statement.Type_Sequence)
			{
				SequenceStatement seq = (SequenceStatement)stat;
				for (int i = 1; i < seq.GetStats().Count; i++)
				{
					if (IsInlineable(seq, i))
					{
						InlineBlock(seq, i);
						return true;
					}
				}
			}
			return res;
		}

		private static void InlineBlock(SequenceStatement seq, int index)
		{
			Statement first = seq.GetStats()[index];
			Statement pre = seq.GetStats()[index - 1];
			pre.RemoveSuccessor(pre.GetAllSuccessorEdges()[0]);
			// single regular edge
			StatEdge edge = first.GetPredecessorEdges(StatEdge.Type_Break)[0];
			Statement source = edge.GetSource();
			Statement parent = source.GetParent();
			source.RemoveSuccessor(edge);
			List<Statement> lst = new List<Statement>();
			for (int i = seq.GetStats().Count - 1; i >= index; i--)
			{
				lst.Add(0, seq.GetStats().RemoveAtReturningValue(i));
			}
			if (parent.type == Statement.Type_If && ((IfStatement)parent).iftype == IfStatement
				.Iftype_If && source == parent.GetFirst())
			{
				IfStatement ifparent = (IfStatement)parent;
				SequenceStatement block = new SequenceStatement(lst);
				block.SetAllParent();
				StatEdge newedge = new StatEdge(StatEdge.Type_Regular, source, block);
				source.AddSuccessor(newedge);
				ifparent.SetIfEdge(newedge);
				ifparent.SetIfstat(block);
				ifparent.GetStats().AddWithKey(block, block.id);
				block.SetParent(ifparent);
			}
			else
			{
				lst.Add(0, source);
				SequenceStatement block = new SequenceStatement(lst);
				block.SetAllParent();
				parent.ReplaceStatement(source, block);
				// LabelHelper.lowContinueLabels not applicable because of forward continue edges
				// LabelHelper.lowContinueLabels(block, new HashSet<StatEdge>());
				// do it by hand
				foreach (StatEdge prededge in block.GetPredecessorEdges(StatEdge.Type_Continue))
				{
					block.RemovePredecessor(prededge);
					prededge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, prededge, source
						);
					source.AddPredecessor(prededge);
					source.AddLabeledEdge(prededge);
				}
				if (parent.type == Statement.Type_Switch)
				{
					((SwitchStatement)parent).SortEdgesAndNodes();
				}
				source.AddSuccessor(new StatEdge(StatEdge.Type_Regular, source, first));
			}
		}

		private static bool IsInlineable(SequenceStatement seq, int index)
		{
			Statement first = seq.GetStats()[index];
			Statement pre = seq.GetStats()[index - 1];
			if (pre.HasBasicSuccEdge())
			{
				return false;
			}
			List<StatEdge> lst = first.GetPredecessorEdges(StatEdge.Type_Break);
			if (lst.Count == 1)
			{
				StatEdge edge = lst[0];
				if (SameCatchRanges(edge))
				{
					if (!edge.@explicit)
					{
						for (int i = index; i < seq.GetStats().Count; i++)
						{
							if (!NoExitLabels(seq.GetStats()[i], seq))
							{
								return false;
							}
						}
					}
					return true;
				}
			}
			// FIXME: count labels properly
			return false;
		}

		private static bool SameCatchRanges(StatEdge edge)
		{
			Statement from = edge.GetSource();
			Statement to = edge.GetDestination();
			while (true)
			{
				Statement parent = from.GetParent();
				if (parent.ContainsStatementStrict(to))
				{
					break;
				}
				if (parent.type == Statement.Type_Trycatch || parent.type == Statement.Type_Catchall)
				{
					if (parent.GetFirst() == from)
					{
						return false;
					}
				}
				else if (parent.type == Statement.Type_Syncronized)
				{
					if (parent.GetStats()[1] == from)
					{
						return false;
					}
				}
				from = parent;
			}
			return true;
		}

		private static bool NoExitLabels(Statement block, Statement sequence)
		{
			foreach (StatEdge edge in block.GetAllSuccessorEdges())
			{
				if (edge.GetType() != StatEdge.Type_Regular && edge.GetDestination().type != Statement
					.Type_Dummyexit)
				{
					if (!sequence.ContainsStatementStrict(edge.GetDestination()))
					{
						return false;
					}
				}
			}
			foreach (Statement st in block.GetStats())
			{
				if (!NoExitLabels(st, sequence))
				{
					return false;
				}
			}
			return true;
		}

		public static bool IsBreakEdgeLabeled(Statement source, Statement closure)
		{
			if (closure.type == Statement.Type_Do || closure.type == Statement.Type_Switch)
			{
				Statement parent = source.GetParent();
				return parent != closure && (parent.type == Statement.Type_Do || parent.type == Statement
					.Type_Switch || IsBreakEdgeLabeled(parent, closure));
			}
			else
			{
				return true;
			}
		}
	}
}
