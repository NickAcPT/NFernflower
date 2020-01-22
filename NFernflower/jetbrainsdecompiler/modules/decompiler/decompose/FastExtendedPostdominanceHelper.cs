// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Decompose
{
	public class FastExtendedPostdominanceHelper
	{
		private List<Statement> lstReversePostOrderList;

		private Dictionary<int, FastFixedSetFactory.FastFixedSet<int>> mapSupportPoints = 
			new Dictionary<int, FastFixedSetFactory.FastFixedSet<int>>();

		private readonly Dictionary<int, FastFixedSetFactory.FastFixedSet<int>> mapExtPostdominators
			 = new Dictionary<int, FastFixedSetFactory.FastFixedSet<int>>();

		private Statement statement;

		private FastFixedSetFactory<int> factory;

		public virtual Dictionary<int, HashSet<int>> GetExtendedPostdominators(Statement 
			statement)
		{
			this.statement = statement;
			HashSet<int> set = new HashSet<int>();
			foreach (Statement st in statement.GetStats())
			{
				set.Add(st.id);
			}
			this.factory = new FastFixedSetFactory<int>(set);
			lstReversePostOrderList = statement.GetReversePostOrderList();
			//		try {
			//			DotExporter.toDotFile(statement, new File("c:\\Temp\\stat1.dot"));
			//		} catch (Exception ex) {
			//			ex.printStackTrace();
			//		}
			CalcDefaultReachableSets();
			RemoveErroneousNodes();
			DominatorTreeExceptionFilter filter = new DominatorTreeExceptionFilter(statement);
			filter.Initialize();
			FilterOnExceptionRanges(filter);
			FilterOnDominance(filter);
			HashSet<KeyValuePair<int, FastFixedSetFactory.FastFixedSet<int>>> entries = mapExtPostdominators;
			Dictionary<int, HashSet<int>> res = new Dictionary<int, HashSet<int>>(entries.Count
				);
			foreach (KeyValuePair<int, FastFixedSetFactory.FastFixedSet<int>> entry in entries)
			{
				Sharpen.Collections.Put(res, entry.Key, entry.Value.ToPlainSet());
			}
			return res;
		}

		private void FilterOnDominance(DominatorTreeExceptionFilter filter)
		{
			DominatorEngine engine = filter.GetDomEngine();
			foreach (int head in new HashSet<int>(mapExtPostdominators.Keys))
			{
				FastFixedSetFactory.FastFixedSet<int> setPostdoms = mapExtPostdominators.GetOrNull
					(head);
				LinkedList<Statement> stack = new LinkedList<Statement>();
				LinkedList<FastFixedSetFactory.FastFixedSet<int>> stackPath = new LinkedList<FastFixedSetFactory.FastFixedSet
					<int>>();
				stack.Add(statement.GetStats().GetWithKey(head));
				stackPath.Add(factory.SpawnEmptySet());
				HashSet<Statement> setVisited = new HashSet<Statement>();
				setVisited.Add(stack.GetFirst());
				while (!(stack.Count == 0))
				{
					Statement stat = Sharpen.Collections.RemoveFirst(stack);
					FastFixedSetFactory.FastFixedSet<int> path = Sharpen.Collections.RemoveFirst(stackPath
						);
					if (setPostdoms.Contains(stat.id))
					{
						path.Add(stat.id);
					}
					if (path.Contains(setPostdoms))
					{
						continue;
					}
					if (!engine.IsDominator(stat.id, head))
					{
						setPostdoms.Complement(path);
						continue;
					}
					foreach (StatEdge edge in stat.GetSuccessorEdges(StatEdge.Type_Regular))
					{
						Statement edge_destination = edge.GetDestination();
						if (!setVisited.Contains(edge_destination))
						{
							stack.Add(edge_destination);
							stackPath.Add(path.GetCopy());
							setVisited.Add(edge_destination);
						}
					}
				}
				if (setPostdoms.IsEmpty())
				{
					Sharpen.Collections.Remove(mapExtPostdominators, head);
				}
			}
		}

		private void FilterOnExceptionRanges(DominatorTreeExceptionFilter filter)
		{
			foreach (int head in new HashSet<int>(mapExtPostdominators.Keys))
			{
				FastFixedSetFactory.FastFixedSet<int> set = mapExtPostdominators.GetOrNull(head);
				for (IEnumerator<int> it = set.GetEnumerator(); it.MoveNext(); )
				{
					if (!filter.AcceptStatementPair(head, it.Current))
					{
						it.Remove();
					}
				}
				if (set.IsEmpty())
				{
					Sharpen.Collections.Remove(mapExtPostdominators, head);
				}
			}
		}

		private void RemoveErroneousNodes()
		{
			mapSupportPoints = new Dictionary<int, FastFixedSetFactory.FastFixedSet<int>>();
			CalcReachabilitySuppPoints(StatEdge.Type_Regular);
			IterateReachability((Statement node, Dictionary<int, FastFixedSetFactory.FastFixedSet
				<int>> mapSets) => 			{
				int nodeid = node.id;
				FastFixedSetFactory.FastFixedSet<int> setReachability = mapSets.GetOrNull(nodeid);
				List<FastFixedSetFactory.FastFixedSet<int>> lstPredSets = new List<FastFixedSetFactory.FastFixedSet
					<int>>();
				foreach (StatEdge prededge in node.GetPredecessorEdges(StatEdge.Type_Regular))
				{
					FastFixedSetFactory.FastFixedSet<int> setPred = mapSets.GetOrNull(prededge.GetSource
						().id);
					if (setPred == null)
					{
						setPred = mapSupportPoints.GetOrNull(prededge.GetSource().id);
					}
					// setPred cannot be empty as it is a reachability set
					lstPredSets.Add(setPred);
				}
				foreach (int id in setReachability)
				{
					FastFixedSetFactory.FastFixedSet<int> setReachabilityCopy = setReachability.GetCopy
						();
					FastFixedSetFactory.FastFixedSet<int> setIntersection = factory.SpawnEmptySet();
					bool isIntersectionInitialized = false;
					foreach (FastFixedSetFactory.FastFixedSet<int> predset in lstPredSets)
					{
						if (predset.Contains(id))
						{
							if (!isIntersectionInitialized)
							{
								setIntersection.Union(predset);
								isIntersectionInitialized = true;
							}
							else
							{
								setIntersection.Intersection(predset);
							}
						}
					}
					if (nodeid != id)
					{
						setIntersection.Add(nodeid);
					}
					else
					{
						setIntersection.Remove(nodeid);
					}
					setReachabilityCopy.Complement(setIntersection);
					mapExtPostdominators.GetOrNull(id).Complement(setReachabilityCopy);
				}
				return false;
			}
, StatEdge.Type_Regular);
			// exception handlers cannot be postdominator nodes
			// TODO: replace with a standard set?
			FastFixedSetFactory.FastFixedSet<int> setHandlers = factory.SpawnEmptySet();
			bool handlerfound = false;
			foreach (Statement stat in statement.GetStats())
			{
				if ((stat.GetPredecessorEdges(Statement.Statedge_Direct_All).Count == 0) && !(stat
					.GetPredecessorEdges(StatEdge.Type_Exception).Count == 0))
				{
					// exception handler
					setHandlers.Add(stat.id);
					handlerfound = true;
				}
			}
			if (handlerfound)
			{
				foreach (FastFixedSetFactory.FastFixedSet<int> set in mapExtPostdominators.Values)
				{
					set.Complement(setHandlers);
				}
			}
		}

		private void CalcDefaultReachableSets()
		{
			int edgetype = StatEdge.Type_Regular | StatEdge.Type_Exception;
			CalcReachabilitySuppPoints(edgetype);
			foreach (Statement stat in statement.GetStats())
			{
				Sharpen.Collections.Put(mapExtPostdominators, stat.id, factory.SpawnEmptySet());
			}
			IterateReachability((Statement node, Dictionary<int, FastFixedSetFactory.FastFixedSet
				<int>> mapSets) => 			{
				int nodeid = node.id;
				FastFixedSetFactory.FastFixedSet<int> setReachability = mapSets.GetOrNull(nodeid);
				foreach (int id in setReachability)
				{
					mapExtPostdominators.GetOrNull(id).Add(nodeid);
				}
				return false;
			}
, edgetype);
		}

		private void CalcReachabilitySuppPoints(int edgetype)
		{
			IterateReachability((Statement node, Dictionary<int, FastFixedSetFactory.FastFixedSet
				<int>> mapSets) => 			{
				// consider to be a support point
				foreach (StatEdge sucedge in node.GetAllSuccessorEdges())
				{
					if ((sucedge.GetType() & edgetype) != 0)
					{
						if (mapSets.ContainsKey(sucedge.GetDestination().id))
						{
							FastFixedSetFactory.FastFixedSet<int> setReachability = mapSets.GetOrNull(node.id
								);
							if (!InterpreterUtil.EqualObjects(setReachability, mapSupportPoints.GetOrNull(node
								.id)))
							{
								Sharpen.Collections.Put(mapSupportPoints, node.id, setReachability);
								return true;
							}
						}
					}
				}
				return false;
			}
, edgetype);
		}

		private void IterateReachability(FastExtendedPostdominanceHelper.IIReachabilityAction
			 action, int edgetype)
		{
			while (true)
			{
				bool iterate = false;
				Dictionary<int, FastFixedSetFactory.FastFixedSet<int>> mapSets = new Dictionary<int
					, FastFixedSetFactory.FastFixedSet<int>>();
				foreach (Statement stat in lstReversePostOrderList)
				{
					FastFixedSetFactory.FastFixedSet<int> set = factory.SpawnEmptySet();
					set.Add(stat.id);
					foreach (StatEdge prededge in stat.GetAllPredecessorEdges())
					{
						if ((prededge.GetType() & edgetype) != 0)
						{
							Statement pred = prededge.GetSource();
							FastFixedSetFactory.FastFixedSet<int> setPred = mapSets.GetOrNull(pred.id);
							if (setPred == null)
							{
								setPred = mapSupportPoints.GetOrNull(pred.id);
							}
							if (setPred != null)
							{
								set.Union(setPred);
							}
						}
					}
					Sharpen.Collections.Put(mapSets, stat.id, set);
					if (action != null)
					{
						iterate |= action.Action(stat, mapSets);
					}
					// remove reachability information of fully processed nodes (saves memory)
					foreach (StatEdge prededge in stat.GetAllPredecessorEdges())
					{
						if ((prededge.GetType() & edgetype) != 0)
						{
							Statement pred = prededge.GetSource();
							if (mapSets.ContainsKey(pred.id))
							{
								bool remstat = true;
								foreach (StatEdge sucedge in pred.GetAllSuccessorEdges())
								{
									if ((sucedge.GetType() & edgetype) != 0)
									{
										if (!mapSets.ContainsKey(sucedge.GetDestination().id))
										{
											remstat = false;
											break;
										}
									}
								}
								if (remstat)
								{
									Sharpen.Collections.Put(mapSets, pred.id, null);
								}
							}
						}
					}
				}
				if (!iterate)
				{
					break;
				}
			}
		}

		private interface IIReachabilityAction
		{
			bool Action(Statement node, Dictionary<int, FastFixedSetFactory.FastFixedSet<int>
				> mapSets);
		}
	}
}
