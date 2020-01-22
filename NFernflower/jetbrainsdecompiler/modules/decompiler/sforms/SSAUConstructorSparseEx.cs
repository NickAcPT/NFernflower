// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Sforms
{
	public class SSAUConstructorSparseEx
	{
		private readonly Dictionary<string, SFormsFastMapDirect> inVarVersions = new Dictionary
			<string, SFormsFastMapDirect>();

		private readonly Dictionary<string, SFormsFastMapDirect> outVarVersions = new Dictionary
			<string, SFormsFastMapDirect>();

		private readonly Dictionary<string, SFormsFastMapDirect> outNegVarVersions = new 
			Dictionary<string, SFormsFastMapDirect>();

		private readonly Dictionary<string, SFormsFastMapDirect> extraVarVersions = new Dictionary
			<string, SFormsFastMapDirect>();

		private readonly Dictionary<int, int> lastversion = new Dictionary<int, int>();

		private readonly Dictionary<VarVersionPair, int> mapVersionFirstRange = new Dictionary
			<VarVersionPair, int>();

		private readonly Dictionary<VarVersionPair, VarVersionPair> phantomppnodes = new 
			Dictionary<VarVersionPair, VarVersionPair>();

		private readonly Dictionary<string, Dictionary<VarVersionPair, VarVersionPair>> phantomexitnodes
			 = new Dictionary<string, Dictionary<VarVersionPair, VarVersionPair>>();

		private readonly VarVersionsGraph ssuversions = new VarVersionsGraph();

		private readonly Dictionary<int, int> mapFieldVars = new Dictionary<int, int>();

		private int fieldvarcounter = -1;

		private FastSparseSetFactory<int> factory;

		// node id, var, version
		//private HashMap<String, HashMap<Integer, FastSet<Integer>>> inVarVersions = new HashMap<String, HashMap<Integer, FastSet<Integer>>>();
		// node id, var, version (direct branch)
		//private HashMap<String, HashMap<Integer, FastSet<Integer>>> outVarVersions = new HashMap<String, HashMap<Integer, FastSet<Integer>>>();
		// node id, var, version (negative branch)
		//private HashMap<String, HashMap<Integer, FastSet<Integer>>> outNegVarVersions = new HashMap<String, HashMap<Integer, FastSet<Integer>>>();
		// node id, var, version
		//private HashMap<String, HashMap<Integer, FastSet<Integer>>> extraVarVersions = new HashMap<String, HashMap<Integer, FastSet<Integer>>>();
		// var, version
		// version, protected ranges (catch, finally)
		// version, version
		// ++ and --
		// node.id, version, version
		// finally exits
		// versions memory dependencies
		// field access vars (exprent id, var id)
		// field access counter
		// set factory
		public virtual void SplitVariables(RootStatement root, StructMethod mt)
		{
			FlattenStatementsHelper flatthelper = new FlattenStatementsHelper();
			DirectGraph dgraph = flatthelper.BuildDirectGraph(root);
			HashSet<int> setInit = new HashSet<int>();
			for (int i = 0; i < 64; i++)
			{
				setInit.Add(i);
			}
			factory = new FastSparseSetFactory<int>(setInit);
			Sharpen.Collections.Put(extraVarVersions, dgraph.first.id, CreateFirstMap(mt, root
				));
			SetCatchMaps(root, dgraph, flatthelper);
			//		try {
			//			DotExporter.toDotFile(dgraph, new File("c:\\Temp\\gr12_my.dot"));
			//		} catch(Exception ex) {ex.printStackTrace();}
			HashSet<string> updated = new HashSet<string>();
			do
			{
				//			System.out.println("~~~~~~~~~~~~~ \r\n"+root.toJava());
				SsaStatements(dgraph, updated, false);
			}
			while (!(updated.Count == 0));
			//			System.out.println("~~~~~~~~~~~~~ \r\n"+root.toJava());
			SsaStatements(dgraph, updated, true);
			ssuversions.InitDominators();
		}

		private void SsaStatements(DirectGraph dgraph, HashSet<string> updated, bool calcLiveVars
			)
		{
			foreach (DirectNode node in dgraph.nodes)
			{
				updated.Remove(node.id);
				MergeInVarMaps(node, dgraph);
				SFormsFastMapDirect varmap = new SFormsFastMapDirect(inVarVersions.GetOrNull(node
					.id));
				SFormsFastMapDirect[] varmaparr = new SFormsFastMapDirect[] { varmap, null };
				if (node.exprents != null)
				{
					foreach (Exprent expr in node.exprents)
					{
						ProcessExprent(expr, varmaparr, node.statement, calcLiveVars);
					}
				}
				if (varmaparr[1] == null)
				{
					varmaparr[1] = varmaparr[0];
				}
				// quick solution: 'dummy' field variables should not cross basic block borders (otherwise problems e.g. with finally loops - usage without assignment in a loop)
				// For the full solution consider adding a dummy assignment at the entry point of the method
				bool allow_field_propagation = (node.succs.Count == 0) || (node.succs.Count == 1 
					&& node.succs[0].preds.Count == 1);
				if (!allow_field_propagation && varmaparr[0] != null)
				{
					varmaparr[0].RemoveAllFields();
					varmaparr[1].RemoveAllFields();
				}
				bool this_updated = !MapsEqual(varmaparr[0], outVarVersions.GetOrNull(node.id)) ||
					 (outNegVarVersions.ContainsKey(node.id) && !MapsEqual(varmaparr[1], outNegVarVersions
					.GetOrNull(node.id)));
				if (this_updated)
				{
					Sharpen.Collections.Put(outVarVersions, node.id, varmaparr[0]);
					if (dgraph.mapNegIfBranch.ContainsKey(node.id))
					{
						Sharpen.Collections.Put(outNegVarVersions, node.id, varmaparr[1]);
					}
					foreach (DirectNode nd in node.succs)
					{
						updated.Add(nd.id);
					}
				}
			}
		}

		private void ProcessExprent(Exprent expr, SFormsFastMapDirect[] varmaparr, Statement
			 stat, bool calcLiveVars)
		{
			if (expr == null)
			{
				return;
			}
			VarExprent varassign = null;
			bool finished = false;
			switch (expr.type)
			{
				case Exprent.Exprent_Assignment:
				{
					AssignmentExprent assexpr = (AssignmentExprent)expr;
					if (assexpr.GetCondType() == AssignmentExprent.Condition_None)
					{
						Exprent dest = assexpr.GetLeft();
						if (dest.type == Exprent.Exprent_Var)
						{
							varassign = (VarExprent)dest;
						}
					}
					break;
				}

				case Exprent.Exprent_Function:
				{
					FunctionExprent func = (FunctionExprent)expr;
					switch (func.GetFuncType())
					{
						case FunctionExprent.Function_Iif:
						{
							ProcessExprent(func.GetLstOperands()[0], varmaparr, stat, calcLiveVars);
							SFormsFastMapDirect varmapFalse;
							if (varmaparr[1] == null)
							{
								varmapFalse = new SFormsFastMapDirect(varmaparr[0]);
							}
							else
							{
								varmapFalse = varmaparr[1];
								varmaparr[1] = null;
							}
							ProcessExprent(func.GetLstOperands()[1], varmaparr, stat, calcLiveVars);
							SFormsFastMapDirect[] varmaparrNeg = new SFormsFastMapDirect[] { varmapFalse, null
								 };
							ProcessExprent(func.GetLstOperands()[2], varmaparrNeg, stat, calcLiveVars);
							MergeMaps(varmaparr[0], varmaparrNeg[0]);
							varmaparr[1] = null;
							finished = true;
							break;
						}

						case FunctionExprent.Function_Cadd:
						{
							ProcessExprent(func.GetLstOperands()[0], varmaparr, stat, calcLiveVars);
							SFormsFastMapDirect[] varmaparrAnd = new SFormsFastMapDirect[] { new SFormsFastMapDirect
								(varmaparr[0]), null };
							ProcessExprent(func.GetLstOperands()[1], varmaparrAnd, stat, calcLiveVars);
							// false map
							varmaparr[1] = MergeMaps(varmaparr[varmaparr[1] == null ? 0 : 1], varmaparrAnd[varmaparrAnd
								[1] == null ? 0 : 1]);
							// true map
							varmaparr[0] = varmaparrAnd[0];
							finished = true;
							break;
						}

						case FunctionExprent.Function_Cor:
						{
							ProcessExprent(func.GetLstOperands()[0], varmaparr, stat, calcLiveVars);
							SFormsFastMapDirect[] varmaparrOr = new SFormsFastMapDirect[] { new SFormsFastMapDirect
								(varmaparr[varmaparr[1] == null ? 0 : 1]), null };
							ProcessExprent(func.GetLstOperands()[1], varmaparrOr, stat, calcLiveVars);
							// false map
							varmaparr[1] = varmaparrOr[varmaparrOr[1] == null ? 0 : 1];
							// true map
							varmaparr[0] = MergeMaps(varmaparr[0], varmaparrOr[0]);
							finished = true;
							break;
						}
					}
					break;
				}
			}
			if (!finished)
			{
				List<Exprent> lst = expr.GetAllExprents();
				lst.Remove(varassign);
				foreach (Exprent ex in lst)
				{
					ProcessExprent(ex, varmaparr, stat, calcLiveVars);
				}
			}
			SFormsFastMapDirect varmap = varmaparr[0];
			// field access
			if (expr.type == Exprent.Exprent_Field)
			{
				int index;
				if (mapFieldVars.ContainsKey(expr.id))
				{
					index = mapFieldVars.GetOrNullable(expr.id);
				}
				else
				{
					index = fieldvarcounter--;
					Sharpen.Collections.Put(mapFieldVars, expr.id, index);
					// ssu graph
					ssuversions.CreateNode(new VarVersionPair(index, 1));
				}
				SetCurrentVar(varmap, index, 1);
			}
			else if (expr.type == Exprent.Exprent_Invocation || (expr.type == Exprent.Exprent_Assignment
				 && ((AssignmentExprent)expr).GetLeft().type == Exprent.Exprent_Field) || (expr.
				type == Exprent.Exprent_New && ((NewExprent)expr).GetNewType().type == ICodeConstants
				.Type_Object) || expr.type == Exprent.Exprent_Function)
			{
				bool ismmpp = true;
				if (expr.type == Exprent.Exprent_Function)
				{
					ismmpp = false;
					FunctionExprent fexpr = (FunctionExprent)expr;
					if (fexpr.GetFuncType() >= FunctionExprent.Function_Imm && fexpr.GetFuncType() <=
						 FunctionExprent.Function_Ppi)
					{
						if (fexpr.GetLstOperands()[0].type == Exprent.Exprent_Field)
						{
							ismmpp = true;
						}
					}
				}
				if (ismmpp)
				{
					varmap.RemoveAllFields();
				}
			}
			if (varassign != null)
			{
				int varindex = varassign.GetIndex();
				if (varassign.GetVersion() == 0)
				{
					// get next version
					int nextver = GetNextFreeVersion(varindex, stat);
					// set version
					varassign.SetVersion(nextver);
					// ssu graph
					ssuversions.CreateNode(new VarVersionPair(varindex, nextver));
					SetCurrentVar(varmap, varindex, nextver);
				}
				else
				{
					if (calcLiveVars)
					{
						VarMapToGraph(new VarVersionPair(varindex, varassign.GetVersion()), varmap);
					}
					SetCurrentVar(varmap, varindex, varassign.GetVersion());
				}
			}
			else if (expr.type == Exprent.Exprent_Function)
			{
				// MM or PP function
				FunctionExprent func_1 = (FunctionExprent)expr;
				switch (func_1.GetFuncType())
				{
					case FunctionExprent.Function_Imm:
					case FunctionExprent.Function_Mmi:
					case FunctionExprent.Function_Ipp:
					case FunctionExprent.Function_Ppi:
					{
						if (func_1.GetLstOperands()[0].type == Exprent.Exprent_Var)
						{
							VarExprent var = (VarExprent)func_1.GetLstOperands()[0];
							int varindex = var.GetIndex();
							VarVersionPair varpaar = new VarVersionPair(varindex, var.GetVersion());
							// ssu graph
							VarVersionPair phantomver = phantomppnodes.GetOrNull(varpaar);
							if (phantomver == null)
							{
								// get next version
								int nextver = GetNextFreeVersion(varindex, null);
								phantomver = new VarVersionPair(varindex, nextver);
								//ssuversions.createOrGetNode(phantomver);
								ssuversions.CreateNode(phantomver);
								VarVersionNode vernode = ssuversions.nodes.GetWithKey(varpaar);
								FastSparseSetFactory.FastSparseSet<int> vers = factory.SpawnEmptySet();
								if (vernode.preds.Count == 1)
								{
									vers.Add(vernode.preds.GetEnumerator().Current.source.version);
								}
								else
								{
									foreach (VarVersionEdge edge in vernode.preds)
									{
										vers.Add(edge.source.preds.GetEnumerator().Current.source.version);
									}
								}
								vers.Add(nextver);
								CreateOrUpdatePhiNode(varpaar, vers, stat);
								Sharpen.Collections.Put(phantomppnodes, varpaar, phantomver);
							}
							if (calcLiveVars)
							{
								VarMapToGraph(varpaar, varmap);
							}
							SetCurrentVar(varmap, varindex, var.GetVersion());
						}
						break;
					}
				}
			}
			else if (expr.type == Exprent.Exprent_Var)
			{
				VarExprent vardest = (VarExprent)expr;
				int varindex = vardest.GetIndex();
				int current_vers = vardest.GetVersion();
				FastSparseSetFactory.FastSparseSet<int> vers = varmap.Get(varindex);
				int cardinality = vers.GetCardinality();
				if (cardinality == 1)
				{
					// size == 1
					if (current_vers != 0)
					{
						if (calcLiveVars)
						{
							VarMapToGraph(new VarVersionPair(varindex, current_vers), varmap);
						}
						SetCurrentVar(varmap, varindex, current_vers);
					}
					else
					{
						// split last version
						int usever = GetNextFreeVersion(varindex, stat);
						// set version
						vardest.SetVersion(usever);
						SetCurrentVar(varmap, varindex, usever);
						// ssu graph
						int lastver = vers.GetEnumerator().Current;
						VarVersionNode prenode = ssuversions.nodes.GetWithKey(new VarVersionPair(varindex
							, lastver));
						VarVersionNode usenode = ssuversions.CreateNode(new VarVersionPair(varindex, usever
							));
						VarVersionEdge edge = new VarVersionEdge(VarVersionEdge.Edge_General, prenode, usenode
							);
						prenode.AddSuccessor(edge);
						usenode.AddPredecessor(edge);
					}
				}
				else if (cardinality == 2)
				{
					// size > 1
					if (current_vers != 0)
					{
						if (calcLiveVars)
						{
							VarMapToGraph(new VarVersionPair(varindex, current_vers), varmap);
						}
						SetCurrentVar(varmap, varindex, current_vers);
					}
					else
					{
						// split version
						int usever = GetNextFreeVersion(varindex, stat);
						// set version
						vardest.SetVersion(usever);
						// ssu node
						ssuversions.CreateNode(new VarVersionPair(varindex, usever));
						SetCurrentVar(varmap, varindex, usever);
						current_vers = usever;
					}
					CreateOrUpdatePhiNode(new VarVersionPair(varindex, current_vers), vers, stat);
				}
			}
		}

		// vers.size() == 0 means uninitialized variable, which is impossible
		private void CreateOrUpdatePhiNode(VarVersionPair phivar, FastSparseSetFactory.FastSparseSet
			<int> vers, Statement stat)
		{
			FastSparseSetFactory.FastSparseSet<int> versCopy = vers.GetCopy();
			HashSet<int> phiVers = new HashSet<int>();
			// take into account the corresponding mm/pp node if existing
			int ppvers = phantomppnodes.ContainsKey(phivar) ? phantomppnodes.GetOrNull(phivar
				).version : -1;
			// ssu graph
			VarVersionNode phinode = ssuversions.nodes.GetWithKey(phivar);
			List<VarVersionEdge> lstPreds = new List<VarVersionEdge>(phinode.preds);
			if (lstPreds.Count == 1)
			{
				// not yet a phi node
				VarVersionEdge edge = lstPreds[0];
				edge.source.RemoveSuccessor(edge);
				phinode.RemovePredecessor(edge);
			}
			else
			{
				foreach (VarVersionEdge edge in lstPreds)
				{
					int verssrc = edge.source.preds.GetEnumerator().Current.source.version;
					if (!vers.Contains(verssrc) && verssrc != ppvers)
					{
						edge.source.RemoveSuccessor(edge);
						phinode.RemovePredecessor(edge);
					}
					else
					{
						versCopy.Remove(verssrc);
						phiVers.Add(verssrc);
					}
				}
			}
			List<VarVersionNode> colnodes = new List<VarVersionNode>();
			List<VarVersionPair> colpaars = new List<VarVersionPair>();
			foreach (int ver in versCopy)
			{
				VarVersionNode prenode = ssuversions.nodes.GetWithKey(new VarVersionPair(phivar.var
					, ver));
				int tempver = GetNextFreeVersion(phivar.var, stat);
				VarVersionNode tempnode = new VarVersionNode(phivar.var, tempver);
				colnodes.Add(tempnode);
				colpaars.Add(new VarVersionPair(phivar.var, tempver));
				VarVersionEdge edge = new VarVersionEdge(VarVersionEdge.Edge_General, prenode, tempnode
					);
				prenode.AddSuccessor(edge);
				tempnode.AddPredecessor(edge);
				edge = new VarVersionEdge(VarVersionEdge.Edge_General, tempnode, phinode);
				tempnode.AddSuccessor(edge);
				phinode.AddPredecessor(edge);
				phiVers.Add(tempver);
			}
			ssuversions.AddNodes(colnodes, colpaars);
		}

		private void VarMapToGraph(VarVersionPair varpaar, SFormsFastMapDirect varmap)
		{
			VBStyleCollection<VarVersionNode, VarVersionPair> nodes = ssuversions.nodes;
			VarVersionNode node = nodes.GetWithKey(varpaar);
			node.live = new SFormsFastMapDirect(varmap);
		}

		private int GetNextFreeVersion(int var, Statement stat)
		{
			int? nextver = lastversion.GetOrNullable(var);
			if (nextver == null)
			{
				nextver.Value = 1;
			}
			else
			{
				nextver.Value++;
			}
			Sharpen.Collections.Put(lastversion, var, nextver.Value);
			// save the first protected range, containing current statement
			if (stat != null)
			{
				// null iff phantom version
				int firstRangeId = GetFirstProtectedRange(stat);
				if (firstRangeId != null)
				{
					Sharpen.Collections.Put(mapVersionFirstRange, new VarVersionPair(var, nextver.Value
						), firstRangeId);
				}
			}
			return nextver.Value;
		}

		private void MergeInVarMaps(DirectNode node, DirectGraph dgraph)
		{
			SFormsFastMapDirect mapNew = new SFormsFastMapDirect();
			foreach (DirectNode pred in node.preds)
			{
				SFormsFastMapDirect mapOut = GetFilteredOutMap(node.id, pred.id, dgraph, node.id);
				if (mapNew.IsEmpty())
				{
					mapNew = mapOut.GetCopy();
				}
				else
				{
					MergeMaps(mapNew, mapOut);
				}
			}
			if (extraVarVersions.ContainsKey(node.id))
			{
				SFormsFastMapDirect mapExtra = extraVarVersions.GetOrNull(node.id);
				if (mapNew.IsEmpty())
				{
					mapNew = mapExtra.GetCopy();
				}
				else
				{
					MergeMaps(mapNew, mapExtra);
				}
			}
			Sharpen.Collections.Put(inVarVersions, node.id, mapNew);
		}

		private SFormsFastMapDirect GetFilteredOutMap(string nodeid, string predid, DirectGraph
			 dgraph, string destid)
		{
			SFormsFastMapDirect mapNew = new SFormsFastMapDirect();
			bool isFinallyExit = dgraph.mapShortRangeFinallyPaths.ContainsKey(predid);
			if (nodeid.Equals(dgraph.mapNegIfBranch.GetOrNull(predid)))
			{
				if (outNegVarVersions.ContainsKey(predid))
				{
					mapNew = outNegVarVersions.GetOrNull(predid).GetCopy();
				}
			}
			else if (outVarVersions.ContainsKey(predid))
			{
				mapNew = outVarVersions.GetOrNull(predid).GetCopy();
			}
			if (isFinallyExit)
			{
				SFormsFastMapDirect mapNewTemp = mapNew.GetCopy();
				SFormsFastMapDirect mapTrueSource = new SFormsFastMapDirect();
				string exceptionDest = dgraph.mapFinallyMonitorExceptionPathExits.GetOrNull(predid
					);
				bool isExceptionMonitorExit = (exceptionDest != null && !nodeid.Equals(exceptionDest
					));
				HashSet<string> setLongPathWrapper = new HashSet<string>();
				foreach (List<FlattenStatementsHelper.FinallyPathWrapper> lstwrapper in dgraph.mapLongRangeFinallyPaths
					.Values)
				{
					foreach (FlattenStatementsHelper.FinallyPathWrapper finwraplong in lstwrapper)
					{
						setLongPathWrapper.Add(finwraplong.destination + "##" + finwraplong.source);
					}
				}
				foreach (FlattenStatementsHelper.FinallyPathWrapper finwrap in dgraph.mapShortRangeFinallyPaths
					.GetOrNull(predid))
				{
					SFormsFastMapDirect map;
					bool recFinally = dgraph.mapShortRangeFinallyPaths.ContainsKey(finwrap.source);
					if (recFinally)
					{
						// recursion
						map = GetFilteredOutMap(finwrap.entry, finwrap.source, dgraph, destid);
					}
					else if (finwrap.entry.Equals(dgraph.mapNegIfBranch.GetOrNull(finwrap.source)))
					{
						map = outNegVarVersions.GetOrNull(finwrap.source);
					}
					else
					{
						map = outVarVersions.GetOrNull(finwrap.source);
					}
					// false path?
					bool isFalsePath;
					if (recFinally)
					{
						isFalsePath = !finwrap.destination.Equals(nodeid);
					}
					else
					{
						isFalsePath = !setLongPathWrapper.Contains(destid + "##" + finwrap.source);
					}
					if (isFalsePath)
					{
						mapNewTemp.Complement(map);
					}
					else if (mapTrueSource.IsEmpty())
					{
						if (map != null)
						{
							mapTrueSource = map.GetCopy();
						}
					}
					else
					{
						MergeMaps(mapTrueSource, map);
					}
				}
				if (isExceptionMonitorExit)
				{
					mapNew = mapTrueSource;
				}
				else
				{
					mapNewTemp.Union(mapTrueSource);
					mapNew.Intersection(mapNewTemp);
					if (!mapTrueSource.IsEmpty() && !mapNew.IsEmpty())
					{
						// FIXME: what for??
						// replace phi versions with corresponding phantom ones
						Dictionary<VarVersionPair, VarVersionPair> mapPhantom = phantomexitnodes.GetOrNull
							(predid);
						if (mapPhantom == null)
						{
							mapPhantom = new Dictionary<VarVersionPair, VarVersionPair>();
						}
						SFormsFastMapDirect mapExitVar = mapNew.GetCopy();
						mapExitVar.Complement(mapTrueSource);
						foreach (KeyValuePair<int, FastSparseSetFactory.FastSparseSet<int>> ent in mapExitVar
							.EntryList())
						{
							foreach (int version in ent.Value)
							{
								int varindex = ent.Key;
								VarVersionPair exitvar = new VarVersionPair(varindex, version);
								FastSparseSetFactory.FastSparseSet<int> newSet = mapNew.Get(varindex);
								// remove the actual exit version
								newSet.Remove(version);
								// get or create phantom version
								VarVersionPair phantomvar = mapPhantom.GetOrNull(exitvar);
								if (phantomvar == null)
								{
									int newversion = GetNextFreeVersion(exitvar.var, null);
									phantomvar = new VarVersionPair(exitvar.var, newversion);
									VarVersionNode exitnode = ssuversions.nodes.GetWithKey(exitvar);
									VarVersionNode phantomnode = ssuversions.CreateNode(phantomvar);
									phantomnode.flags |= VarVersionNode.Flag_Phantom_Finexit;
									VarVersionEdge edge = new VarVersionEdge(VarVersionEdge.Edge_Phantom, exitnode, phantomnode
										);
									exitnode.AddSuccessor(edge);
									phantomnode.AddPredecessor(edge);
									Sharpen.Collections.Put(mapPhantom, exitvar, phantomvar);
								}
								// add phantom version
								newSet.Add(phantomvar.version);
							}
						}
						if (!(mapPhantom.Count == 0))
						{
							Sharpen.Collections.Put(phantomexitnodes, predid, mapPhantom);
						}
					}
				}
			}
			return mapNew;
		}

		private static SFormsFastMapDirect MergeMaps(SFormsFastMapDirect mapTo, SFormsFastMapDirect
			 map2)
		{
			if (map2 != null && !map2.IsEmpty())
			{
				mapTo.Union(map2);
			}
			return mapTo;
		}

		private static bool MapsEqual(SFormsFastMapDirect map1, SFormsFastMapDirect map2)
		{
			if (map1 == null)
			{
				return map2 == null;
			}
			else if (map2 == null)
			{
				return false;
			}
			if (map1.Size() != map2.Size())
			{
				return false;
			}
			foreach (KeyValuePair<int, FastSparseSetFactory.FastSparseSet<int>> ent2 in map2.
				EntryList())
			{
				if (!InterpreterUtil.EqualObjects(map1.Get(ent2.Key), ent2.Value))
				{
					return false;
				}
			}
			return true;
		}

		private void SetCurrentVar(SFormsFastMapDirect varmap, int var, int vers)
		{
			FastSparseSetFactory.FastSparseSet<int> set = factory.SpawnEmptySet();
			set.Add(vers);
			varmap.Put(var, set);
		}

		private void SetCatchMaps(Statement stat, DirectGraph dgraph, FlattenStatementsHelper
			 flatthelper)
		{
			SFormsFastMapDirect map;
			switch (stat.type)
			{
				case Statement.Type_Catchall:
				case Statement.Type_Trycatch:
				{
					List<VarExprent> lstVars;
					if (stat.type == Statement.Type_Catchall)
					{
						lstVars = ((CatchAllStatement)stat).GetVars();
					}
					else
					{
						lstVars = ((CatchStatement)stat).GetVars();
					}
					for (int i = 1; i < stat.GetStats().Count; i++)
					{
						int varindex = lstVars[i - 1].GetIndex();
						int version = GetNextFreeVersion(varindex, stat);
						// == 1
						map = new SFormsFastMapDirect();
						SetCurrentVar(map, varindex, version);
						Sharpen.Collections.Put(extraVarVersions, dgraph.nodes.GetWithKey(flatthelper.GetMapDestinationNodes
							().GetOrNull(stat.GetStats()[i].id)[0]).id, map);
						//ssuversions.createOrGetNode(new VarVersionPair(varindex, version));
						ssuversions.CreateNode(new VarVersionPair(varindex, version));
					}
					break;
				}
			}
			foreach (Statement st in stat.GetStats())
			{
				SetCatchMaps(st, dgraph, flatthelper);
			}
		}

		private SFormsFastMapDirect CreateFirstMap(StructMethod mt, RootStatement root)
		{
			bool thisvar = !mt.HasModifier(ICodeConstants.Acc_Static);
			MethodDescriptor md = MethodDescriptor.ParseDescriptor(mt.GetDescriptor());
			int paramcount = md.@params.Length + (thisvar ? 1 : 0);
			int varindex = 0;
			SFormsFastMapDirect map = new SFormsFastMapDirect();
			for (int i = 0; i < paramcount; i++)
			{
				int version = GetNextFreeVersion(varindex, root);
				// == 1
				FastSparseSetFactory.FastSparseSet<int> set = factory.SpawnEmptySet();
				set.Add(version);
				map.Put(varindex, set);
				ssuversions.CreateNode(new VarVersionPair(varindex, version));
				if (thisvar)
				{
					if (i == 0)
					{
						varindex++;
					}
					else
					{
						varindex += md.@params[i - 1].stackSize;
					}
				}
				else
				{
					varindex += md.@params[i].stackSize;
				}
			}
			return map;
		}

		private static int GetFirstProtectedRange(Statement stat)
		{
			while (true)
			{
				Statement parent = stat.GetParent();
				if (parent == null)
				{
					break;
				}
				if (parent.type == Statement.Type_Catchall || parent.type == Statement.Type_Trycatch)
				{
					if (parent.GetFirst() == stat)
					{
						return parent.id;
					}
				}
				else if (parent.type == Statement.Type_Syncronized)
				{
					if (((SynchronizedStatement)parent).GetBody() == stat)
					{
						return parent.id;
					}
				}
				stat = parent;
			}
			return null;
		}

		public virtual VarVersionsGraph GetSsuversions()
		{
			return ssuversions;
		}

		public virtual SFormsFastMapDirect GetLiveVarVersionsMap(VarVersionPair varpaar)
		{
			VarVersionNode node = ssuversions.nodes.GetWithKey(varpaar);
			if (node != null)
			{
				return node.live;
			}
			return null;
		}

		public virtual Dictionary<VarVersionPair, int> GetMapVersionFirstRange()
		{
			return mapVersionFirstRange;
		}

		public virtual Dictionary<int, int> GetMapFieldVars()
		{
			return mapFieldVars;
		}
	}
}
