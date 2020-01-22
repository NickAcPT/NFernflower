// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;
using System.Linq;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class DecHelper
	{
		public static bool CheckStatementExceptions<_T0>(List<_T0> lst)
			where _T0 : Statement
		{
			HashSet<Statement> all = new HashSet<Statement>(lst);
			HashSet<Statement> handlers = new HashSet<Statement>();
			HashSet<Statement> intersection = null;
			foreach (var stat in lst)
			{
				HashSet<Statement> setNew = stat.GetNeighboursSet(StatEdge.Type_Exception, Statement
					.Direction_Forward);
				if (intersection == null)
				{
					intersection = setNew;
				}
				else
				{
					HashSet<Statement> interclone = new HashSet<Statement>(intersection);
					interclone.RemoveAll(setNew);
					intersection.RetainAll(setNew);
					setNew.RemoveAll(intersection);
					Sharpen.Collections.AddAll(handlers, interclone);
					Sharpen.Collections.AddAll(handlers, setNew);
				}
			}
			foreach (Statement stat in handlers)
			{
				if (!all.Contains(stat) || !(stat.GetNeighbours(StatEdge.Type_Exception
					    , Statement.Direction_Backward).All(all.Contains)))
				{
					return false;
				}
			}
			// check for other handlers (excluding head)
			for (int i = 1; i < lst.Count; i++)
			{
				var stat = lst[i];
				if (!(stat.GetPredecessorEdges(StatEdge.Type_Exception).Count == 0) && !handlers.
					Contains(stat))
				{
					return false;
				}
			}
			return true;
		}

		public static bool IsChoiceStatement(Statement head, List<Statement> lst)
		{
			Statement post = null;
			HashSet<Statement> setDest = head.GetNeighboursSet(StatEdge.Type_Regular, Statement
				.Direction_Forward);
			if (setDest.Contains(head))
			{
				return false;
			}
			while (true)
			{
				lst.Clear();
				bool repeat = false;
				setDest.Remove(post);
				foreach (Statement stat in setDest)
				{
					if (stat.GetLastBasicType() != Statement.Lastbasictype_General)
					{
						if (post == null)
						{
							post = stat;
							repeat = true;
							break;
						}
						else
						{
							return false;
						}
					}
					// preds
					HashSet<Statement> setPred = stat.GetNeighboursSet(StatEdge.Type_Regular, Statement
						.Direction_Backward);
					setPred.Remove(head);
					if (setPred.Contains(stat))
					{
						return false;
					}
					if (!setPred.All(setDest.Contains) || setPred.Count > 1)
					{
						if (post == null)
						{
							post = stat;
							repeat = true;
							break;
						}
						else
						{
							return false;
						}
					}
					else if (setPred.Count == 1)
					{
						Statement pred = setPred.GetEnumerator().Current;
						while (lst.Contains(pred))
						{
							HashSet<Statement> setPredTemp = pred.GetNeighboursSet(StatEdge.Type_Regular, Statement
								.Direction_Backward);
							setPredTemp.Remove(head);
							if (!(setPredTemp.Count == 0))
							{
								// at most 1 predecessor
								pred = setPredTemp.GetEnumerator().Current;
								if (pred == stat)
								{
									return false;
								}
							}
							else
							{
								// loop found
								break;
							}
						}
					}
					// succs
					List<StatEdge> lstEdges = stat.GetSuccessorEdges(Statement.Statedge_Direct_All);
					if (lstEdges.Count > 1)
					{
						HashSet<Statement> setSucc = stat.GetNeighboursSet(Statement.Statedge_Direct_All, 
							Statement.Direction_Forward);
						setSucc.RetainAll(setDest);
						if (setSucc.Count > 0)
						{
							return false;
						}
						else if (post == null)
						{
							post = stat;
							repeat = true;
							break;
						}
						else
						{
							return false;
						}
					}
					else if (lstEdges.Count == 1)
					{
						StatEdge edge = lstEdges[0];
						if (edge.GetType() == StatEdge.Type_Regular)
						{
							Statement statd = edge.GetDestination();
							if (head == statd)
							{
								return false;
							}
							if (post != statd && !setDest.Contains(statd))
							{
								if (post != null)
								{
									return false;
								}
								else
								{
									HashSet<Statement> set = statd.GetNeighboursSet(StatEdge.Type_Regular, Statement.
										Direction_Backward);
									if (set.Count > 1)
									{
										post = statd;
										repeat = true;
										break;
									}
									else
									{
										return false;
									}
								}
							}
						}
					}
					lst.Add(stat);
				}
				if (!repeat)
				{
					break;
				}
			}
			lst.Add(head);
			lst.Remove(post);
			lst.Add(0, post);
			return true;
		}

		public static HashSet<Statement> GetUniquePredExceptions(Statement head)
		{
			HashSet<Statement> setHandlers = new HashSet<Statement>(head.GetNeighbours(StatEdge
				.Type_Exception, Statement.Direction_Forward));
			setHandlers.RemoveWhere((Statement statement) => statement.GetPredecessorEdges(StatEdge
				.Type_Exception).Count > 1);
			return setHandlers;
		}

		public static List<Exprent> CopyExprentList<_T0>(List<_T0> lst)
			where _T0 : Exprent
		{
			List<Exprent> ret = new List<Exprent>();
			foreach (var expr in lst)
			{
				ret.Add(expr.Copy());
			}
			return ret;
		}
	}
}
