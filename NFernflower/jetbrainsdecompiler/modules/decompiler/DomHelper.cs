// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections;
using System.Collections.Generic;
using JetBrainsDecompiler.Code.Cfg;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Modules.Decompiler.Decompose;
using JetBrainsDecompiler.Modules.Decompiler.Deobfuscator;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class DomHelper
	{
		private static RootStatement GraphToStatement(ControlFlowGraph graph)
		{
			VBStyleCollection<Statement, int> stats = new VBStyleCollection<Statement, int>();
			VBStyleCollection<BasicBlock, int> blocks = graph.GetBlocks();
			foreach (BasicBlock block in blocks)
			{
				stats.AddWithKey(new BasicBlockStatement(block), block.id);
			}
			BasicBlock firstblock = graph.GetFirst();
			// head statement
			Statement firstst = stats.GetWithKey(firstblock.id);
			// dummy exit statement
			DummyExitStatement dummyexit = new DummyExitStatement();
			Statement general;
			if (stats.Count > 1 || firstblock.IsSuccessor(firstblock))
			{
				// multiple basic blocks or an infinite loop of one block
				general = new GeneralStatement(firstst, stats, null);
			}
			else
			{
				// one straightforward basic block
				RootStatement root = new RootStatement(firstst, dummyexit);
				firstst.AddSuccessor(new StatEdge(StatEdge.Type_Break, firstst, dummyexit, root));
				return root;
			}
			foreach (BasicBlock block in blocks)
			{
				Statement stat = stats.GetWithKey(block.id);
				foreach (BasicBlock succ in block.GetSuccs())
				{
					Statement stsucc = stats.GetWithKey(succ.id);
					int type;
					if (stsucc == firstst)
					{
						type = StatEdge.Type_Continue;
					}
					else if (graph.GetFinallyExits().Contains(block))
					{
						type = StatEdge.Type_Finallyexit;
						stsucc = dummyexit;
					}
					else if (succ.id == graph.GetLast().id)
					{
						type = StatEdge.Type_Break;
						stsucc = dummyexit;
					}
					else
					{
						type = StatEdge.Type_Regular;
					}
					stat.AddSuccessor(new StatEdge(type, stat, (type == StatEdge.Type_Continue) ? general
						 : stsucc, (type == StatEdge.Type_Regular) ? null : general));
				}
				// exceptions edges
				foreach (BasicBlock succex in block.GetSuccExceptions())
				{
					Statement stsuccex = stats.GetWithKey(succex.id);
					ExceptionRangeCFG range = graph.GetExceptionRange(succex, block);
					if (!range.IsCircular())
					{
						stat.AddSuccessor(new StatEdge(stat, stsuccex, range.GetExceptionTypes()));
					}
				}
			}
			general.BuildContinueSet();
			general.BuildMonitorFlags();
			return new RootStatement(general, dummyexit);
		}

		public static VBStyleCollection<List<int>, int> CalcPostDominators(Statement container
			)
		{
			Dictionary<Statement, FastFixedSetFactory.FastFixedSet<Statement>> lists = new Dictionary
				<Statement, FastFixedSetFactory.FastFixedSet<Statement>>();
			StrongConnectivityHelper schelper = new StrongConnectivityHelper(container);
			List<List<Statement>> components = schelper.GetComponents();
			List<Statement> lstStats = container.GetPostReversePostOrderList(StrongConnectivityHelper
				.GetExitReps(components));
			FastFixedSetFactory<Statement> factory = new FastFixedSetFactory<Statement>(lstStats
				);
			FastFixedSetFactory.FastFixedSet<Statement> setFlagNodes = factory.SpawnEmptySet(
				);
			setFlagNodes.SetAllElements();
			FastFixedSetFactory.FastFixedSet<Statement> initSet = factory.SpawnEmptySet();
			initSet.SetAllElements();
			foreach (List<Statement> lst in components)
			{
				FastFixedSetFactory.FastFixedSet<Statement> tmpSet;
				if (StrongConnectivityHelper.IsExitComponent(lst))
				{
					tmpSet = factory.SpawnEmptySet();
					tmpSet.AddAll(lst);
				}
				else
				{
					tmpSet = initSet.GetCopy();
				}
				foreach (Statement stat in lst)
				{
					Sharpen.Collections.Put(lists, stat, tmpSet);
				}
			}
			do
			{
				foreach (Statement stat in lstStats)
				{
					if (!setFlagNodes.Contains(stat))
					{
						continue;
					}
					setFlagNodes.Remove(stat);
					FastFixedSetFactory.FastFixedSet<Statement> doms = lists.GetOrNull(stat);
					FastFixedSetFactory.FastFixedSet<Statement> domsSuccs = factory.SpawnEmptySet();
					List<Statement> lstSuccs = stat.GetNeighbours(StatEdge.Type_Regular, Statement.Direction_Forward
						);
					for (int j = 0; j < lstSuccs.Count; j++)
					{
						Statement succ = lstSuccs[j];
						FastFixedSetFactory.FastFixedSet<Statement> succlst = lists.GetOrNull(succ);
						if (j == 0)
						{
							domsSuccs.Union(succlst);
						}
						else
						{
							domsSuccs.Intersection(succlst);
						}
					}
					if (!domsSuccs.Contains(stat))
					{
						domsSuccs.Add(stat);
					}
					if (!InterpreterUtil.EqualObjects(domsSuccs, doms))
					{
						Sharpen.Collections.Put(lists, stat, domsSuccs);
						List<Statement> lstPreds = stat.GetNeighbours(StatEdge.Type_Regular, Statement.Direction_Backward
							);
						foreach (Statement pred in lstPreds)
						{
							setFlagNodes.Add(pred);
						}
					}
				}
			}
			while (!setFlagNodes.IsEmpty());
			VBStyleCollection<List<int>, int> ret = new VBStyleCollection<List<int>, int>();
			List<Statement> lstRevPost = container.GetReversePostOrderList();
			// sort order crucial!
			Dictionary<int, int> mapSortOrder = new Dictionary<int, int>();
			for (int i = 0; i < lstRevPost.Count; i++)
			{
				Sharpen.Collections.Put(mapSortOrder, lstRevPost[i].id, i);
			}
			foreach (Statement st in lstStats)
			{
				List<int> lstPosts = new List<int>();
				foreach (Statement stt in lists.GetOrNull(st))
				{
					lstPosts.Add(stt.id);
				}
				lstPosts.Sort(IComparer.Comparing(mapSortOrder));
				if (lstPosts.Count > 1 && lstPosts[0] == st.id)
				{
					lstPosts.Add(lstPosts.RemoveAtReturningValue(0));
				}
				ret.AddWithKey(lstPosts, st.id);
			}
			return ret;
		}

		public static RootStatement ParseGraph(ControlFlowGraph graph)
		{
			RootStatement root = GraphToStatement(graph);
			if (!ProcessStatement(root, new Dictionary<int, HashSet<int>>()))
			{
				//			try {
				//				DotExporter.toDotFile(root.getFirst().getStats().get(13), new File("c:\\Temp\\stat1.dot"));
				//			} catch (Exception ex) {
				//				ex.printStackTrace();
				//			}
				throw new Exception("parsing failure!");
			}
			LabelHelper.LowContinueLabels(root, new HashSet<StatEdge>());
			SequenceHelper.CondenseSequences(root);
			root.BuildMonitorFlags();
			// build synchronized statements
			BuildSynchronized(root);
			return root;
		}

		public static void RemoveSynchronizedHandler(Statement stat)
		{
			foreach (Statement st in stat.GetStats())
			{
				RemoveSynchronizedHandler(st);
			}
			if (stat.type == Statement.Type_Syncronized)
			{
				((SynchronizedStatement)stat).RemoveExc();
			}
		}

		private static void BuildSynchronized(Statement stat)
		{
			foreach (Statement st in stat.GetStats())
			{
				BuildSynchronized(st);
			}
			if (stat.type == Statement.Type_Sequence)
			{
				while (true)
				{
					bool found = false;
					List<Statement> lst = stat.GetStats();
					for (int i = 0; i < lst.Count - 1; i++)
					{
						Statement current = lst[i];
						// basic block
						if (current.IsMonitorEnter())
						{
							Statement next = lst[i + 1];
							Statement nextDirect = next;
							while (next.type == Statement.Type_Sequence)
							{
								next = next.GetFirst();
							}
							if (next.type == Statement.Type_Catchall)
							{
								CatchAllStatement ca = (CatchAllStatement)next;
								if (ca.GetFirst().IsContainsMonitorExit() && ca.GetHandler().IsContainsMonitorExit
									())
								{
									// remove the head block from sequence
									current.RemoveSuccessor(current.GetSuccessorEdges(Statement.Statedge_Direct_All)[
										0]);
									foreach (StatEdge edge in current.GetPredecessorEdges(Statement.Statedge_Direct_All
										))
									{
										current.RemovePredecessor(edge);
										edge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, edge, nextDirect);
										nextDirect.AddPredecessor(edge);
									}
									stat.GetStats().RemoveWithKey(current.id);
									stat.SetFirst(stat.GetStats()[0]);
									// new statement
									SynchronizedStatement sync = new SynchronizedStatement(current, ca.GetFirst(), ca
										.GetHandler());
									sync.SetAllParent();
									foreach (StatEdge edge in new HashSet<StatEdge>(ca.GetLabelEdges()))
									{
										sync.AddLabeledEdge(edge);
									}
									current.AddSuccessor(new StatEdge(StatEdge.Type_Regular, current, ca.GetFirst()));
									ca.GetParent().ReplaceStatement(ca, sync);
									found = true;
									break;
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
		}

		private static bool ProcessStatement(Statement general, Dictionary<int, HashSet<int
			>> mapExtPost)
		{
			if (general.type == Statement.Type_Root)
			{
				Statement stat = general.GetFirst();
				if (stat.type != Statement.Type_General)
				{
					return true;
				}
				else
				{
					bool complete = ProcessStatement(stat, mapExtPost);
					if (complete)
					{
						// replace general purpose statement with simple one
						general.ReplaceStatement(stat, stat.GetFirst());
					}
					return complete;
				}
			}
			bool mapRefreshed = (mapExtPost.Count == 0);
			for (int mapstage = 0; mapstage < 2; mapstage++)
			{
				for (int reducibility = 0; reducibility < 5; reducibility++)
				{
					// FIXME: implement proper node splitting. For now up to 5 nodes in sequence are splitted.
					if (reducibility > 0)
					{
						//					try {
						//						DotExporter.toDotFile(general, new File("c:\\Temp\\stat1.dot"));
						//					} catch(Exception ex) {ex.printStackTrace();}
						// take care of irreducible control flow graphs
						if (IrreducibleCFGDeobfuscator.IsStatementIrreducible(general))
						{
							if (!IrreducibleCFGDeobfuscator.SplitIrreducibleNode(general))
							{
								DecompilerContext.GetLogger().WriteMessage("Irreducible statement cannot be decomposed!"
									, IFernflowerLogger.Severity.Error);
								break;
							}
						}
						else
						{
							if (mapstage == 2 || mapRefreshed)
							{
								// last chance lost
								DecompilerContext.GetLogger().WriteMessage("Statement cannot be decomposed although reducible!"
									, IFernflowerLogger.Severity.Error);
							}
							break;
						}
						//					try {
						//						DotExporter.toDotFile(general, new File("c:\\Temp\\stat1.dot"));
						//					} catch(Exception ex) {ex.printStackTrace();}
						mapExtPost = new Dictionary<int, HashSet<int>>();
						mapRefreshed = true;
					}
					for (int i = 0; i < 2; i++)
					{
						bool forceall = i != 0;
						while (true)
						{
							if (FindSimpleStatements(general, mapExtPost))
							{
								reducibility = 0;
							}
							if (general.type == Statement.Type_Placeholder)
							{
								return true;
							}
							Statement stat = FindGeneralStatement(general, forceall, mapExtPost);
							if (stat != null)
							{
								bool complete = ProcessStatement(stat, general.GetFirst() == stat ? mapExtPost : 
									new Dictionary<int, HashSet<int>>());
								if (complete)
								{
									// replace general purpose statement with simple one
									general.ReplaceStatement(stat, stat.GetFirst());
								}
								else
								{
									return false;
								}
								mapExtPost = new Dictionary<int, HashSet<int>>();
								mapRefreshed = true;
								reducibility = 0;
							}
							else
							{
								break;
							}
						}
					}
				}
				//				try {
				//					DotExporter.toDotFile(general, new File("c:\\Temp\\stat1.dot"));
				//				} catch (Exception ex) {
				//					ex.printStackTrace();
				//				}
				if (mapRefreshed)
				{
					break;
				}
				else
				{
					mapExtPost = new Dictionary<int, HashSet<int>>();
				}
			}
			return false;
		}

		private static Statement FindGeneralStatement(Statement stat, bool forceall, Dictionary
			<int, HashSet<int>> mapExtPost)
		{
			VBStyleCollection<Statement, int> stats = stat.GetStats();
			VBStyleCollection<List<int>, int> vbPost;
			if ((mapExtPost.Count == 0))
			{
				FastExtendedPostdominanceHelper extpost = new FastExtendedPostdominanceHelper();
				Sharpen.Collections.PutAll(mapExtPost, extpost.GetExtendedPostdominators(stat));
			}
			if (forceall)
			{
				vbPost = new VBStyleCollection<List<int>, int>();
				List<Statement> lstAll = stat.GetPostReversePostOrderList();
				foreach (Statement st in lstAll)
				{
					HashSet<int> set = mapExtPost.GetOrNull(st.id);
					if (set != null)
					{
						vbPost.AddWithKey(new List<int>(set), st.id);
					}
				}
				// FIXME: sort order!!
				// tail statements
				HashSet<int> setFirst = mapExtPost.GetOrNull(stat.GetFirst().id);
				if (setFirst != null)
				{
					foreach (int id in setFirst)
					{
						List<int> lst = vbPost.GetWithKey(id);
						if (lst == null)
						{
							vbPost.AddWithKey(lst = new List<int>(), id);
						}
						lst.Add(id);
					}
				}
			}
			else
			{
				vbPost = CalcPostDominators(stat);
			}
			for (int k = 0; k < vbPost.Count; k++)
			{
				int headid = vbPost.GetKey(k);
				List<int> posts = vbPost[k];
				if (!mapExtPost.ContainsKey(headid) && !(posts.Count == 1 && posts[0].Equals(headid
					)))
				{
					continue;
				}
				Statement head = stats.GetWithKey(headid);
				HashSet<int> setExtPosts = mapExtPost.GetOrNull(headid);
				foreach (int postId in posts)
				{
					if (!postId.Equals(headid) && !setExtPosts.Contains(postId))
					{
						continue;
					}
					Statement post = stats.GetWithKey(postId);
					if (post == null)
					{
						// possible in case of an inherited postdominance set
						continue;
					}
					bool same = (post == head);
					HashSet<Statement> setNodes = new HashSet<Statement>();
					HashSet<Statement> setPreds = new HashSet<Statement>();
					// collect statement nodes
					HashSet<Statement> setHandlers = new HashSet<Statement>();
					setHandlers.Add(head);
					while (true)
					{
						bool hdfound = false;
						foreach (Statement handler in setHandlers)
						{
							if (setNodes.Contains(handler))
							{
								continue;
							}
							bool addhd = (setNodes.Count == 0);
							// first handler == head
							if (!addhd)
							{
								List<Statement> hdsupp = handler.GetNeighbours(StatEdge.Type_Exception, Statement
									.Direction_Backward);
								addhd = (setNodes.ContainsAll(hdsupp) && (setNodes.Count > hdsupp.Count || setNodes
									.Count == 1));
							}
							// strict subset
							if (addhd)
							{
								LinkedList<Statement> lstStack = new LinkedList<Statement>();
								lstStack.Add(handler);
								while (!(lstStack.Count == 0))
								{
									Statement st = lstStack.RemoveAtReturningValue(0);
									if (!(setNodes.Contains(st) || (!same && st == post)))
									{
										setNodes.Add(st);
										if (st != head)
										{
											// record predeccessors except for the head
											Sharpen.Collections.AddAll(setPreds, st.GetNeighbours(StatEdge.Type_Regular, Statement
												.Direction_Backward));
										}
										// put successors on the stack
										Sharpen.Collections.AddAll(lstStack, st.GetNeighbours(StatEdge.Type_Regular, Statement
											.Direction_Forward));
										// exception edges
										Sharpen.Collections.AddAll(setHandlers, st.GetNeighbours(StatEdge.Type_Exception, 
											Statement.Direction_Forward));
									}
								}
								hdfound = true;
								setHandlers.Remove(handler);
								break;
							}
						}
						if (!hdfound)
						{
							break;
						}
					}
					// check exception handlers
					setHandlers.Clear();
					foreach (Statement st in setNodes)
					{
						Sharpen.Collections.AddAll(setHandlers, st.GetNeighbours(StatEdge.Type_Exception, 
							Statement.Direction_Forward));
					}
					setHandlers.RemoveAll(setNodes);
					bool excok = true;
					foreach (Statement handler in setHandlers)
					{
						if (!handler.GetNeighbours(StatEdge.Type_Exception, Statement.Direction_Backward)
							.ContainsAll(setNodes))
						{
							excok = false;
							break;
						}
					}
					// build statement and return
					if (excok)
					{
						Statement res;
						setPreds.RemoveAll(setNodes);
						if (setPreds.Count == 0)
						{
							if ((setNodes.Count > 1 || head.GetNeighbours(StatEdge.Type_Regular, Statement.Direction_Backward
								).Contains(head)) && setNodes.Count < stats.Count)
							{
								if (CheckSynchronizedCompleteness(setNodes))
								{
									res = new GeneralStatement(head, setNodes, same ? null : post);
									stat.CollapseNodesToStatement(res);
									return res;
								}
							}
						}
					}
				}
			}
			return null;
		}

		private static bool CheckSynchronizedCompleteness(HashSet<Statement> setNodes)
		{
			// check exit nodes
			foreach (Statement stat in setNodes)
			{
				if (stat.IsMonitorEnter())
				{
					List<StatEdge> lstSuccs = stat.GetSuccessorEdges(Statement.Statedge_Direct_All);
					if (lstSuccs.Count != 1 || lstSuccs[0].GetType() != StatEdge.Type_Regular)
					{
						return false;
					}
					if (!setNodes.Contains(lstSuccs[0].GetDestination()))
					{
						return false;
					}
				}
			}
			return true;
		}

		private static bool FindSimpleStatements(Statement stat, Dictionary<int, HashSet<
			int>> mapExtPost)
		{
			bool found;
			bool success = false;
			do
			{
				found = false;
				List<Statement> lstStats = stat.GetPostReversePostOrderList();
				foreach (Statement st in lstStats)
				{
					Statement result = DetectStatement(st);
					if (result != null)
					{
						if (stat.type == Statement.Type_General && result.GetFirst() == stat.GetFirst() &&
							 stat.GetStats().Count == result.GetStats().Count)
						{
							// mark general statement
							stat.type = Statement.Type_Placeholder;
						}
						stat.CollapseNodesToStatement(result);
						// update the postdominator map
						if (!(mapExtPost.Count == 0))
						{
							HashSet<int> setOldNodes = new HashSet<int>();
							foreach (Statement old in result.GetStats())
							{
								setOldNodes.Add(old.id);
							}
							int newid = result.id;
							foreach (int key in new List<int>(mapExtPost.Keys))
							{
								HashSet<int> set = mapExtPost.GetOrNull(key);
								int oldsize = set.Count;
								set.RemoveAll(setOldNodes);
								if (setOldNodes.Contains(key))
								{
									Sharpen.Collections.AddAll(mapExtPost.ComputeIfAbsent(newid, (int k) => new HashSet
										<int>()), set);
									Sharpen.Collections.Remove(mapExtPost, key);
								}
								else if (set.Count < oldsize)
								{
									set.Add(newid);
								}
							}
						}
						found = true;
						break;
					}
				}
				if (found)
				{
					success = true;
				}
			}
			while (found);
			return success;
		}

		private static Statement DetectStatement(Statement head)
		{
			Statement res;
			if ((res = DoStatement.IsHead(head)) != null)
			{
				return res;
			}
			if ((res = SwitchStatement.IsHead(head)) != null)
			{
				return res;
			}
			if ((res = IfStatement.IsHead(head)) != null)
			{
				return res;
			}
			// synchronized statements will be identified later
			// right now they are recognized as catchall
			if ((res = SequenceStatement.IsHead2Block(head)) != null)
			{
				return res;
			}
			if ((res = CatchStatement.IsHead(head)) != null)
			{
				return res;
			}
			if ((res = CatchAllStatement.IsHead(head)) != null)
			{
				return res;
			}
			return null;
		}
	}
}
