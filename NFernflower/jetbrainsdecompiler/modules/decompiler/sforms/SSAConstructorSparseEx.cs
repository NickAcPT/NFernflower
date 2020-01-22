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
	public class SSAConstructorSparseEx
	{
		private readonly Dictionary<string, SFormsFastMapDirect> inVarVersions = new Dictionary
			<string, SFormsFastMapDirect>();

		private readonly Dictionary<string, SFormsFastMapDirect> outVarVersions = new Dictionary
			<string, SFormsFastMapDirect>();

		private readonly Dictionary<string, SFormsFastMapDirect> outNegVarVersions = new 
			Dictionary<string, SFormsFastMapDirect>();

		private readonly Dictionary<string, SFormsFastMapDirect> extraVarVersions = new Dictionary
			<string, SFormsFastMapDirect>();

		private readonly Dictionary<VarVersionPair, FastSparseSetFactory<int>.FastSparseSet<int
			>> phi = new Dictionary<VarVersionPair, FastSparseSetFactory<int>.FastSparseSet<int>>
			();

		private readonly Dictionary<int, int> lastversion = new Dictionary<int, int>();

		private FastSparseSetFactory<int> factory;

		// node id, var, version
		// node id, var, version (direct branch)
		// node id, var, version (negative branch)
		// node id, var, version
		// (var, version), version
		// var, version
		// set factory
		public virtual void SplitVariables(RootStatement root, StructMethod mt)
		{
			FlattenStatementsHelper flatthelper = new FlattenStatementsHelper();
			DirectGraph dgraph = flatthelper.BuildDirectGraph(root);
			// try {
			// DotExporter.toDotFile(dgraph, new File("c:\\Temp\\gr12_my.dot"));
			// } catch(Exception ex) {ex.printStackTrace();}
			HashSet<int> setInit = new HashSet<int>();
			for (int i = 0; i < 64; i++)
			{
				setInit.Add(i);
			}
			factory = new FastSparseSetFactory<int>(setInit);
			SFormsFastMapDirect firstmap = CreateFirstMap(mt);
			Sharpen.Collections.Put(extraVarVersions, dgraph.first.id, firstmap);
			SetCatchMaps(root, dgraph, flatthelper);
			HashSet<string> updated = new HashSet<string>();
			do
			{
				// System.out.println("~~~~~~~~~~~~~ \r\n"+root.toJava());
				SsaStatements(dgraph, updated);
			}
			while (!(updated.Count == 0));
		}

		// System.out.println("~~~~~~~~~~~~~ \r\n"+root.toJava());
		private void SsaStatements(DirectGraph dgraph, HashSet<string> updated)
		{
			// try {
			// DotExporter.toDotFile(dgraph, new File("c:\\Temp\\gr1_my.dot"));
			// } catch(Exception ex) {ex.printStackTrace();}
			foreach (DirectNode node in dgraph.nodes)
			{
				//			if (node.id.endsWith("_inc")) {
				//				System.out.println();
				//
				//				try {
				//					DotExporter.toDotFile(dgraph, new File("c:\\Temp\\gr1_my.dot"));
				//				} catch (Exception ex) {
				//					ex.printStackTrace();
				//				}
				//			}
				updated.Remove(node.id);
				MergeInVarMaps(node, dgraph);
				SFormsFastMapDirect varmap = inVarVersions.GetOrNull(node.id);
				varmap = new SFormsFastMapDirect(varmap);
				SFormsFastMapDirect[] varmaparr = new SFormsFastMapDirect[] { varmap, null };
				if (node.exprents != null)
				{
					foreach (Exprent expr in node.exprents)
					{
						ProcessExprent(expr, varmaparr);
					}
				}
				if (varmaparr[1] == null)
				{
					varmaparr[1] = varmaparr[0];
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

		private void ProcessExprent(Exprent expr, SFormsFastMapDirect[] varmaparr)
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
							ProcessExprent(func.GetLstOperands()[0], varmaparr);
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
							ProcessExprent(func.GetLstOperands()[1], varmaparr);
							SFormsFastMapDirect[] varmaparrNeg = new SFormsFastMapDirect[] { varmapFalse, null
								 };
							ProcessExprent(func.GetLstOperands()[2], varmaparrNeg);
							MergeMaps(varmaparr[0], varmaparrNeg[0]);
							varmaparr[1] = null;
							finished = true;
							break;
						}

						case FunctionExprent.Function_Cadd:
						{
							ProcessExprent(func.GetLstOperands()[0], varmaparr);
							SFormsFastMapDirect[] varmaparrAnd = new SFormsFastMapDirect[] { new SFormsFastMapDirect
								(varmaparr[0]), null };
							ProcessExprent(func.GetLstOperands()[1], varmaparrAnd);
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
							ProcessExprent(func.GetLstOperands()[0], varmaparr);
							SFormsFastMapDirect[] varmaparrOr = new SFormsFastMapDirect[] { new SFormsFastMapDirect
								(varmaparr[varmaparr[1] == null ? 0 : 1]), null };
							ProcessExprent(func.GetLstOperands()[1], varmaparrOr);
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
			if (finished)
			{
				return;
			}
			List<Exprent> lst = expr.GetAllExprents();
			lst.Remove(varassign);
			foreach (Exprent ex in lst)
			{
				ProcessExprent(ex, varmaparr);
			}
			SFormsFastMapDirect varmap = varmaparr[0];
			if (varassign != null)
			{
				int varindex = varassign.GetIndex();
				if (varassign.GetVersion() == 0)
				{
					// get next version
					int nextver = GetNextFreeVersion(varindex);
					// set version
					varassign.SetVersion(nextver);
					SetCurrentVar(varmap, varindex, nextver);
				}
				else
				{
					SetCurrentVar(varmap, varindex, varassign.GetVersion());
				}
			}
			else if (expr.type == Exprent.Exprent_Var)
			{
				VarExprent vardest = (VarExprent)expr;
				int varindex = vardest.GetIndex();
				FastSparseSetFactory<int>.FastSparseSet<int> vers = varmap.Get(varindex);
				int cardinality = vers.GetCardinality();
				if (cardinality == 1)
				{
					// == 1
					// set version
					int it = vers.GetEnumerator().Current;
					vardest.SetVersion(it);
				}
				else if (cardinality == 2)
				{
					// size > 1
					int current_vers = vardest.GetVersion();
					VarVersionPair currpaar = new VarVersionPair(varindex, current_vers);
					if (current_vers != 0 && phi.ContainsKey(currpaar))
					{
						SetCurrentVar(varmap, varindex, current_vers);
						// update phi node
						phi.GetOrNull(currpaar).Union(vers);
					}
					else
					{
						// increase version
						int nextver = GetNextFreeVersion(varindex);
						// set version
						vardest.SetVersion(nextver);
						SetCurrentVar(varmap, varindex, nextver);
						// create new phi node
						Sharpen.Collections.Put(phi, new VarVersionPair(varindex, nextver), vers);
					}
				}
			}
		}

		// 0 means uninitialized variable, which is impossible
		private int GetNextFreeVersion(int var)
		{
			int? nextver = lastversion.GetOrNullable(var);
			if (nextver == null)
			{
				nextver = 1;
			}
			else
			{
				nextver++;
			}
			Sharpen.Collections.Put(lastversion, var, nextver.Value);
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
			bool isFinallyExit = dgraph.mapShortRangeFinallyPaths.ContainsKey(predid);
			if (isFinallyExit && !mapNew.IsEmpty())
			{
				SFormsFastMapDirect mapNewTemp = mapNew.GetCopy();
				SFormsFastMapDirect mapTrueSource = new SFormsFastMapDirect();
				string exceptionDest = dgraph.mapFinallyMonitorExceptionPathExits.GetOrNull(predid
					);
				bool isExceptionMonitorExit = (exceptionDest != null && !nodeid.Equals(exceptionDest
					));
				HashSet<string> setLongPathWrapper = new HashSet<string>();
				foreach (FlattenStatementsHelper.FinallyPathWrapper finwraplong in dgraph.mapLongRangeFinallyPaths
					.GetOrNull(predid))
				{
					setLongPathWrapper.Add(finwraplong.destination + "##" + finwraplong.source);
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
					SFormsFastMapDirect oldInMap = inVarVersions.GetOrNull(nodeid);
					if (oldInMap != null)
					{
						mapNewTemp.Union(oldInMap);
					}
					mapNew.Intersection(mapNewTemp);
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
			foreach (KeyValuePair<int, FastSparseSetFactory<int>.FastSparseSet<int>> ent2 in map2.
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
			FastSparseSetFactory<int>.FastSparseSet<int> set = factory.SpawnEmptySet();
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
						int version = GetNextFreeVersion(varindex);
						// == 1
						map = new SFormsFastMapDirect();
						SetCurrentVar(map, varindex, version);
						Sharpen.Collections.Put(extraVarVersions, dgraph.nodes.GetWithKey(flatthelper.GetMapDestinationNodes
							().GetOrNull(stat.GetStats()[i].id)[0]).id, map);
					}
					break;
				}
			}
			foreach (Statement st in stat.GetStats())
			{
				SetCatchMaps(st, dgraph, flatthelper);
			}
		}

		private SFormsFastMapDirect CreateFirstMap(StructMethod mt)
		{
			bool thisvar = !mt.HasModifier(ICodeConstants.Acc_Static);
			MethodDescriptor md = MethodDescriptor.ParseDescriptor(mt.GetDescriptor());
			int paramcount = md.@params.Length + (thisvar ? 1 : 0);
			int varindex = 0;
			SFormsFastMapDirect map = new SFormsFastMapDirect();
			for (int i = 0; i < paramcount; i++)
			{
				int version = GetNextFreeVersion(varindex);
				// == 1
				FastSparseSetFactory<int>.FastSparseSet<int> set = factory.SpawnEmptySet();
				set.Add(version);
				map.Put(varindex, set);
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

		public virtual Dictionary<VarVersionPair, FastSparseSetFactory<int>.FastSparseSet<int>
			> GetPhi()
		{
			return phi;
		}
	}
}
