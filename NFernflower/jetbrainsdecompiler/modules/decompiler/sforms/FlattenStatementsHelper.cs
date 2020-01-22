// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Sforms
{
	public class FlattenStatementsHelper
	{
		private readonly IDictionary<int, string[]> mapDestinationNodes = new Dictionary<
			int, string[]>();

		private readonly List<FlattenStatementsHelper.Edge> listEdges = new List<FlattenStatementsHelper.Edge
			>();

		private readonly IDictionary<string, List<string[]>> mapShortRangeFinallyPathIds
			 = new Dictionary<string, List<string[]>>();

		private readonly IDictionary<string, List<string[]>> mapLongRangeFinallyPathIds = 
			new Dictionary<string, List<string[]>>();

		private readonly IDictionary<string, int> mapPosIfBranch = new Dictionary<string, 
			int>();

		private DirectGraph graph;

		private RootStatement root;

		// statement.id, node.id(direct), node.id(continue)
		// node.id(source), statement.id(destination), edge type
		// node.id(exit), [node.id(source), statement.id(destination)]
		// node.id(exit), [node.id(source), statement.id(destination)]
		// positive if branches
		public virtual DirectGraph BuildDirectGraph(RootStatement root)
		{
			this.root = root;
			graph = new DirectGraph();
			FlattenStatement();
			// dummy exit node
			Statement dummyexit = root.GetDummyExit();
			DirectNode node = new DirectNode(DirectNode.Node_Direct, dummyexit, dummyexit.id.
				ToString());
			node.exprents = new List<Exprent>();
			graph.nodes.AddWithKey(node, node.id);
			Sharpen.Collections.Put(mapDestinationNodes, dummyexit.id, new string[] { node.id
				, null });
			SetEdges();
			graph.first = graph.nodes.GetWithKey(mapDestinationNodes.GetOrNull(root.id)[0]);
			graph.SortReversePostOrder();
			return graph;
		}

		private void FlattenStatement()
		{
			LinkedList<_T2081736913> lstStackStatements = new LinkedList<_T2081736913>();
			lstStackStatements.Add(new _T2081736913(this, root, new LinkedList<FlattenStatementsHelper.StackEntry
				>(), null));
			while (!(lstStackStatements.Count == 0))
			{
				_T2081736913 statEntry = Sharpen.Collections.RemoveFirst(lstStackStatements);
				Statement stat = statEntry.statement;
				LinkedList<FlattenStatementsHelper.StackEntry> stackFinally = statEntry.stackFinally;
				int statementBreakIndex = statEntry.statementIndex;
				DirectNode node;
				DirectNode nd;
				List<StatEdge> lstSuccEdges = new List<StatEdge>();
				DirectNode sourcenode = null;
				if (statEntry.succEdges == null)
				{
					switch (stat.type)
					{
						case Statement.Type_Basicblock:
						{
							node = new DirectNode(DirectNode.Node_Direct, stat, (BasicBlockStatement)stat);
							if (stat.GetExprents() != null)
							{
								node.exprents = stat.GetExprents();
							}
							graph.nodes.PutWithKey(node, node.id);
							Sharpen.Collections.Put(mapDestinationNodes, stat.id, new string[] { node.id, null
								 });
							Sharpen.Collections.AddAll(lstSuccEdges, stat.GetSuccessorEdges(Statement.Statedge_Direct_All
								));
							sourcenode = node;
							List<Exprent> tailExprentList = statEntry.tailExprents;
							if (tailExprentList != null)
							{
								DirectNode tail = new DirectNode(DirectNode.Node_Tail, stat, stat.id + "_tail");
								tail.exprents = tailExprentList;
								graph.nodes.PutWithKey(tail, tail.id);
								Sharpen.Collections.Put(mapDestinationNodes, -stat.id, new string[] { tail.id, null
									 });
								listEdges.Add(new FlattenStatementsHelper.Edge(node.id, -stat.id, StatEdge.Type_Regular
									));
								sourcenode = tail;
							}
							// 'if' statement: record positive branch
							if (stat.GetLastBasicType() == Statement.Lastbasictype_If)
							{
								Sharpen.Collections.Put(mapPosIfBranch, sourcenode.id, lstSuccEdges[0].GetDestination
									().id);
							}
							break;
						}

						case Statement.Type_Catchall:
						case Statement.Type_Trycatch:
						{
							DirectNode firstnd = new DirectNode(DirectNode.Node_Try, stat, stat.id + "_try");
							Sharpen.Collections.Put(mapDestinationNodes, stat.id, new string[] { firstnd.id, 
								null });
							graph.nodes.PutWithKey(firstnd, firstnd.id);
							LinkedList<_T2081736913> lst = new LinkedList<_T2081736913>();
							foreach (Statement st in stat.GetStats())
							{
								listEdges.Add(new FlattenStatementsHelper.Edge(firstnd.id, st.id, StatEdge.Type_Regular
									));
								LinkedList<FlattenStatementsHelper.StackEntry> stack = stackFinally;
								if (stat.type == Statement.Type_Catchall && ((CatchAllStatement)stat).IsFinally())
								{
									stack = new LinkedList<FlattenStatementsHelper.StackEntry>(stackFinally);
									if (st == stat.GetFirst())
									{
										// catch head
										stack.Add(new FlattenStatementsHelper.StackEntry((CatchAllStatement)stat, false));
									}
									else
									{
										// handler
										stack.Add(new FlattenStatementsHelper.StackEntry((CatchAllStatement)stat, true, StatEdge
											.Type_Break, root.GetDummyExit(), st, st, firstnd, firstnd, true));
									}
								}
								lst.Add(new _T2081736913(this, st, stack, null));
							}
							lstStackStatements.AddAll(0, lst);
							break;
						}

						case Statement.Type_Do:
						{
							if (statementBreakIndex == 0)
							{
								statEntry.statementIndex = 1;
								lstStackStatements.AddFirst(statEntry);
								lstStackStatements.AddFirst(new _T2081736913(this, stat.GetFirst(), stackFinally, 
									null));
								goto mainloop_continue;
							}
							nd = graph.nodes.GetWithKey(mapDestinationNodes.GetOrNull(stat.GetFirst().id)[0]);
							DoStatement dostat = (DoStatement)stat;
							int looptype = dostat.GetLooptype();
							if (looptype == DoStatement.Loop_Do)
							{
								Sharpen.Collections.Put(mapDestinationNodes, stat.id, new string[] { nd.id, nd.id
									 });
								break;
							}
							lstSuccEdges.Add(stat.GetSuccessorEdges(Statement.Statedge_Direct_All)[0]);
							switch (looptype)
							{
								case DoStatement.Loop_While:
								case DoStatement.Loop_Dowhile:
								{
									// exactly one edge
									node = new DirectNode(DirectNode.Node_Condition, stat, stat.id + "_cond");
									node.exprents = dostat.GetConditionExprentList();
									graph.nodes.PutWithKey(node, node.id);
									listEdges.Add(new FlattenStatementsHelper.Edge(node.id, stat.GetFirst().id, StatEdge
										.Type_Regular));
									if (looptype == DoStatement.Loop_While)
									{
										Sharpen.Collections.Put(mapDestinationNodes, stat.id, new string[] { node.id, node
											.id });
									}
									else
									{
										Sharpen.Collections.Put(mapDestinationNodes, stat.id, new string[] { nd.id, node.
											id });
										bool found = false;
										foreach (FlattenStatementsHelper.Edge edge in listEdges)
										{
											if (edge.statid.Equals(stat.id) && edge.edgetype == StatEdge.Type_Continue)
											{
												found = true;
												break;
											}
										}
										if (!found)
										{
											listEdges.Add(new FlattenStatementsHelper.Edge(nd.id, stat.id, StatEdge.Type_Continue
												));
										}
									}
									sourcenode = node;
									break;
								}

								case DoStatement.Loop_For:
								{
									DirectNode nodeinit = new DirectNode(DirectNode.Node_Init, stat, stat.id + "_init"
										);
									if (dostat.GetInitExprent() != null)
									{
										nodeinit.exprents = dostat.GetInitExprentList();
									}
									graph.nodes.PutWithKey(nodeinit, nodeinit.id);
									DirectNode nodecond = new DirectNode(DirectNode.Node_Condition, stat, stat.id + "_cond"
										);
									nodecond.exprents = dostat.GetConditionExprentList();
									graph.nodes.PutWithKey(nodecond, nodecond.id);
									DirectNode nodeinc = new DirectNode(DirectNode.Node_Increment, stat, stat.id + "_inc"
										);
									nodeinc.exprents = dostat.GetIncExprentList();
									graph.nodes.PutWithKey(nodeinc, nodeinc.id);
									Sharpen.Collections.Put(mapDestinationNodes, stat.id, new string[] { nodeinit.id, 
										nodeinc.id });
									Sharpen.Collections.Put(mapDestinationNodes, -stat.id, new string[] { nodecond.id
										, null });
									listEdges.Add(new FlattenStatementsHelper.Edge(nodecond.id, stat.GetFirst().id, StatEdge
										.Type_Regular));
									listEdges.Add(new FlattenStatementsHelper.Edge(nodeinit.id, -stat.id, StatEdge.Type_Regular
										));
									listEdges.Add(new FlattenStatementsHelper.Edge(nodeinc.id, -stat.id, StatEdge.Type_Regular
										));
									bool found_1 = false;
									foreach (FlattenStatementsHelper.Edge edge in listEdges)
									{
										if (edge.statid.Equals(stat.id) && edge.edgetype == StatEdge.Type_Continue)
										{
											found_1 = true;
											break;
										}
									}
									if (!found_1)
									{
										listEdges.Add(new FlattenStatementsHelper.Edge(nd.id, stat.id, StatEdge.Type_Continue
											));
									}
									sourcenode = nodecond;
									break;
								}
							}
							break;
						}

						case Statement.Type_Syncronized:
						case Statement.Type_Switch:
						case Statement.Type_If:
						case Statement.Type_Sequence:
						case Statement.Type_Root:
						{
							int statsize = stat.GetStats().Count;
							if (stat.type == Statement.Type_Syncronized)
							{
								statsize = 2;
							}
							// exclude the handler if synchronized
							if (statementBreakIndex <= statsize)
							{
								List<Exprent> tailexprlst = null;
								switch (stat.type)
								{
									case Statement.Type_Syncronized:
									{
										tailexprlst = ((SynchronizedStatement)stat).GetHeadexprentList();
										break;
									}

									case Statement.Type_Switch:
									{
										tailexprlst = ((SwitchStatement)stat).GetHeadexprentList();
										break;
									}

									case Statement.Type_If:
									{
										tailexprlst = ((IfStatement)stat).GetHeadexprentList();
										break;
									}
								}
								for (int i = statementBreakIndex; i < statsize; i++)
								{
									statEntry.statementIndex = i + 1;
									lstStackStatements.AddFirst(statEntry);
									lstStackStatements.AddFirst(new _T2081736913(this, stat.GetStats()[i], stackFinally
										, (i == 0 && tailexprlst != null && tailexprlst[0] != null) ? tailexprlst : null
										));
									goto mainloop_continue;
								}
								node = graph.nodes.GetWithKey(mapDestinationNodes.GetOrNull(stat.GetFirst().id)[0
									]);
								Sharpen.Collections.Put(mapDestinationNodes, stat.id, new string[] { node.id, null
									 });
								if (stat.type == Statement.Type_If && ((IfStatement)stat).iftype == IfStatement.Iftype_If)
								{
									lstSuccEdges.Add(stat.GetSuccessorEdges(Statement.Statedge_Direct_All)[0]);
									// exactly one edge
									sourcenode = tailexprlst[0] == null ? node : graph.nodes.GetWithKey(node.id + "_tail"
										);
								}
							}
							break;
						}
					}
				}
				// no successor edges
				if (sourcenode != null)
				{
					if (statEntry.succEdges != null)
					{
						lstSuccEdges = statEntry.succEdges;
					}
					for (int edgeindex = statEntry.edgeIndex; edgeindex < lstSuccEdges.Count; edgeindex
						++)
					{
						StatEdge edge = lstSuccEdges[edgeindex];
						LinkedList<FlattenStatementsHelper.StackEntry> stack = new LinkedList<FlattenStatementsHelper.StackEntry
							>(stackFinally);
						int edgetype = edge.GetType();
						Statement destination = edge.GetDestination();
						DirectNode finallyShortRangeSource = sourcenode;
						DirectNode finallyLongRangeSource = sourcenode;
						Statement finallyShortRangeEntry = null;
						Statement finallyLongRangeEntry = null;
						bool isFinallyMonitorExceptionPath = false;
						bool isFinallyExit = false;
						while (true)
						{
							FlattenStatementsHelper.StackEntry entry = null;
							if (!(stack.Count == 0))
							{
								entry = stack.GetLast();
							}
							bool created = true;
							if (entry == null)
							{
								SaveEdge(sourcenode, destination, edgetype, isFinallyExit ? finallyShortRangeSource
									 : null, finallyLongRangeSource, finallyShortRangeEntry, finallyLongRangeEntry, 
									isFinallyMonitorExceptionPath);
							}
							else
							{
								CatchAllStatement catchall = entry.catchstatement;
								if (entry.state)
								{
									// finally handler statement
									if (edgetype == StatEdge.Type_Finallyexit)
									{
										Sharpen.Collections.RemoveLast(stack);
										destination = entry.destination;
										edgetype = entry.edgetype;
										finallyShortRangeSource = entry.finallyShortRangeSource;
										finallyLongRangeSource = entry.finallyLongRangeSource;
										finallyShortRangeEntry = entry.finallyShortRangeEntry;
										finallyLongRangeEntry = entry.finallyLongRangeEntry;
										isFinallyExit = true;
										isFinallyMonitorExceptionPath = (catchall.GetMonitor() != null) & entry.isFinallyExceptionPath;
										created = false;
									}
									else if (!catchall.ContainsStatementStrict(destination))
									{
										Sharpen.Collections.RemoveLast(stack);
										created = false;
									}
									else
									{
										SaveEdge(sourcenode, destination, edgetype, isFinallyExit ? finallyShortRangeSource
											 : null, finallyLongRangeSource, finallyShortRangeEntry, finallyLongRangeEntry, 
											isFinallyMonitorExceptionPath);
									}
								}
								else if (!catchall.ContainsStatementStrict(destination))
								{
									// finally protected try statement
									SaveEdge(sourcenode, catchall.GetHandler(), StatEdge.Type_Regular, isFinallyExit ? 
										finallyShortRangeSource : null, finallyLongRangeSource, finallyShortRangeEntry, 
										finallyLongRangeEntry, isFinallyMonitorExceptionPath);
									Sharpen.Collections.RemoveLast(stack);
									stack.Add(new FlattenStatementsHelper.StackEntry(catchall, true, edgetype, destination
										, catchall.GetHandler(), finallyLongRangeEntry == null ? catchall.GetHandler() : 
										finallyLongRangeEntry, sourcenode, finallyLongRangeSource, false));
									statEntry.edgeIndex = edgeindex + 1;
									statEntry.succEdges = lstSuccEdges;
									lstStackStatements.AddFirst(statEntry);
									lstStackStatements.AddFirst(new _T2081736913(this, catchall.GetHandler(), stack, 
										null));
									goto mainloop_continue;
								}
								else
								{
									SaveEdge(sourcenode, destination, edgetype, isFinallyExit ? finallyShortRangeSource
										 : null, finallyLongRangeSource, finallyShortRangeEntry, finallyLongRangeEntry, 
										isFinallyMonitorExceptionPath);
								}
							}
							if (created)
							{
								break;
							}
						}
					}
				}
mainloop_continue: ;
			}
mainloop_break: ;
		}

		internal class _T2081736913
		{
			public readonly Statement statement;

			public readonly LinkedList<FlattenStatementsHelper.StackEntry> stackFinally;

			public readonly List<Exprent> tailExprents;

			public int statementIndex;

			public int edgeIndex;

			public List<StatEdge> succEdges;

			internal _T2081736913(FlattenStatementsHelper _enclosing, Statement statement, LinkedList
				<FlattenStatementsHelper.StackEntry> stackFinally, List<Exprent> tailExprents)
			{
				this._enclosing = _enclosing;
				this.statement = statement;
				this.stackFinally = stackFinally;
				this.tailExprents = tailExprents;
			}

			private readonly FlattenStatementsHelper _enclosing;
		}

		private void SaveEdge(DirectNode sourcenode, Statement destination, int edgetype, 
			DirectNode finallyShortRangeSource, DirectNode finallyLongRangeSource, Statement
			 finallyShortRangeEntry, Statement finallyLongRangeEntry, bool isFinallyMonitorExceptionPath
			)
		{
			if (edgetype != StatEdge.Type_Finallyexit)
			{
				listEdges.Add(new FlattenStatementsHelper.Edge(sourcenode.id, destination.id, edgetype
					));
			}
			if (finallyShortRangeSource != null)
			{
				bool isContinueEdge = (edgetype == StatEdge.Type_Continue);
				mapShortRangeFinallyPathIds.ComputeIfAbsent(sourcenode.id, (string k) => new List
					<string[]>()).Add(new string[] { finallyShortRangeSource.id, destination.id.ToString
					(), finallyShortRangeEntry.id.ToString(), isFinallyMonitorExceptionPath ? "1" : 
					null, isContinueEdge ? "1" : null });
				mapLongRangeFinallyPathIds.ComputeIfAbsent(sourcenode.id, (string k) => new List<
					string[]>()).Add(new string[] { finallyLongRangeSource.id, destination.id.ToString
					(), finallyLongRangeEntry.id.ToString(), isContinueEdge ? "1" : null });
			}
		}

		private void SetEdges()
		{
			foreach (FlattenStatementsHelper.Edge edge in listEdges)
			{
				string sourceid = edge.sourceid;
				int statid = edge.statid;
				DirectNode source = graph.nodes.GetWithKey(sourceid);
				DirectNode dest = graph.nodes.GetWithKey(mapDestinationNodes.GetOrNull(statid)[edge
					.edgetype == StatEdge.Type_Continue ? 1 : 0]);
				if (!source.succs.Contains(dest))
				{
					source.succs.Add(dest);
				}
				if (!dest.preds.Contains(source))
				{
					dest.preds.Add(source);
				}
				if (mapPosIfBranch.ContainsKey(sourceid) && !statid.Equals(mapPosIfBranch.GetOrNullable
					(sourceid)))
				{
					Sharpen.Collections.Put(graph.mapNegIfBranch, sourceid, dest.id);
				}
			}
			for (int i = 0; i < 2; i++)
			{
				foreach (KeyValuePair<string, List<string[]>> ent in (i == 0 ? mapShortRangeFinallyPathIds
					 : mapLongRangeFinallyPathIds))
				{
					List<FlattenStatementsHelper.FinallyPathWrapper> newLst = new List<FlattenStatementsHelper.FinallyPathWrapper
						>();
					List<string[]> lst = ent.Value;
					foreach (string[] arr in lst)
					{
						bool isContinueEdge = arr[i == 0 ? 4 : 3] != null;
						DirectNode dest = graph.nodes.GetWithKey(mapDestinationNodes.GetOrNull(System.Convert.ToInt32
							(arr[1]))[isContinueEdge ? 1 : 0]);
						DirectNode enter = graph.nodes.GetWithKey(mapDestinationNodes.GetOrNull(System.Convert.ToInt32
							(arr[2]))[0]);
						newLst.Add(new FlattenStatementsHelper.FinallyPathWrapper(arr[0], dest.id, enter.
							id));
						if (i == 0 && arr[3] != null)
						{
							Sharpen.Collections.Put(graph.mapFinallyMonitorExceptionPathExits, ent.Key, dest.
								id);
						}
					}
					if (!(newLst.Count == 0))
					{
						Sharpen.Collections.Put((i == 0 ? graph.mapShortRangeFinallyPaths : graph.mapLongRangeFinallyPaths
							), ent.Key, new List<FlattenStatementsHelper.FinallyPathWrapper>(new HashSet<FlattenStatementsHelper.FinallyPathWrapper
							>(newLst)));
					}
				}
			}
		}

		public virtual IDictionary<int, string[]> GetMapDestinationNodes()
		{
			return mapDestinationNodes;
		}

		public class FinallyPathWrapper
		{
			public readonly string source;

			public readonly string destination;

			public readonly string entry;

			private FinallyPathWrapper(string source, string destination, string entry)
			{
				this.source = source;
				this.destination = destination;
				this.entry = entry;
			}

			public override bool Equals(object o)
			{
				if (o == this)
				{
					return true;
				}
				if (!(o is FlattenStatementsHelper.FinallyPathWrapper))
				{
					return false;
				}
				FlattenStatementsHelper.FinallyPathWrapper fpw = (FlattenStatementsHelper.FinallyPathWrapper
					)o;
				return (source + ":" + destination + ":" + entry).Equals(fpw.source + ":" + fpw.destination
					 + ":" + fpw.entry);
			}

			public override int GetHashCode()
			{
				return (source + ":" + destination + ":" + entry).GetHashCode();
			}

			public override string ToString()
			{
				return source + "->(" + entry + ")->" + destination;
			}
		}

		private class StackEntry
		{
			public readonly CatchAllStatement catchstatement;

			public readonly bool state;

			public readonly int edgetype;

			public readonly bool isFinallyExceptionPath;

			public readonly Statement destination;

			public readonly Statement finallyShortRangeEntry;

			public readonly Statement finallyLongRangeEntry;

			public readonly DirectNode finallyShortRangeSource;

			public readonly DirectNode finallyLongRangeSource;

			internal StackEntry(CatchAllStatement catchstatement, bool state, int edgetype, Statement
				 destination, Statement finallyShortRangeEntry, Statement finallyLongRangeEntry, 
				DirectNode finallyShortRangeSource, DirectNode finallyLongRangeSource, bool isFinallyExceptionPath
				)
			{
				this.catchstatement = catchstatement;
				this.state = state;
				this.edgetype = edgetype;
				this.isFinallyExceptionPath = isFinallyExceptionPath;
				this.destination = destination;
				this.finallyShortRangeEntry = finallyShortRangeEntry;
				this.finallyLongRangeEntry = finallyLongRangeEntry;
				this.finallyShortRangeSource = finallyShortRangeSource;
				this.finallyLongRangeSource = finallyLongRangeSource;
			}

			internal StackEntry(CatchAllStatement catchstatement, bool state)
				: this(catchstatement, state, -1, null, null, null, null, null, false)
			{
			}
		}

		private class Edge
		{
			public readonly string sourceid;

			public readonly int statid;

			public readonly int edgetype;

			internal Edge(string sourceid, int statid, int edgetype)
			{
				this.sourceid = sourceid;
				this.statid = statid;
				this.edgetype = edgetype;
			}
		}
	}
}
