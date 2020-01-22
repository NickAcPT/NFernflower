// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code.Cfg;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class ExitHelper
	{
		public static bool CondenseExits(RootStatement root)
		{
			int changed = IntegrateExits(root);
			if (changed > 0)
			{
				CleanUpUnreachableBlocks(root);
				SequenceHelper.CondenseSequences(root);
			}
			return (changed > 0);
		}

		private static void CleanUpUnreachableBlocks(Statement stat)
		{
			bool found;
			do
			{
				found = false;
				for (int i = 0; i < stat.GetStats().Count; i++)
				{
					Statement st = stat.GetStats()[i];
					CleanUpUnreachableBlocks(st);
					if (st.type == Statement.Type_Sequence && st.GetStats().Count > 1)
					{
						Statement last = st.GetStats().GetLast();
						Statement secondlast = st.GetStats()[st.GetStats().Count - 2];
						if (last.GetExprents() == null || !(last.GetExprents().Count == 0))
						{
							if (!secondlast.HasBasicSuccEdge())
							{
								HashSet<Statement> set = last.GetNeighboursSet(Statement.Statedge_Direct_All, Statement
									.Direction_Backward);
								set.Remove(secondlast);
								if ((set.Count == 0))
								{
									last.SetExprents(new List<Exprent>());
									found = true;
									break;
								}
							}
						}
					}
				}
			}
			while (found);
		}

		private static int IntegrateExits(Statement stat)
		{
			int ret = 0;
			Statement dest;
			if (stat.GetExprents() == null)
			{
				while (true)
				{
					int changed = 0;
					foreach (Statement st in stat.GetStats())
					{
						changed = IntegrateExits(st);
						if (changed > 0)
						{
							ret = 1;
							break;
						}
					}
					if (changed == 0)
					{
						break;
					}
				}
				if (stat.type == Statement.Type_If)
				{
					IfStatement ifst = (IfStatement)stat;
					if (ifst.GetIfstat() == null)
					{
						StatEdge ifedge = ifst.GetIfEdge();
						dest = IsExitEdge(ifedge);
						if (dest != null)
						{
							BasicBlockStatement bstat = new BasicBlockStatement(new BasicBlock(DecompilerContext
								.GetCounterContainer().GetCounterAndIncrement(CounterContainer.Statement_Counter
								)));
							bstat.SetExprents(DecHelper.CopyExprentList(dest.GetExprents()));
							ifst.GetFirst().RemoveSuccessor(ifedge);
							StatEdge newedge = new StatEdge(StatEdge.Type_Regular, ifst.GetFirst(), bstat);
							ifst.GetFirst().AddSuccessor(newedge);
							ifst.SetIfEdge(newedge);
							ifst.SetIfstat(bstat);
							ifst.GetStats().AddWithKey(bstat, bstat.id);
							bstat.SetParent(ifst);
							StatEdge oldexitedge = dest.GetAllSuccessorEdges()[0];
							StatEdge newexitedge = new StatEdge(StatEdge.Type_Break, bstat, oldexitedge.GetDestination
								());
							bstat.AddSuccessor(newexitedge);
							oldexitedge.closure.AddLabeledEdge(newexitedge);
							ret = 1;
						}
					}
				}
			}
			if (stat.GetAllSuccessorEdges().Count == 1 && stat.GetAllSuccessorEdges()[0].GetType
				() == StatEdge.Type_Break && (stat.GetLabelEdges().Count == 0))
			{
				Statement parent = stat.GetParent();
				if (stat != parent.GetFirst() || (parent.type != Statement.Type_If && parent.type
					 != Statement.Type_Switch))
				{
					StatEdge destedge = stat.GetAllSuccessorEdges()[0];
					dest = IsExitEdge(destedge);
					if (dest != null)
					{
						stat.RemoveSuccessor(destedge);
						BasicBlockStatement bstat = new BasicBlockStatement(new BasicBlock(DecompilerContext
							.GetCounterContainer().GetCounterAndIncrement(CounterContainer.Statement_Counter
							)));
						bstat.SetExprents(DecHelper.CopyExprentList(dest.GetExprents()));
						StatEdge oldexitedge = dest.GetAllSuccessorEdges()[0];
						StatEdge newexitedge = new StatEdge(StatEdge.Type_Break, bstat, oldexitedge.GetDestination
							());
						bstat.AddSuccessor(newexitedge);
						oldexitedge.closure.AddLabeledEdge(newexitedge);
						SequenceStatement block = new SequenceStatement(Sharpen.Arrays.AsList(stat, bstat
							));
						block.SetAllParent();
						parent.ReplaceStatement(stat, block);
						// LabelHelper.lowContinueLabels not applicable because of forward continue edges
						// LabelHelper.lowContinueLabels(block, new HashSet<StatEdge>());
						// do it by hand
						foreach (StatEdge prededge in block.GetPredecessorEdges(StatEdge.Type_Continue))
						{
							block.RemovePredecessor(prededge);
							prededge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, prededge, stat);
							stat.AddPredecessor(prededge);
							stat.AddLabeledEdge(prededge);
						}
						stat.AddSuccessor(new StatEdge(StatEdge.Type_Regular, stat, bstat));
						foreach (StatEdge edge in dest.GetAllPredecessorEdges())
						{
							if (!edge.@explicit && stat.ContainsStatementStrict(edge.GetSource()) && MergeHelper
								.IsDirectPath(edge.GetSource().GetParent(), bstat))
							{
								dest.RemovePredecessor(edge);
								edge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, edge, bstat);
								bstat.AddPredecessor(edge);
								if (!stat.ContainsStatementStrict(edge.closure))
								{
									stat.AddLabeledEdge(edge);
								}
							}
						}
						ret = 2;
					}
				}
			}
			return ret;
		}

		private static Statement IsExitEdge(StatEdge edge)
		{
			Statement dest = edge.GetDestination();
			if (edge.GetType() == StatEdge.Type_Break && dest.type == Statement.Type_Basicblock
				 && edge.@explicit && (edge.labeled || IsOnlyEdge(edge)))
			{
				List<Exprent> data = dest.GetExprents();
				if (data != null && data.Count == 1)
				{
					if (data[0].type == Exprent.Exprent_Exit)
					{
						return dest;
					}
				}
			}
			return null;
		}

		private static bool IsOnlyEdge(StatEdge edge)
		{
			Statement stat = edge.GetDestination();
			foreach (StatEdge ed in stat.GetAllPredecessorEdges())
			{
				if (ed != edge)
				{
					if (ed.GetType() == StatEdge.Type_Regular)
					{
						Statement source = ed.GetSource();
						if (source.type == Statement.Type_Basicblock || (source.type == Statement.Type_If
							 && ((IfStatement)source).iftype == IfStatement.Iftype_If) || (source.type == Statement
							.Type_Do && ((DoStatement)source).GetLooptype() != DoStatement.Loop_Do))
						{
							return false;
						}
					}
					else
					{
						return false;
					}
				}
			}
			return true;
		}

		public static void RemoveRedundantReturns(RootStatement root)
		{
			DummyExitStatement dummyExit = root.GetDummyExit();
			foreach (StatEdge edge in dummyExit.GetAllPredecessorEdges())
			{
				if (!edge.@explicit)
				{
					Statement source = edge.GetSource();
					List<Exprent> lstExpr = source.GetExprents();
					if (lstExpr != null && !(lstExpr.Count == 0))
					{
						Exprent expr = lstExpr[lstExpr.Count - 1];
						if (expr.type == Exprent.Exprent_Exit)
						{
							ExitExprent ex = (ExitExprent)expr;
							if (ex.GetExitType() == ExitExprent.Exit_Return && ex.GetValue() == null)
							{
								// remove redundant return
								dummyExit.AddBytecodeOffsets(ex.bytecode);
								lstExpr.RemoveAtReturningValue(lstExpr.Count - 1);
							}
						}
					}
				}
			}
		}
	}
}
