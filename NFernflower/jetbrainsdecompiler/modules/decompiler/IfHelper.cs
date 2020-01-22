// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class IfHelper
	{
		public static bool MergeAllIfs(RootStatement root)
		{
			bool res = MergeAllIfsRec(root, new HashSet<int>());
			if (res)
			{
				SequenceHelper.CondenseSequences(root);
			}
			return res;
		}

		private static bool MergeAllIfsRec<_T0>(Statement stat, HashSet<_T0> setReorderedIfs
			)
		{
			bool res = false;
			if (stat.GetExprents() == null)
			{
				while (true)
				{
					bool changed = false;
					foreach (Statement st in stat.GetStats())
					{
						res |= MergeAllIfsRec(st, setReorderedIfs);
						// collapse composed if's
						if (changed = MergeIfs(st, setReorderedIfs))
						{
							break;
						}
					}
					res |= changed;
					if (!changed)
					{
						break;
					}
				}
			}
			return res;
		}

		public static bool MergeIfs<_T0>(Statement statement, HashSet<_T0> setReorderedIfs
			)
		{
			if (statement.type != Statement.Type_If && statement.type != Statement.Type_Sequence)
			{
				return false;
			}
			bool res = false;
			while (true)
			{
				bool updated = false;
				List<Statement> lst = new List<Statement>();
				if (statement.type == Statement.Type_If)
				{
					lst.Add(statement);
				}
				else
				{
					Sharpen.Collections.AddAll(lst, statement.GetStats());
				}
				bool stsingle = (lst.Count == 1);
				foreach (Statement stat in lst)
				{
					if (stat.type == Statement.Type_If)
					{
						IfHelper.IfNode rtnode = BuildGraph((IfStatement)stat, stsingle);
						if (rtnode == null)
						{
							continue;
						}
						if (updated = CollapseIfIf(rtnode))
						{
							break;
						}
						if (!setReorderedIfs.Contains(stat.id))
						{
							if (updated = CollapseIfElse(rtnode))
							{
								break;
							}
							if (updated = CollapseElse(rtnode))
							{
								break;
							}
						}
						if (updated = ReorderIf((IfStatement)stat))
						{
							setReorderedIfs.Add(stat.id);
							break;
						}
					}
				}
				if (!updated)
				{
					break;
				}
				res |= true;
			}
			return res;
		}

		private static bool CollapseIfIf(IfHelper.IfNode rtnode)
		{
			if (rtnode.edgetypes[0] == 0)
			{
				IfHelper.IfNode ifbranch = rtnode.succs[0];
				if (ifbranch.succs.Count == 2)
				{
					// if-if branch
					if (ifbranch.succs[1].value == rtnode.succs[1].value)
					{
						IfStatement ifparent = (IfStatement)rtnode.value;
						IfStatement ifchild = (IfStatement)ifbranch.value;
						Statement ifinner = ifbranch.succs[0].value;
						if ((ifchild.GetFirst().GetExprents().Count == 0))
						{
							ifparent.GetFirst().RemoveSuccessor(ifparent.GetIfEdge());
							ifchild.RemoveSuccessor(ifchild.GetAllSuccessorEdges()[0]);
							ifparent.GetStats().RemoveWithKey(ifchild.id);
							if (ifbranch.edgetypes[0] == 1)
							{
								// target null
								ifparent.SetIfstat(null);
								StatEdge ifedge = ifchild.GetIfEdge();
								ifchild.GetFirst().RemoveSuccessor(ifedge);
								ifedge.SetSource(ifparent.GetFirst());
								if (ifedge.closure == ifchild)
								{
									ifedge.closure = null;
								}
								ifparent.GetFirst().AddSuccessor(ifedge);
								ifparent.SetIfEdge(ifedge);
							}
							else
							{
								ifchild.GetFirst().RemoveSuccessor(ifchild.GetIfEdge());
								StatEdge ifedge = new StatEdge(StatEdge.Type_Regular, ifparent.GetFirst(), ifinner
									);
								ifparent.GetFirst().AddSuccessor(ifedge);
								ifparent.SetIfEdge(ifedge);
								ifparent.SetIfstat(ifinner);
								ifparent.GetStats().AddWithKey(ifinner, ifinner.id);
								ifinner.SetParent(ifparent);
								if (!(ifinner.GetAllSuccessorEdges().Count == 0))
								{
									StatEdge edge = ifinner.GetAllSuccessorEdges()[0];
									if (edge.closure == ifchild)
									{
										edge.closure = null;
									}
								}
							}
							// merge if conditions
							IfExprent statexpr = ifparent.GetHeadexprent();
							List<Exprent> lstOperands = new List<Exprent>();
							lstOperands.Add(statexpr.GetCondition());
							lstOperands.Add(ifchild.GetHeadexprent().GetCondition());
							statexpr.SetCondition(new FunctionExprent(FunctionExprent.Function_Cadd, lstOperands
								, null));
							statexpr.AddBytecodeOffsets(ifchild.GetHeadexprent().bytecode);
							return true;
						}
					}
				}
			}
			return false;
		}

		private static bool CollapseIfElse(IfHelper.IfNode rtnode)
		{
			if (rtnode.edgetypes[0] == 0)
			{
				IfHelper.IfNode ifbranch = rtnode.succs[0];
				if (ifbranch.succs.Count == 2)
				{
					// if-else branch
					if (ifbranch.succs[0].value == rtnode.succs[1].value)
					{
						IfStatement ifparent = (IfStatement)rtnode.value;
						IfStatement ifchild = (IfStatement)ifbranch.value;
						if ((ifchild.GetFirst().GetExprents().Count == 0))
						{
							ifparent.GetFirst().RemoveSuccessor(ifparent.GetIfEdge());
							ifchild.GetFirst().RemoveSuccessor(ifchild.GetIfEdge());
							ifparent.GetStats().RemoveWithKey(ifchild.id);
							if (ifbranch.edgetypes[1] == 1 && ifbranch.edgetypes[0] == 1)
							{
								// target null
								ifparent.SetIfstat(null);
								StatEdge ifedge = ifchild.GetAllSuccessorEdges()[0];
								ifchild.RemoveSuccessor(ifedge);
								ifedge.SetSource(ifparent.GetFirst());
								ifparent.GetFirst().AddSuccessor(ifedge);
								ifparent.SetIfEdge(ifedge);
							}
							else
							{
								throw new Exception("inconsistent if structure!");
							}
							// merge if conditions
							IfExprent statexpr = ifparent.GetHeadexprent();
							List<Exprent> lstOperands = new List<Exprent>();
							lstOperands.Add(statexpr.GetCondition());
							lstOperands.Add(new FunctionExprent(FunctionExprent.Function_Bool_Not, ifchild.GetHeadexprent
								().GetCondition(), null));
							statexpr.SetCondition(new FunctionExprent(FunctionExprent.Function_Cadd, lstOperands
								, null));
							statexpr.AddBytecodeOffsets(ifchild.GetHeadexprent().bytecode);
							return true;
						}
					}
				}
			}
			return false;
		}

		private static bool CollapseElse(IfHelper.IfNode rtnode)
		{
			if (rtnode.edgetypes[1] == 0)
			{
				IfHelper.IfNode elsebranch = rtnode.succs[1];
				if (elsebranch.succs.Count == 2)
				{
					// else-if or else-else branch
					int path = elsebranch.succs[1].value == rtnode.succs[0].value ? 2 : (elsebranch.succs
						[0].value == rtnode.succs[0].value ? 1 : 0);
					if (path > 0)
					{
						IfStatement firstif = (IfStatement)rtnode.value;
						IfStatement secondif = (IfStatement)elsebranch.value;
						Statement parent = firstif.GetParent();
						if ((secondif.GetFirst().GetExprents().Count == 0))
						{
							firstif.GetFirst().RemoveSuccessor(firstif.GetIfEdge());
							// remove first if
							firstif.RemoveAllSuccessors(secondif);
							foreach (StatEdge edge in firstif.GetAllPredecessorEdges())
							{
								if (!firstif.ContainsStatementStrict(edge.GetSource()))
								{
									firstif.RemovePredecessor(edge);
									edge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, edge, secondif);
									secondif.AddPredecessor(edge);
								}
							}
							parent.GetStats().RemoveWithKey(firstif.id);
							if (parent.GetFirst() == firstif)
							{
								parent.SetFirst(secondif);
							}
							// merge if conditions
							IfExprent statexpr = secondif.GetHeadexprent();
							List<Exprent> lstOperands = new List<Exprent>();
							lstOperands.Add(firstif.GetHeadexprent().GetCondition());
							if (path == 2)
							{
								lstOperands[0] = new FunctionExprent(FunctionExprent.Function_Bool_Not, lstOperands
									[0], null);
							}
							lstOperands.Add(statexpr.GetCondition());
							statexpr.SetCondition(new FunctionExprent(path == 1 ? FunctionExprent.Function_Cor
								 : FunctionExprent.Function_Cadd, lstOperands, null));
							if ((secondif.GetFirst().GetExprents().Count == 0) && !(firstif.GetFirst().GetExprents
								().Count == 0))
							{
								secondif.ReplaceStatement(secondif.GetFirst(), firstif.GetFirst());
							}
							return true;
						}
					}
				}
				else if (elsebranch.succs.Count == 1)
				{
					if (elsebranch.succs[0].value == rtnode.succs[0].value)
					{
						IfStatement firstif = (IfStatement)rtnode.value;
						Statement second = elsebranch.value;
						firstif.RemoveAllSuccessors(second);
						foreach (StatEdge edge in second.GetAllSuccessorEdges())
						{
							second.RemoveSuccessor(edge);
							edge.SetSource(firstif);
							firstif.AddSuccessor(edge);
						}
						StatEdge ifedge = firstif.GetIfEdge();
						firstif.GetFirst().RemoveSuccessor(ifedge);
						second.AddSuccessor(new StatEdge(ifedge.GetType(), second, ifedge.GetDestination(
							), ifedge.closure));
						StatEdge newifedge = new StatEdge(StatEdge.Type_Regular, firstif.GetFirst(), second
							);
						firstif.GetFirst().AddSuccessor(newifedge);
						firstif.SetIfstat(second);
						firstif.GetStats().AddWithKey(second, second.id);
						second.SetParent(firstif);
						firstif.GetParent().GetStats().RemoveWithKey(second.id);
						// negate the if condition
						IfExprent statexpr = firstif.GetHeadexprent();
						statexpr.SetCondition(new FunctionExprent(FunctionExprent.Function_Bool_Not, statexpr
							.GetCondition(), null));
						return true;
					}
				}
			}
			return false;
		}

		private static IfHelper.IfNode BuildGraph(IfStatement stat, bool stsingle)
		{
			if (stat.iftype == IfStatement.Iftype_Ifelse)
			{
				return null;
			}
			IfHelper.IfNode res = new IfHelper.IfNode(stat);
			// if branch
			Statement ifchild = stat.GetIfstat();
			if (ifchild == null)
			{
				StatEdge edge = stat.GetIfEdge();
				res.AddChild(new IfHelper.IfNode(edge.GetDestination()), 1);
			}
			else
			{
				IfHelper.IfNode ifnode = new IfHelper.IfNode(ifchild);
				res.AddChild(ifnode, 0);
				if (ifchild.type == Statement.Type_If && ((IfStatement)ifchild).iftype == IfStatement
					.Iftype_If)
				{
					IfStatement stat2 = (IfStatement)ifchild;
					Statement ifchild2 = stat2.GetIfstat();
					if (ifchild2 == null)
					{
						StatEdge edge = stat2.GetIfEdge();
						ifnode.AddChild(new IfHelper.IfNode(edge.GetDestination()), 1);
					}
					else
					{
						ifnode.AddChild(new IfHelper.IfNode(ifchild2), 0);
					}
				}
				if (!(ifchild.GetAllSuccessorEdges().Count == 0))
				{
					ifnode.AddChild(new IfHelper.IfNode(ifchild.GetAllSuccessorEdges()[0].GetDestination
						()), 1);
				}
			}
			// else branch
			StatEdge edge_1 = stat.GetAllSuccessorEdges()[0];
			Statement elsechild = edge_1.GetDestination();
			IfHelper.IfNode elsenode = new IfHelper.IfNode(elsechild);
			if (stsingle || edge_1.GetType() != StatEdge.Type_Regular)
			{
				res.AddChild(elsenode, 1);
			}
			else
			{
				res.AddChild(elsenode, 0);
				if (elsechild.type == Statement.Type_If && ((IfStatement)elsechild).iftype == IfStatement
					.Iftype_If)
				{
					IfStatement stat2 = (IfStatement)elsechild;
					Statement ifchild2 = stat2.GetIfstat();
					if (ifchild2 == null)
					{
						elsenode.AddChild(new IfHelper.IfNode(stat2.GetIfEdge().GetDestination()), 1);
					}
					else
					{
						elsenode.AddChild(new IfHelper.IfNode(ifchild2), 0);
					}
				}
				if (!(elsechild.GetAllSuccessorEdges().Count == 0))
				{
					elsenode.AddChild(new IfHelper.IfNode(elsechild.GetAllSuccessorEdges()[0].GetDestination
						()), 1);
				}
			}
			return res;
		}

		// FIXME: rewrite the entire method!!! keep in mind finally exits!!
		private static bool ReorderIf(IfStatement ifstat)
		{
			if (ifstat.iftype == IfStatement.Iftype_Ifelse)
			{
				return false;
			}
			bool ifdirect;
			bool elsedirect;
			bool noifstat = false;
			bool noelsestat;
			bool ifdirectpath = false;
			bool elsedirectpath = false;
			Statement parent = ifstat.GetParent();
			Statement from = parent.type == Statement.Type_Sequence ? parent : ifstat;
			Statement next = GetNextStatement(from);
			if (ifstat.GetIfstat() == null)
			{
				noifstat = true;
				ifdirect = ifstat.GetIfEdge().GetType() == StatEdge.Type_Finallyexit || MergeHelper
					.IsDirectPath(from, ifstat.GetIfEdge().GetDestination());
			}
			else
			{
				List<StatEdge> lstSuccs = ifstat.GetIfstat().GetAllSuccessorEdges();
				ifdirect = !(lstSuccs.Count == 0) && lstSuccs[0].GetType() == StatEdge.Type_Finallyexit
					 || HasDirectEndEdge(ifstat.GetIfstat(), from);
			}
			Statement last = parent.type == Statement.Type_Sequence ? parent.GetStats().GetLast
				() : ifstat;
			noelsestat = (last == ifstat);
			elsedirect = !(last.GetAllSuccessorEdges().Count == 0) && last.GetAllSuccessorEdges
				()[0].GetType() == StatEdge.Type_Finallyexit || HasDirectEndEdge(last, from);
			if (!noelsestat && ExistsPath(ifstat, ifstat.GetAllSuccessorEdges()[0].GetDestination
				()))
			{
				return false;
			}
			if (!ifdirect && !noifstat)
			{
				ifdirectpath = ExistsPath(ifstat, next);
			}
			if (!elsedirect && !noelsestat)
			{
				SequenceStatement sequence = (SequenceStatement)parent;
				for (int i = sequence.GetStats().Count - 1; i >= 0; i--)
				{
					Statement sttemp = sequence.GetStats()[i];
					if (sttemp == ifstat)
					{
						break;
					}
					else if (elsedirectpath = ExistsPath(sttemp, next))
					{
						break;
					}
				}
			}
			if ((ifdirect || ifdirectpath) && (elsedirect || elsedirectpath) && !noifstat && 
				!noelsestat)
			{
				// if - then - else
				SequenceStatement sequence = (SequenceStatement)parent;
				// build and cut the new else statement
				List<Statement> lst = new List<Statement>();
				for (int i = sequence.GetStats().Count - 1; i >= 0; i--)
				{
					Statement sttemp = sequence.GetStats()[i];
					if (sttemp == ifstat)
					{
						break;
					}
					else
					{
						lst.Add(0, sttemp);
					}
				}
				Statement stelse;
				if (lst.Count == 1)
				{
					stelse = lst[0];
				}
				else
				{
					stelse = new SequenceStatement(lst);
					stelse.SetAllParent();
				}
				ifstat.RemoveSuccessor(ifstat.GetAllSuccessorEdges()[0]);
				foreach (Statement st in lst)
				{
					sequence.GetStats().RemoveWithKey(st.id);
				}
				StatEdge elseedge = new StatEdge(StatEdge.Type_Regular, ifstat.GetFirst(), stelse
					);
				ifstat.GetFirst().AddSuccessor(elseedge);
				ifstat.SetElsestat(stelse);
				ifstat.SetElseEdge(elseedge);
				ifstat.GetStats().AddWithKey(stelse, stelse.id);
				stelse.SetParent(ifstat);
				//			if(next.type != Statement.TYPE_DUMMYEXIT && (ifdirect || elsedirect)) {
				//	 			StatEdge breakedge = new StatEdge(StatEdge.TYPE_BREAK, ifstat, next);
				//				sequence.addLabeledEdge(breakedge);
				//				ifstat.addSuccessor(breakedge);
				//			}
				ifstat.iftype = IfStatement.Iftype_Ifelse;
			}
			else if (ifdirect && (!elsedirect || (noifstat && !noelsestat)))
			{
				// if - then
				// negate the if condition
				IfExprent statexpr = ifstat.GetHeadexprent();
				statexpr.SetCondition(new FunctionExprent(FunctionExprent.Function_Bool_Not, statexpr
					.GetCondition(), null));
				if (noelsestat)
				{
					StatEdge ifedge = ifstat.GetIfEdge();
					StatEdge elseedge = ifstat.GetAllSuccessorEdges()[0];
					if (noifstat)
					{
						ifstat.GetFirst().RemoveSuccessor(ifedge);
						ifstat.RemoveSuccessor(elseedge);
						ifedge.SetSource(ifstat);
						elseedge.SetSource(ifstat.GetFirst());
						ifstat.AddSuccessor(ifedge);
						ifstat.GetFirst().AddSuccessor(elseedge);
						ifstat.SetIfEdge(elseedge);
					}
					else
					{
						Statement ifbranch = ifstat.GetIfstat();
						SequenceStatement newseq = new SequenceStatement(Sharpen.Arrays.AsList(ifstat, ifbranch
							));
						ifstat.GetFirst().RemoveSuccessor(ifedge);
						ifstat.GetStats().RemoveWithKey(ifbranch.id);
						ifstat.SetIfstat(null);
						ifstat.RemoveSuccessor(elseedge);
						elseedge.SetSource(ifstat.GetFirst());
						ifstat.GetFirst().AddSuccessor(elseedge);
						ifstat.SetIfEdge(elseedge);
						ifstat.GetParent().ReplaceStatement(ifstat, newseq);
						newseq.SetAllParent();
						ifstat.AddSuccessor(new StatEdge(StatEdge.Type_Regular, ifstat, ifbranch));
					}
				}
				else
				{
					SequenceStatement sequence = (SequenceStatement)parent;
					// build and cut the new else statement
					List<Statement> lst = new List<Statement>();
					for (int i = sequence.GetStats().Count - 1; i >= 0; i--)
					{
						Statement sttemp = sequence.GetStats()[i];
						if (sttemp == ifstat)
						{
							break;
						}
						else
						{
							lst.Add(0, sttemp);
						}
					}
					Statement stelse;
					if (lst.Count == 1)
					{
						stelse = lst[0];
					}
					else
					{
						stelse = new SequenceStatement(lst);
						stelse.SetAllParent();
					}
					ifstat.RemoveSuccessor(ifstat.GetAllSuccessorEdges()[0]);
					foreach (Statement st in lst)
					{
						sequence.GetStats().RemoveWithKey(st.id);
					}
					if (noifstat)
					{
						StatEdge ifedge = ifstat.GetIfEdge();
						ifstat.GetFirst().RemoveSuccessor(ifedge);
						ifedge.SetSource(ifstat);
						ifstat.AddSuccessor(ifedge);
					}
					else
					{
						Statement ifbranch = ifstat.GetIfstat();
						ifstat.GetFirst().RemoveSuccessor(ifstat.GetIfEdge());
						ifstat.GetStats().RemoveWithKey(ifbranch.id);
						ifstat.AddSuccessor(new StatEdge(StatEdge.Type_Regular, ifstat, ifbranch));
						sequence.GetStats().AddWithKey(ifbranch, ifbranch.id);
						ifbranch.SetParent(sequence);
					}
					StatEdge newifedge = new StatEdge(StatEdge.Type_Regular, ifstat.GetFirst(), stelse
						);
					ifstat.GetFirst().AddSuccessor(newifedge);
					ifstat.SetIfstat(stelse);
					ifstat.SetIfEdge(newifedge);
					ifstat.GetStats().AddWithKey(stelse, stelse.id);
					stelse.SetParent(ifstat);
				}
			}
			else
			{
				return false;
			}
			return true;
		}

		private static bool HasDirectEndEdge(Statement stat, Statement from)
		{
			foreach (StatEdge edge in stat.GetAllSuccessorEdges())
			{
				if (MergeHelper.IsDirectPath(from, edge.GetDestination()))
				{
					return true;
				}
			}
			if (stat.GetExprents() == null)
			{
				switch (stat.type)
				{
					case Statement.Type_Sequence:
					{
						return HasDirectEndEdge(stat.GetStats().GetLast(), from);
					}

					case Statement.Type_Catchall:
					case Statement.Type_Trycatch:
					{
						foreach (Statement st in stat.GetStats())
						{
							if (HasDirectEndEdge(st, from))
							{
								return true;
							}
						}
						break;
					}

					case Statement.Type_If:
					{
						IfStatement ifstat = (IfStatement)stat;
						if (ifstat.iftype == IfStatement.Iftype_Ifelse)
						{
							return HasDirectEndEdge(ifstat.GetIfstat(), from) || HasDirectEndEdge(ifstat.GetElsestat
								(), from);
						}
						break;
					}

					case Statement.Type_Syncronized:
					{
						return HasDirectEndEdge(stat.GetStats()[1], from);
					}

					case Statement.Type_Switch:
					{
						foreach (Statement st in stat.GetStats())
						{
							if (HasDirectEndEdge(st, from))
							{
								return true;
							}
						}
						break;
					}
				}
			}
			return false;
		}

		private static Statement GetNextStatement(Statement stat)
		{
			Statement parent = stat.GetParent();
			switch (parent.type)
			{
				case Statement.Type_Root:
				{
					return ((RootStatement)parent).GetDummyExit();
				}

				case Statement.Type_Do:
				{
					return parent;
				}

				case Statement.Type_Sequence:
				{
					SequenceStatement sequence = (SequenceStatement)parent;
					if (sequence.GetStats().GetLast() != stat)
					{
						for (int i = sequence.GetStats().Count - 1; i >= 0; i--)
						{
							if (sequence.GetStats()[i] == stat)
							{
								return sequence.GetStats()[i + 1];
							}
						}
					}
					break;
				}
			}
			return GetNextStatement(parent);
		}

		private static bool ExistsPath(Statement from, Statement to)
		{
			foreach (StatEdge edge in to.GetAllPredecessorEdges())
			{
				if (from.ContainsStatementStrict(edge.GetSource()))
				{
					return true;
				}
			}
			return false;
		}

		private class IfNode
		{
			public readonly Statement value;

			public readonly List<IfHelper.IfNode> succs = new List<IfHelper.IfNode>();

			public readonly List<int> edgetypes = new List<int>();

			internal IfNode(Statement value)
			{
				this.value = value;
			}

			public virtual void AddChild(IfHelper.IfNode child, int type)
			{
				succs.Add(child);
				edgetypes.Add(type);
			}
		}
	}
}
