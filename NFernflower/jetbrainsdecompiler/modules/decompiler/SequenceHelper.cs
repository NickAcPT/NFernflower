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
	public class SequenceHelper
	{
		public static void CondenseSequences(Statement root)
		{
			CondenseSequencesRec(root);
		}

		private static void CondenseSequencesRec(Statement stat)
		{
			if (stat.type == Statement.Type_Sequence)
			{
				List<Statement> lst = new List<Statement>(stat.GetStats());
				bool unfolded = false;
				// unfold blocks
				for (int i = 0; i < lst.Count; i++)
				{
					Statement st = lst[i];
					if (st.type == Statement.Type_Sequence)
					{
						RemoveEmptyStatements((SequenceStatement)st);
						if (i == lst.Count - 1 || IsSequenceDisbandable(st, lst[i + 1]))
						{
							// move predecessors
							Statement first = st.GetFirst();
							foreach (StatEdge edge in st.GetAllPredecessorEdges())
							{
								st.RemovePredecessor(edge);
								edge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, edge, first);
								first.AddPredecessor(edge);
							}
							// move successors
							Statement last = st.GetStats().GetLast();
							if ((last.GetAllSuccessorEdges().Count == 0) && i < lst.Count - 1)
							{
								last.AddSuccessor(new StatEdge(StatEdge.Type_Regular, last, lst[i + 1]));
							}
							else
							{
								foreach (StatEdge edge in last.GetAllSuccessorEdges())
								{
									if (i == lst.Count - 1)
									{
										if (edge.closure == st)
										{
											stat.AddLabeledEdge(edge);
										}
									}
									else
									{
										edge.GetSource().ChangeEdgeType(Statement.Direction_Forward, edge, StatEdge.Type_Regular
											);
										edge.closure.GetLabelEdges().Remove(edge);
										edge.closure = null;
									}
								}
							}
							foreach (StatEdge edge in st.GetAllSuccessorEdges())
							{
								st.RemoveSuccessor(edge);
							}
							foreach (StatEdge edge in new HashSet<StatEdge>(st.GetLabelEdges()))
							{
								if (edge.GetSource() != last)
								{
									last.AddLabeledEdge(edge);
								}
							}
							lst.RemoveAtReturningValue(i);
							lst.AddAll(i, st.GetStats());
							i--;
							unfolded = true;
						}
					}
				}
				if (unfolded)
				{
					SequenceStatement sequence = new SequenceStatement(lst);
					sequence.SetAllParent();
					stat.GetParent().ReplaceStatement(stat, sequence);
					stat = sequence;
				}
			}
			// sequence consisting of one statement -> disband
			if (stat.type == Statement.Type_Sequence)
			{
				RemoveEmptyStatements((SequenceStatement)stat);
				if (stat.GetStats().Count == 1)
				{
					Statement st = stat.GetFirst();
					bool ok = (st.GetAllSuccessorEdges().Count == 0);
					if (!ok)
					{
						StatEdge edge = st.GetAllSuccessorEdges()[0];
						ok = (stat.GetAllSuccessorEdges().Count == 0);
						if (!ok)
						{
							StatEdge statedge = stat.GetAllSuccessorEdges()[0];
							ok = (edge.GetDestination() == statedge.GetDestination());
							if (ok)
							{
								st.RemoveSuccessor(edge);
							}
						}
					}
					if (ok)
					{
						stat.GetParent().ReplaceStatement(stat, st);
						stat = st;
					}
				}
			}
			// replace flat statements with synthetic basic blocks
			while (true)
			{
				foreach (Statement st in stat.GetStats())
				{
					if (((st.GetStats().Count == 0) || st.GetExprents() != null) && st.type != Statement
						.Type_Basicblock)
					{
						DestroyAndFlattenStatement(st);
						goto outer_continue;
					}
				}
				break;
outer_continue: ;
			}
outer_break: ;
			// recursion
			for (int i = 0; i < stat.GetStats().Count; i++)
			{
				CondenseSequencesRec(stat.GetStats()[i]);
			}
		}

		private static bool IsSequenceDisbandable(Statement block, Statement next)
		{
			Statement last = block.GetStats().GetLast();
			List<StatEdge> lstSuccs = last.GetAllSuccessorEdges();
			if (!(lstSuccs.Count == 0))
			{
				if (lstSuccs[0].GetDestination() != next)
				{
					return false;
				}
			}
			foreach (StatEdge edge in next.GetPredecessorEdges(StatEdge.Type_Break))
			{
				if (last != edge.GetSource() && !last.ContainsStatementStrict(edge.GetSource()))
				{
					return false;
				}
			}
			return true;
		}

		private static void RemoveEmptyStatements(SequenceStatement sequence)
		{
			if (sequence.GetStats().Count <= 1)
			{
				return;
			}
			MergeFlatStatements(sequence);
			while (true)
			{
				bool found = false;
				foreach (Statement st in sequence.GetStats())
				{
					if (st.GetExprents() != null && (st.GetExprents().Count == 0))
					{
						if ((st.GetAllSuccessorEdges().Count == 0))
						{
							List<StatEdge> lstBreaks = st.GetPredecessorEdges(StatEdge.Type_Break);
							if ((lstBreaks.Count == 0))
							{
								foreach (StatEdge edge in st.GetAllPredecessorEdges())
								{
									edge.GetSource().RemoveSuccessor(edge);
								}
								found = true;
							}
						}
						else
						{
							StatEdge sucedge = st.GetAllSuccessorEdges()[0];
							if (sucedge.GetType() != StatEdge.Type_Finallyexit)
							{
								st.RemoveSuccessor(sucedge);
								foreach (StatEdge edge in st.GetAllPredecessorEdges())
								{
									if (sucedge.GetType() != StatEdge.Type_Regular)
									{
										edge.GetSource().ChangeEdgeType(Statement.Direction_Forward, edge, sucedge.GetType
											());
									}
									st.RemovePredecessor(edge);
									edge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, edge, sucedge.GetDestination
										());
									sucedge.GetDestination().AddPredecessor(edge);
									if (sucedge.closure != null)
									{
										sucedge.closure.AddLabeledEdge(edge);
									}
								}
								found = true;
							}
						}
						if (found)
						{
							sequence.GetStats().RemoveWithKey(st.id);
							break;
						}
					}
				}
				if (!found)
				{
					break;
				}
			}
			sequence.SetFirst(sequence.GetStats()[0]);
		}

		private static void MergeFlatStatements(SequenceStatement sequence)
		{
			while (true)
			{
				Statement next;
				Statement current = null;
				bool found = false;
				for (int i = sequence.GetStats().Count - 1; i >= 0; i--)
				{
					next = current;
					current = sequence.GetStats()[i];
					if (next != null && current.GetExprents() != null && !(current.GetExprents().Count == 0))
					{
						if (next.GetExprents() != null)
						{
							next.GetExprents().AddAll(0, current.GetExprents());
							current.GetExprents().Clear();
							found = true;
						}
						else
						{
							Statement first = GetFirstExprentlist(next);
							if (first != null)
							{
								first.GetExprents().AddAll(0, current.GetExprents());
								current.GetExprents().Clear();
								found = true;
							}
						}
					}
				}
				if (!found)
				{
					break;
				}
			}
		}

		private static Statement GetFirstExprentlist(Statement stat)
		{
			if (stat.GetExprents() != null)
			{
				return stat;
			}
			switch (stat.type)
			{
				case Statement.Type_If:
				case Statement.Type_Sequence:
				case Statement.Type_Switch:
				case Statement.Type_Syncronized:
				{
					return GetFirstExprentlist(stat.GetFirst());
				}
			}
			return null;
		}

		public static void DestroyAndFlattenStatement(Statement stat)
		{
			DestroyStatementContent(stat, false);
			BasicBlockStatement bstat = new BasicBlockStatement(new BasicBlock(DecompilerContext
				.GetCounterContainer().GetCounterAndIncrement(CounterContainer.Statement_Counter
				)));
			if (stat.GetExprents() == null)
			{
				bstat.SetExprents(new List<Exprent>());
			}
			else
			{
				bstat.SetExprents(DecHelper.CopyExprentList(stat.GetExprents()));
			}
			stat.GetParent().ReplaceStatement(stat, bstat);
		}

		public static void DestroyStatementContent(Statement stat, bool self)
		{
			foreach (Statement st in stat.GetStats())
			{
				DestroyStatementContent(st, true);
			}
			stat.GetStats().Clear();
			if (self)
			{
				foreach (StatEdge edge in stat.GetAllSuccessorEdges())
				{
					stat.RemoveSuccessor(edge);
				}
			}
		}
	}
}
