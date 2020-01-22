// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class LoopExtractHelper
	{
		public static bool ExtractLoops(Statement root)
		{
			bool res = (ExtractLoopsRec(root) != 0);
			if (res)
			{
				SequenceHelper.CondenseSequences(root);
			}
			return res;
		}

		private static int ExtractLoopsRec(Statement stat)
		{
			bool res = false;
			while (true)
			{
				bool updated = false;
				foreach (Statement st in stat.GetStats())
				{
					int extr = ExtractLoopsRec(st);
					res |= (extr != 0);
					if (extr == 2)
					{
						updated = true;
						break;
					}
				}
				if (!updated)
				{
					break;
				}
			}
			if (stat.type == Statement.Type_Do)
			{
				if (ExtractLoop((DoStatement)stat))
				{
					return 2;
				}
			}
			return res ? 1 : 0;
		}

		private static bool ExtractLoop(DoStatement stat)
		{
			if (stat.GetLooptype() != DoStatement.Loop_Do)
			{
				return false;
			}
			foreach (StatEdge edge in stat.GetLabelEdges())
			{
				if (edge.GetType() != StatEdge.Type_Continue && edge.GetDestination().type != Statement
					.Type_Dummyexit)
				{
					return false;
				}
			}
			return ExtractLastIf(stat) || ExtractFirstIf(stat);
		}

		private static bool ExtractLastIf(DoStatement stat)
		{
			// search for an if condition at the end of the loop
			Statement last = stat.GetFirst();
			while (last.type == Statement.Type_Sequence)
			{
				last = last.GetStats().GetLast();
			}
			if (last.type == Statement.Type_If)
			{
				IfStatement lastif = (IfStatement)last;
				if (lastif.iftype == IfStatement.Iftype_If && lastif.GetIfstat() != null)
				{
					Statement ifstat = lastif.GetIfstat();
					StatEdge elseedge = lastif.GetAllSuccessorEdges()[0];
					if (elseedge.GetType() == StatEdge.Type_Continue && elseedge.closure == stat)
					{
						HashSet<Statement> set = stat.GetNeighboursSet(StatEdge.Type_Continue, Statement.
							Direction_Backward);
						set.Remove(last);
						if ((set.Count == 0))
						{
							// no direct continues in a do{}while loop
							if (IsExternStatement(stat, ifstat, ifstat))
							{
								ExtractIfBlock(stat, lastif);
								return true;
							}
						}
					}
				}
			}
			return false;
		}

		private static bool ExtractFirstIf(DoStatement stat)
		{
			// search for an if condition at the entrance of the loop
			Statement first = stat.GetFirst();
			while (first.type == Statement.Type_Sequence)
			{
				first = first.GetFirst();
			}
			// found an if statement
			if (first.type == Statement.Type_If)
			{
				IfStatement firstif = (IfStatement)first;
				if ((firstif.GetFirst().GetExprents().Count == 0))
				{
					if (firstif.iftype == IfStatement.Iftype_If && firstif.GetIfstat() != null)
					{
						Statement ifstat = firstif.GetIfstat();
						if (IsExternStatement(stat, ifstat, ifstat))
						{
							ExtractIfBlock(stat, firstif);
							return true;
						}
					}
				}
			}
			return false;
		}

		private static bool IsExternStatement(DoStatement loop, Statement block, Statement
			 stat)
		{
			foreach (StatEdge edge in stat.GetAllSuccessorEdges())
			{
				if (loop.ContainsStatement(edge.GetDestination()) && !block.ContainsStatement(edge
					.GetDestination()))
				{
					return false;
				}
			}
			foreach (Statement st in stat.GetStats())
			{
				if (!IsExternStatement(loop, block, st))
				{
					return false;
				}
			}
			return true;
		}

		private static void ExtractIfBlock(DoStatement loop, IfStatement ifstat)
		{
			Statement target = ifstat.GetIfstat();
			StatEdge ifedge = ifstat.GetIfEdge();
			ifstat.SetIfstat(null);
			ifedge.GetSource().ChangeEdgeType(Statement.Direction_Forward, ifedge, StatEdge.Type_Break
				);
			ifedge.closure = loop;
			ifstat.GetStats().RemoveWithKey(target.id);
			loop.AddLabeledEdge(ifedge);
			SequenceStatement block = new SequenceStatement(Sharpen.Arrays.AsList(loop, target
				));
			loop.GetParent().ReplaceStatement(loop, block);
			block.SetAllParent();
			loop.AddSuccessor(new StatEdge(StatEdge.Type_Regular, loop, target));
			foreach (StatEdge edge in new List<StatEdge>(block.GetLabelEdges()))
			{
				if (edge.GetType() == StatEdge.Type_Continue || edge == ifedge)
				{
					loop.AddLabeledEdge(edge);
				}
			}
			foreach (StatEdge edge in block.GetPredecessorEdges(StatEdge.Type_Continue))
			{
				if (loop.ContainsStatementStrict(edge.GetSource()))
				{
					block.RemovePredecessor(edge);
					edge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, edge, loop);
					loop.AddPredecessor(edge);
				}
			}
		}
	}
}
