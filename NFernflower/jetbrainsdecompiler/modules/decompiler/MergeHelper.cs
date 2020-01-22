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
	public class MergeHelper
	{
		public static void EnhanceLoops(Statement root)
		{
			while (EnhanceLoopsRec(root))
			{
			}
			/**/
			SequenceHelper.CondenseSequences(root);
		}

		private static bool EnhanceLoopsRec(Statement stat)
		{
			bool res = false;
			foreach (Statement st in stat.GetStats())
			{
				if (st.GetExprents() == null)
				{
					res |= EnhanceLoopsRec(st);
				}
			}
			if (stat.type == Statement.Type_Do)
			{
				res |= EnhanceLoop((DoStatement)stat);
			}
			return res;
		}

		private static bool EnhanceLoop(DoStatement stat)
		{
			int oldloop = stat.GetLooptype();
			switch (oldloop)
			{
				case DoStatement.Loop_Do:
				{
					// identify a while loop
					if (MatchWhile(stat))
					{
						// identify a for loop - subtype of while
						MatchFor(stat);
					}
					else
					{
						// identify a do{}while loop
						MatchDoWhile(stat);
					}
					break;
				}

				case DoStatement.Loop_While:
				{
					MatchFor(stat);
					break;
				}
			}
			return (stat.GetLooptype() != oldloop);
		}

		private static void MatchDoWhile(DoStatement stat)
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
				if (lastif.iftype == IfStatement.Iftype_If && lastif.GetIfstat() == null)
				{
					StatEdge ifedge = lastif.GetIfEdge();
					StatEdge elseedge = lastif.GetAllSuccessorEdges()[0];
					if ((ifedge.GetType() == StatEdge.Type_Break && elseedge.GetType() == StatEdge.Type_Continue
						 && elseedge.closure == stat && IsDirectPath(stat, ifedge.GetDestination())) || 
						(ifedge.GetType() == StatEdge.Type_Continue && elseedge.GetType() == StatEdge.Type_Break
						 && ifedge.closure == stat && IsDirectPath(stat, elseedge.GetDestination())))
					{
						HashSet<Statement> set = stat.GetNeighboursSet(StatEdge.Type_Continue, Statement.
							Direction_Backward);
						set.Remove(last);
						if (!(set.Count == 0))
						{
							return;
						}
						stat.SetLooptype(DoStatement.Loop_Dowhile);
						IfExprent ifexpr = (IfExprent)lastif.GetHeadexprent().Copy();
						if (ifedge.GetType() == StatEdge.Type_Break)
						{
							ifexpr.NegateIf();
						}
						stat.SetConditionExprent(ifexpr.GetCondition());
						lastif.GetFirst().RemoveSuccessor(ifedge);
						lastif.RemoveSuccessor(elseedge);
						// remove empty if
						if ((lastif.GetFirst().GetExprents().Count == 0))
						{
							RemoveLastEmptyStatement(stat, lastif);
						}
						else
						{
							lastif.SetExprents(lastif.GetFirst().GetExprents());
							StatEdge newedge = new StatEdge(StatEdge.Type_Continue, lastif, stat);
							lastif.AddSuccessor(newedge);
							stat.AddLabeledEdge(newedge);
						}
						if ((stat.GetAllSuccessorEdges().Count == 0))
						{
							StatEdge edge = elseedge.GetType() == StatEdge.Type_Continue ? ifedge : elseedge;
							edge.SetSource(stat);
							if (edge.closure == stat)
							{
								edge.closure = stat.GetParent();
							}
							stat.AddSuccessor(edge);
						}
					}
				}
			}
		}

		private static bool MatchWhile(DoStatement stat)
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
					if (firstif.iftype == IfStatement.Iftype_If)
					{
						if (firstif.GetIfstat() == null)
						{
							StatEdge ifedge = firstif.GetIfEdge();
							if (IsDirectPath(stat, ifedge.GetDestination()))
							{
								// exit condition identified
								stat.SetLooptype(DoStatement.Loop_While);
								// negate condition (while header)
								IfExprent ifexpr = (IfExprent)firstif.GetHeadexprent().Copy();
								ifexpr.NegateIf();
								stat.SetConditionExprent(ifexpr.GetCondition());
								// remove edges
								firstif.GetFirst().RemoveSuccessor(ifedge);
								firstif.RemoveSuccessor(firstif.GetAllSuccessorEdges()[0]);
								if ((stat.GetAllSuccessorEdges().Count == 0))
								{
									ifedge.SetSource(stat);
									if (ifedge.closure == stat)
									{
										ifedge.closure = stat.GetParent();
									}
									stat.AddSuccessor(ifedge);
								}
								// remove empty if statement as it is now part of the loop
								if (firstif == stat.GetFirst())
								{
									BasicBlockStatement bstat = new BasicBlockStatement(new BasicBlock(DecompilerContext
										.GetCounterContainer().GetCounterAndIncrement(CounterContainer.Statement_Counter
										)));
									bstat.SetExprents(new List<Exprent>());
									stat.ReplaceStatement(firstif, bstat);
								}
								else
								{
									// precondition: sequence must contain more than one statement!
									Statement sequence = firstif.GetParent();
									sequence.GetStats().RemoveWithKey(firstif.id);
									sequence.SetFirst(sequence.GetStats()[0]);
								}
								return true;
							}
						}
						else
						{
							StatEdge elseedge = firstif.GetAllSuccessorEdges()[0];
							if (IsDirectPath(stat, elseedge.GetDestination()))
							{
								// exit condition identified
								stat.SetLooptype(DoStatement.Loop_While);
								// no need to negate the while condition
								stat.SetConditionExprent(((IfExprent)firstif.GetHeadexprent().Copy()).GetCondition
									());
								// remove edges
								StatEdge ifedge = firstif.GetIfEdge();
								firstif.GetFirst().RemoveSuccessor(ifedge);
								firstif.RemoveSuccessor(elseedge);
								if ((stat.GetAllSuccessorEdges().Count == 0))
								{
									elseedge.SetSource(stat);
									if (elseedge.closure == stat)
									{
										elseedge.closure = stat.GetParent();
									}
									stat.AddSuccessor(elseedge);
								}
								if (firstif.GetIfstat() == null)
								{
									BasicBlockStatement bstat = new BasicBlockStatement(new BasicBlock(DecompilerContext
										.GetCounterContainer().GetCounterAndIncrement(CounterContainer.Statement_Counter
										)));
									bstat.SetExprents(new List<Exprent>());
									ifedge.SetSource(bstat);
									bstat.AddSuccessor(ifedge);
									stat.ReplaceStatement(firstif, bstat);
								}
								else
								{
									// replace the if statement with its content
									first.GetParent().ReplaceStatement(first, firstif.GetIfstat());
									// lift closures
									foreach (StatEdge prededge in elseedge.GetDestination().GetPredecessorEdges(StatEdge
										.Type_Break))
									{
										if (stat.ContainsStatementStrict(prededge.closure))
										{
											stat.AddLabeledEdge(prededge);
										}
									}
									LabelHelper.LowClosures(stat);
								}
								return true;
							}
						}
					}
				}
			}
			return false;
		}

		public static bool IsDirectPath(Statement stat, Statement endstat)
		{
			HashSet<Statement> setStat = stat.GetNeighboursSet(Statement.Statedge_Direct_All, 
				Statement.Direction_Forward);
			if ((setStat.Count == 0))
			{
				Statement parent = stat.GetParent();
				if (parent == null)
				{
					return false;
				}
				else
				{
					switch (parent.type)
					{
						case Statement.Type_Root:
						{
							return endstat.type == Statement.Type_Dummyexit;
						}

						case Statement.Type_Do:
						{
							return (endstat == parent);
						}

						case Statement.Type_Switch:
						{
							SwitchStatement swst = (SwitchStatement)parent;
							for (int i = 0; i < swst.GetCaseStatements().Count - 1; i++)
							{
								Statement stt = swst.GetCaseStatements()[i];
								if (stt == stat)
								{
									Statement stnext = swst.GetCaseStatements()[i + 1];
									if (stnext.GetExprents() != null && (stnext.GetExprents().Count == 0))
									{
										stnext = stnext.GetAllSuccessorEdges()[0].GetDestination();
									}
									return (endstat == stnext);
								}
							}
							goto default;
						}

						default:
						{
							return IsDirectPath(parent, endstat);
						}
					}
				}
			}
			else
			{
				return setStat.Contains(endstat);
			}
		}

		private static void MatchFor(DoStatement stat)
		{
			Exprent lastDoExprent;
			Exprent initDoExprent;
			Statement lastData;
			Statement preData = null;
			// get last exprent
			lastData = GetLastDirectData(stat.GetFirst());
			if (lastData == null || (lastData.GetExprents().Count == 0))
			{
				return;
			}
			List<Exprent> lstExpr = lastData.GetExprents();
			lastDoExprent = lstExpr[lstExpr.Count - 1];
			bool issingle = false;
			if (lstExpr.Count == 1)
			{
				// single exprent
				if (lastData.GetAllPredecessorEdges().Count > 1)
				{
					// break edges
					issingle = true;
				}
			}
			bool haslast = issingle || lastDoExprent.type == Exprent.Exprent_Assignment || lastDoExprent
				.type == Exprent.Exprent_Function;
			if (!haslast)
			{
				return;
			}
			bool hasinit = false;
			// search for an initializing exprent
			Statement current = stat;
			while (true)
			{
				Statement parent = current.GetParent();
				if (parent == null)
				{
					break;
				}
				if (parent.type == Statement.Type_Sequence)
				{
					if (current == parent.GetFirst())
					{
						current = parent;
					}
					else
					{
						preData = current.GetNeighbours(StatEdge.Type_Regular, Statement.Direction_Backward
							)[0];
						preData = GetLastDirectData(preData);
						if (preData != null && !(preData.GetExprents().Count == 0))
						{
							initDoExprent = preData.GetExprents()[preData.GetExprents().Count - 1];
							if (initDoExprent.type == Exprent.Exprent_Assignment)
							{
								hasinit = true;
							}
						}
						break;
					}
				}
				else
				{
					break;
				}
			}
			if (hasinit || issingle)
			{
				// FIXME: issingle sufficient?
				HashSet<Statement> set = stat.GetNeighboursSet(StatEdge.Type_Continue, Statement.
					Direction_Backward);
				set.Remove(lastData);
				if (!(set.Count == 0))
				{
					return;
				}
				stat.SetLooptype(DoStatement.Loop_For);
				if (hasinit)
				{
					stat.SetInitExprent(preData.GetExprents().RemoveAtReturningValue(preData.GetExprents
						().Count - 1));
				}
				stat.SetIncExprent(lastData.GetExprents().RemoveAtReturningValue(lastData.GetExprents
					().Count - 1));
			}
			if ((lastData.GetExprents().Count == 0))
			{
				List<StatEdge> lst = lastData.GetAllSuccessorEdges();
				if (!(lst.Count == 0))
				{
					lastData.RemoveSuccessor(lst[0]);
				}
				RemoveLastEmptyStatement(stat, lastData);
			}
		}

		private static void RemoveLastEmptyStatement(DoStatement dostat, Statement stat)
		{
			if (stat == dostat.GetFirst())
			{
				BasicBlockStatement bstat = new BasicBlockStatement(new BasicBlock(DecompilerContext
					.GetCounterContainer().GetCounterAndIncrement(CounterContainer.Statement_Counter
					)));
				bstat.SetExprents(new List<Exprent>());
				dostat.ReplaceStatement(stat, bstat);
			}
			else
			{
				foreach (StatEdge edge in stat.GetAllPredecessorEdges())
				{
					edge.GetSource().ChangeEdgeType(Statement.Direction_Forward, edge, StatEdge.Type_Continue
						);
					stat.RemovePredecessor(edge);
					edge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, edge, dostat);
					dostat.AddPredecessor(edge);
					dostat.AddLabeledEdge(edge);
				}
				// parent is a sequence statement
				stat.GetParent().GetStats().RemoveWithKey(stat.id);
			}
		}

		private static Statement GetLastDirectData(Statement stat)
		{
			if (stat.GetExprents() != null)
			{
				return stat;
			}
			if (stat.type == Statement.Type_Sequence)
			{
				for (int i = stat.GetStats().Count - 1; i >= 0; i--)
				{
					Statement tmp = GetLastDirectData(stat.GetStats()[i]);
					if (tmp == null || !(tmp.GetExprents().Count == 0))
					{
						return tmp;
					}
				}
			}
			return null;
		}
	}
}
