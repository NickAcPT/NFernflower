// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class LabelHelper
	{
		public static void CleanUpEdges(RootStatement root)
		{
			ResetAllEdges(root);
			RemoveNonImmediateEdges(root);
			LiftClosures(root);
			LowContinueLabels(root, new HashSet<StatEdge>());
			LowClosures(root);
		}

		public static void IdentifyLabels(RootStatement root)
		{
			SetExplicitEdges(root);
			HideDefaultSwitchEdges(root);
			ProcessStatementLabel(root);
			SetRetEdgesUnlabeled(root);
		}

		private static void LiftClosures(Statement stat)
		{
			foreach (StatEdge edge in stat.GetAllSuccessorEdges())
			{
				switch (edge.GetType())
				{
					case StatEdge.Type_Continue:
					{
						if (edge.GetDestination() != edge.closure)
						{
							edge.GetDestination().AddLabeledEdge(edge);
						}
						break;
					}

					case StatEdge.Type_Break:
					{
						Statement dest = edge.GetDestination();
						if (dest.type != Statement.Type_Dummyexit)
						{
							Statement parent = dest.GetParent();
							List<Statement> lst = new List<Statement>();
							if (parent.type == Statement.Type_Sequence)
							{
								Sharpen.Collections.AddAll(lst, parent.GetStats());
							}
							else if (parent.type == Statement.Type_Switch)
							{
								Sharpen.Collections.AddAll(lst, ((SwitchStatement)parent).GetCaseStatements());
							}
							for (int i = 0; i < lst.Count; i++)
							{
								if (lst[i] == dest)
								{
									lst[i - 1].AddLabeledEdge(edge);
									break;
								}
							}
						}
						break;
					}
				}
			}
			foreach (Statement st in stat.GetStats())
			{
				LiftClosures(st);
			}
		}

		private static void RemoveNonImmediateEdges(Statement stat)
		{
			foreach (Statement st in stat.GetStats())
			{
				RemoveNonImmediateEdges(st);
			}
			if (!stat.HasBasicSuccEdge())
			{
				foreach (StatEdge edge in stat.GetSuccessorEdges(StatEdge.Type_Continue | StatEdge
					.Type_Break))
				{
					stat.RemoveSuccessor(edge);
				}
			}
		}

		public static void LowContinueLabels(Statement stat, HashSet<StatEdge> edges)
		{
			bool ok = (stat.type != Statement.Type_Do);
			if (!ok)
			{
				DoStatement dostat = (DoStatement)stat;
				ok = dostat.GetLooptype() == DoStatement.Loop_Do || dostat.GetLooptype() == DoStatement
					.Loop_While || (dostat.GetLooptype() == DoStatement.Loop_For && dostat.GetIncExprent
					() == null);
			}
			if (ok)
			{
				Sharpen.Collections.AddAll(edges, stat.GetPredecessorEdges(StatEdge.Type_Continue
					));
			}
			if (ok && stat.type == Statement.Type_Do)
			{
				foreach (StatEdge edge in edges)
				{
					if (stat.ContainsStatementStrict(edge.GetSource()))
					{
						edge.GetDestination().RemovePredecessor(edge);
						edge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, edge, stat);
						stat.AddPredecessor(edge);
						stat.AddLabeledEdge(edge);
					}
				}
			}
			foreach (Statement st in stat.GetStats())
			{
				if (st == stat.GetFirst())
				{
					LowContinueLabels(st, edges);
				}
				else
				{
					LowContinueLabels(st, new HashSet<StatEdge>());
				}
			}
		}

		public static void LowClosures(Statement stat)
		{
			foreach (StatEdge edge in new List<StatEdge>(stat.GetLabelEdges()))
			{
				if (edge.GetType() == StatEdge.Type_Break)
				{
					// FIXME: ?
					foreach (Statement st in stat.GetStats())
					{
						if (st.ContainsStatementStrict(edge.GetSource()))
						{
							if (MergeHelper.IsDirectPath(st, edge.GetDestination()))
							{
								st.AddLabeledEdge(edge);
							}
						}
					}
				}
			}
			foreach (Statement st in stat.GetStats())
			{
				LowClosures(st);
			}
		}

		private static void ResetAllEdges(Statement stat)
		{
			foreach (Statement st in stat.GetStats())
			{
				ResetAllEdges(st);
			}
			foreach (StatEdge edge in stat.GetAllSuccessorEdges())
			{
				edge.@explicit = true;
				edge.labeled = true;
			}
		}

		private static void SetRetEdgesUnlabeled(RootStatement root)
		{
			Statement exit = root.GetDummyExit();
			foreach (StatEdge edge in exit.GetAllPredecessorEdges())
			{
				List<Exprent> lst = edge.GetSource().GetExprents();
				if (edge.GetType() == StatEdge.Type_Finallyexit || (lst != null && !(lst.Count == 0)
					 && lst[lst.Count - 1].type == Exprent.Exprent_Exit))
				{
					edge.labeled = false;
				}
			}
		}

		private static Dictionary<Statement, List<StatEdge>> SetExplicitEdges(Statement 
			stat)
		{
			Dictionary<Statement, List<StatEdge>> mapEdges = new Dictionary<Statement, List
				<StatEdge>>();
			if (stat.GetExprents() != null)
			{
				return mapEdges;
			}
			switch (stat.type)
			{
				case Statement.Type_Trycatch:
				case Statement.Type_Catchall:
				{
					foreach (Statement st in stat.GetStats())
					{
						Dictionary<Statement, List<StatEdge>> mapEdges1 = SetExplicitEdges(st);
						ProcessEdgesWithNext(st, mapEdges1, null);
						if (stat.type == Statement.Type_Trycatch || st == stat.GetFirst())
						{
							// edges leaving a finally catch block are always explicit
							// merge the maps
							if (mapEdges1 != null)
							{
								foreach (KeyValuePair<Statement, List<StatEdge>> entr in mapEdges1)
								{
									if (mapEdges.ContainsKey(entr.Key))
									{
										Sharpen.Collections.AddAll(mapEdges.GetOrNull(entr.Key), entr.Value);
									}
									else
									{
										Sharpen.Collections.Put(mapEdges, entr.Key, entr.Value);
									}
								}
							}
						}
					}
					break;
				}

				case Statement.Type_Do:
				{
					mapEdges = SetExplicitEdges(stat.GetFirst());
					ProcessEdgesWithNext(stat.GetFirst(), mapEdges, stat);
					break;
				}

				case Statement.Type_If:
				{
					IfStatement ifstat = (IfStatement)stat;
					// head statement is a basic block
					if (ifstat.GetIfstat() == null)
					{
						// empty if
						ProcessEdgesWithNext(ifstat.GetFirst(), mapEdges, null);
					}
					else
					{
						mapEdges = SetExplicitEdges(ifstat.GetIfstat());
						ProcessEdgesWithNext(ifstat.GetIfstat(), mapEdges, null);
						Dictionary<Statement, List<StatEdge>> mapEdges1 = null;
						if (ifstat.GetElsestat() != null)
						{
							mapEdges1 = SetExplicitEdges(ifstat.GetElsestat());
							ProcessEdgesWithNext(ifstat.GetElsestat(), mapEdges1, null);
						}
						// merge the maps
						if (mapEdges1 != null)
						{
							foreach (KeyValuePair<Statement, List<StatEdge>> entr in mapEdges1)
							{
								if (mapEdges.ContainsKey(entr.Key))
								{
									Sharpen.Collections.AddAll(mapEdges.GetOrNull(entr.Key), entr.Value);
								}
								else
								{
									Sharpen.Collections.Put(mapEdges, entr.Key, entr.Value);
								}
							}
						}
					}
					break;
				}

				case Statement.Type_Root:
				{
					mapEdges = SetExplicitEdges(stat.GetFirst());
					ProcessEdgesWithNext(stat.GetFirst(), mapEdges, ((RootStatement)stat).GetDummyExit
						());
					break;
				}

				case Statement.Type_Sequence:
				{
					int index = 0;
					while (index < stat.GetStats().Count - 1)
					{
						Statement st = stat.GetStats()[index];
						ProcessEdgesWithNext(st, SetExplicitEdges(st), stat.GetStats()[index + 1]);
						index++;
					}
					Statement st_1 = stat.GetStats()[index];
					mapEdges = SetExplicitEdges(st_1);
					ProcessEdgesWithNext(st_1, mapEdges, null);
					break;
				}

				case Statement.Type_Switch:
				{
					SwitchStatement swst = (SwitchStatement)stat;
					for (int i = 0; i < swst.GetCaseStatements().Count - 1; i++)
					{
						Statement stt = swst.GetCaseStatements()[i];
						Statement stnext = swst.GetCaseStatements()[i + 1];
						if (stnext.GetExprents() != null && (stnext.GetExprents().Count == 0))
						{
							stnext = stnext.GetAllSuccessorEdges()[0].GetDestination();
						}
						ProcessEdgesWithNext(stt, SetExplicitEdges(stt), stnext);
					}
					int last = swst.GetCaseStatements().Count - 1;
					if (last >= 0)
					{
						// empty switch possible
						Statement stlast = swst.GetCaseStatements()[last];
						if (stlast.GetExprents() != null && (stlast.GetExprents().Count == 0))
						{
							StatEdge edge = stlast.GetAllSuccessorEdges()[0];
							Sharpen.Collections.Put(mapEdges, edge.GetDestination(), new List<StatEdge>(System.Linq.Enumerable.ToList(new [] {
								edge})));
						}
						else
						{
							mapEdges = SetExplicitEdges(stlast);
							ProcessEdgesWithNext(stlast, mapEdges, null);
						}
					}
					break;
				}

				case Statement.Type_Syncronized:
				{
					SynchronizedStatement synstat = (SynchronizedStatement)stat;
					ProcessEdgesWithNext(synstat.GetFirst(), SetExplicitEdges(stat.GetFirst()), synstat
						.GetBody());
					// FIXME: basic block?
					mapEdges = SetExplicitEdges(synstat.GetBody());
					ProcessEdgesWithNext(synstat.GetBody(), mapEdges, null);
					break;
				}
			}
			return mapEdges;
		}

		private static void ProcessEdgesWithNext(Statement stat, Dictionary<Statement, List<StatEdge>> mapEdges, Statement next)
		{
			StatEdge statedge = null;
			List<StatEdge> lstSuccs = stat.GetAllSuccessorEdges();
			if (!(lstSuccs.Count == 0))
			{
				statedge = lstSuccs[0];
				if (statedge.GetDestination() == next)
				{
					statedge.@explicit = false;
					statedge = null;
				}
				else
				{
					next = statedge.GetDestination();
				}
			}
			// no next for a do statement
			if (stat.type == Statement.Type_Do && ((DoStatement)stat).GetLooptype() == DoStatement
				.Loop_Do)
			{
				next = null;
			}
			if (next == null)
			{
				if (mapEdges.Count == 1)
				{
					List<StatEdge> lstEdges = new Sharpen.EnumeratorAdapter<List<StatEdge>>(mapEdges.Values.GetEnumerator()).Next();
					if (lstEdges.Count > 1 && new Sharpen.EnumeratorAdapter<Statement>(mapEdges.Keys.GetEnumerator()).Next().type != Statement
						.Type_Dummyexit)
					{
						StatEdge edge_example = lstEdges[0];
						Statement closure = stat.GetParent();
						if (!closure.ContainsStatementStrict(edge_example.closure))
						{
							closure = edge_example.closure;
						}
						StatEdge newedge = new StatEdge(edge_example.GetType(), stat, edge_example.GetDestination
							(), closure);
						stat.AddSuccessor(newedge);
						foreach (StatEdge edge in lstEdges)
						{
							edge.@explicit = false;
						}
						Sharpen.Collections.Put(mapEdges, newedge.GetDestination(), new List<StatEdge>(System.Linq.Enumerable.ToList(new [] {
							newedge})));
					}
				}
			}
			else
			{
				bool implfound = false;
				foreach (KeyValuePair<Statement, List<StatEdge>> entr in mapEdges)
				{
					if (entr.Key == next)
					{
						foreach (StatEdge edge in entr.Value)
						{
							edge.@explicit = false;
						}
						implfound = true;
						break;
					}
				}
				if ((stat.GetAllSuccessorEdges().Count == 0) && !implfound)
				{
					List<StatEdge> lstEdges = null;
					foreach (KeyValuePair<Statement, List<StatEdge>> entr in mapEdges)
					{
						if (entr.Key.type != Statement.Type_Dummyexit && (lstEdges == null || entr.Value.
							Count > lstEdges.Count))
						{
							lstEdges = entr.Value;
						}
					}
					if (lstEdges != null && lstEdges.Count > 1)
					{
						StatEdge edge_example = lstEdges[0];
						Statement closure = stat.GetParent();
						if (!closure.ContainsStatementStrict(edge_example.closure))
						{
							closure = edge_example.closure;
						}
						StatEdge newedge = new StatEdge(edge_example.GetType(), stat, edge_example.GetDestination
							(), closure);
						stat.AddSuccessor(newedge);
						foreach (StatEdge edge in lstEdges)
						{
							edge.@explicit = false;
						}
					}
				}
				mapEdges.Clear();
			}
			if (statedge != null)
			{
				Sharpen.Collections.Put(mapEdges, statedge.GetDestination(), new List<StatEdge>(System.Linq.Enumerable.ToList(new [] {
					statedge})));
			}
		}

		private static void HideDefaultSwitchEdges(Statement stat)
		{
			if (stat.type == Statement.Type_Switch)
			{
				SwitchStatement swst = (SwitchStatement)stat;
				int last = swst.GetCaseStatements().Count - 1;
				if (last >= 0)
				{
					// empty switch possible
					Statement stlast = swst.GetCaseStatements()[last];
					if (stlast.GetExprents() != null && (stlast.GetExprents().Count == 0))
					{
						if (!stlast.GetAllSuccessorEdges()[0].@explicit)
						{
							List<StatEdge> lstEdges = swst.GetCaseEdges()[last];
							lstEdges.Remove(swst.GetDefault_edge());
							if ((lstEdges.Count == 0))
							{
								swst.GetCaseStatements().RemoveAtReturningValue(last);
								swst.GetCaseEdges().RemoveAtReturningValue(last);
							}
						}
					}
				}
			}
			foreach (Statement st in stat.GetStats())
			{
				HideDefaultSwitchEdges(st);
			}
		}

		private class LabelSets
		{
			internal readonly HashSet<Statement> breaks = new HashSet<Statement>();

			internal readonly HashSet<Statement> continues = new HashSet<Statement>();
		}

		private static LabelHelper.LabelSets ProcessStatementLabel(Statement stat)
		{
			LabelHelper.LabelSets sets = new LabelHelper.LabelSets();
			if (stat.GetExprents() == null)
			{
				foreach (Statement st in stat.GetStats())
				{
					LabelHelper.LabelSets nested = ProcessStatementLabel(st);
					Sharpen.Collections.AddAll(sets.breaks, nested.breaks);
					Sharpen.Collections.AddAll(sets.continues, nested.continues);
				}
				bool shieldType = (stat.type == Statement.Type_Do || stat.type == Statement.Type_Switch
					);
				if (shieldType)
				{
					foreach (StatEdge edge in stat.GetLabelEdges())
					{
						if (edge.@explicit && ((edge.GetType() == StatEdge.Type_Break && sets.breaks.Contains
							(edge.GetSource())) || (edge.GetType() == StatEdge.Type_Continue && sets.continues
							.Contains(edge.GetSource()))))
						{
							edge.labeled = false;
						}
					}
				}
				switch (stat.type)
				{
					case Statement.Type_Do:
					{
						sets.continues.Clear();
						goto case Statement.Type_Switch;
					}

					case Statement.Type_Switch:
					{
						sets.breaks.Clear();
						break;
					}
				}
			}
			sets.breaks.Add(stat);
			sets.continues.Add(stat);
			return sets;
		}

		public static void ReplaceContinueWithBreak(Statement stat)
		{
			if (stat.type == Statement.Type_Do)
			{
				List<StatEdge> lst = stat.GetPredecessorEdges(StatEdge.Type_Continue);
				foreach (StatEdge edge in lst)
				{
					if (edge.@explicit)
					{
						Statement minclosure = GetMinContinueClosure(edge);
						if (minclosure != edge.closure && !InlineSingleBlockHelper.IsBreakEdgeLabeled(edge
							.GetSource(), minclosure))
						{
							edge.GetSource().ChangeEdgeType(Statement.Direction_Forward, edge, StatEdge.Type_Break
								);
							edge.labeled = false;
							minclosure.AddLabeledEdge(edge);
						}
					}
				}
			}
			foreach (Statement st in stat.GetStats())
			{
				ReplaceContinueWithBreak(st);
			}
		}

		private static Statement GetMinContinueClosure(StatEdge edge)
		{
			Statement closure = edge.closure;
			while (true)
			{
				bool found = false;
				foreach (Statement st in closure.GetStats())
				{
					if (st.ContainsStatementStrict(edge.GetSource()))
					{
						if (MergeHelper.IsDirectPath(st, edge.GetDestination()))
						{
							closure = st;
							found = true;
							break;
						}
					}
				}
				if (!found)
				{
					break;
				}
			}
			return closure;
		}
	}
}
