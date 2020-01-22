// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Sforms;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Vars
{
	public class VarVersionsProcessor
	{
		private readonly StructMethod method;

		private Dictionary<int, int> mapOriginalVarIndices = new System.Collections.Generic.Dictionary<
			int, int>();

		private readonly VarTypeProcessor typeProcessor;

		public VarVersionsProcessor(StructMethod mt, MethodDescriptor md)
		{
			method = mt;
			typeProcessor = new VarTypeProcessor(mt, md);
		}

		public virtual void SetVarVersions(RootStatement root, VarVersionsProcessor previousVersionsProcessor
			)
		{
			SSAConstructorSparseEx ssa = new SSAConstructorSparseEx();
			ssa.SplitVariables(root, method);
			FlattenStatementsHelper flattenHelper = new FlattenStatementsHelper();
			DirectGraph graph = flattenHelper.BuildDirectGraph(root);
			MergePhiVersions(ssa, graph);
			typeProcessor.CalculateVarTypes(root, graph);
			SimpleMerge(typeProcessor, graph, method);
			// FIXME: advanced merging
			EliminateNonJavaTypes(typeProcessor);
			SetNewVarIndices(typeProcessor, graph, previousVersionsProcessor);
		}

		private static void MergePhiVersions(SSAConstructorSparseEx ssa, DirectGraph graph
			)
		{
			// collect phi versions
			List<HashSet<VarVersionPair>> lst = new List<HashSet<VarVersionPair>>();
			foreach (KeyValuePair<VarVersionPair, FastSparseSetFactory<int>.FastSparseSet<int>> ent
				 in ssa.GetPhi())
			{
				HashSet<VarVersionPair> set = new HashSet<VarVersionPair>();
				set.Add(ent.Key);
				foreach (int version in ent.Value)
				{
					set.Add(new VarVersionPair(ent.Key.var, version));
				}
				for (int i = lst.Count - 1; i >= 0; i--)
				{
					HashSet<VarVersionPair> tset = lst[i];
					HashSet<VarVersionPair> intersection = new HashSet<VarVersionPair>(set);
					intersection.IntersectWith(tset);
					if (!(intersection.Count == 0))
					{
						Sharpen.Collections.AddAll(set, tset);
						lst.RemoveAtReturningValue(i);
					}
				}
				lst.Add(set);
			}
			Dictionary<VarVersionPair, int> phiVersions = new Dictionary<VarVersionPair, int
				>();
			foreach (HashSet<VarVersionPair> set in lst)
			{
				int min = int.MaxValue;
				foreach (VarVersionPair paar in set)
				{
					if (paar.version < min)
					{
						min = paar.version;
					}
				}
				foreach (VarVersionPair paar in set)
				{
					Sharpen.Collections.Put(phiVersions, new VarVersionPair(paar.var, paar.version), 
						min);
				}
			}
			UpdateVersions(graph, phiVersions);
		}

		private static void UpdateVersions(DirectGraph graph, Dictionary<VarVersionPair, 
			int> versions)
		{
			graph.IterateExprents((Exprent exprent) => 			{
					List<Exprent> lst = exprent.GetAllExprents(true);
					lst.Add(exprent);
					foreach (Exprent expr in lst)
					{
						if (expr.type == Exprent.Exprent_Var)
						{
							VarExprent var = (VarExprent)expr;
							int? version = versions.GetOrNullable(new VarVersionPair(var));
							if (version != null)
							{
								var.SetVersion(version.Value);
							}
						}
					}
					return 0;
				}
);
		}

		private static void EliminateNonJavaTypes(VarTypeProcessor typeProcessor)
		{
			Dictionary<VarVersionPair, VarType> mapExprentMaxTypes = typeProcessor.GetMapExprentMaxTypes
				();
			Dictionary<VarVersionPair, VarType> mapExprentMinTypes = typeProcessor.GetMapExprentMinTypes
				();
			foreach (VarVersionPair paar in new List<VarVersionPair>(mapExprentMinTypes.Keys))
			{
				VarType type = mapExprentMinTypes.GetOrNull(paar);
				VarType maxType = mapExprentMaxTypes.GetOrNull(paar);
				if (type.type == ICodeConstants.Type_Bytechar || type.type == ICodeConstants.Type_Shortchar)
				{
					if (maxType != null && maxType.type == ICodeConstants.Type_Char)
					{
						type = VarType.Vartype_Char;
					}
					else
					{
						type = type.type == ICodeConstants.Type_Bytechar ? VarType.Vartype_Byte : VarType
							.Vartype_Short;
					}
					Sharpen.Collections.Put(mapExprentMinTypes, paar, type);
				}
				else if (type.type == ICodeConstants.Type_Null)
				{
					//} else if(type.type == CodeConstants.TYPE_CHAR && (maxType == null || maxType.type == CodeConstants.TYPE_INT)) { // when possible, lift char to int
					//	mapExprentMinTypes.put(paar, VarType.VARTYPE_INT);
					Sharpen.Collections.Put(mapExprentMinTypes, paar, VarType.Vartype_Object);
				}
			}
		}

		private static void SimpleMerge(VarTypeProcessor typeProcessor, DirectGraph graph
			, StructMethod mt)
		{
			Dictionary<VarVersionPair, VarType> mapExprentMaxTypes = typeProcessor.GetMapExprentMaxTypes
				();
			Dictionary<VarVersionPair, VarType> mapExprentMinTypes = typeProcessor.GetMapExprentMinTypes
				();
			Dictionary<int, HashSet<int>> mapVarVersions = new Dictionary<int, HashSet<int>>
				();
			foreach (VarVersionPair pair in mapExprentMinTypes.Keys)
			{
				if (pair.version >= 0)
				{
					// don't merge constants
					mapVarVersions.ComputeIfAbsent(pair.var, (int k) => new HashSet<int>()).Add(pair.
						version);
				}
			}
			bool is_method_static = mt.HasModifier(ICodeConstants.Acc_Static);
			Dictionary<VarVersionPair, int> mapMergedVersions = new Dictionary<VarVersionPair
				, int>();
			foreach (KeyValuePair<int, HashSet<int>> ent in mapVarVersions)
			{
				if (ent.Value.Count > 1)
				{
					List<int> lstVersions = new List<int>(ent.Value);
					lstVersions.Sort();
					for (int i = 0; i < lstVersions.Count; i++)
					{
						VarVersionPair firstPair = new VarVersionPair(ent.Key, lstVersions[i]);
						VarType firstType = mapExprentMinTypes.GetOrNull(firstPair);
						if (firstPair.var == 0 && firstPair.version == 1 && !is_method_static)
						{
							continue;
						}
						// don't merge 'this' variable
						for (int j = i + 1; j < lstVersions.Count; j++)
						{
							VarVersionPair secondPair = new VarVersionPair(ent.Key, lstVersions[j]);
							VarType secondType = mapExprentMinTypes.GetOrNull(secondPair);
							if (firstType.Equals(secondType) || (firstType.Equals(VarType.Vartype_Null) && secondType
								.type == ICodeConstants.Type_Object) || (secondType.Equals(VarType.Vartype_Null)
								 && firstType.type == ICodeConstants.Type_Object))
							{
								VarType firstMaxType = mapExprentMaxTypes.GetOrNull(firstPair);
								VarType secondMaxType = mapExprentMaxTypes.GetOrNull(secondPair);
								VarType type = firstMaxType == null ? secondMaxType : secondMaxType == null ? firstMaxType
									 : VarType.GetCommonMinType(firstMaxType, secondMaxType);
								Sharpen.Collections.Put(mapExprentMaxTypes, firstPair, type);
								Sharpen.Collections.Put(mapMergedVersions, secondPair, firstPair.version);
								Sharpen.Collections.Remove(mapExprentMaxTypes, secondPair);
								Sharpen.Collections.Remove(mapExprentMinTypes, secondPair);
								if (firstType.Equals(VarType.Vartype_Null))
								{
									Sharpen.Collections.Put(mapExprentMinTypes, firstPair, secondType);
									firstType = secondType;
								}
								Sharpen.Collections.Put(typeProcessor.GetMapFinalVars(), firstPair, VarTypeProcessor
									.Var_Non_Final);
								lstVersions.RemoveAtReturningValue(j);
								//noinspection AssignmentToForLoopParameter
								j--;
							}
						}
					}
				}
			}
			if (!(mapMergedVersions.Count == 0))
			{
				UpdateVersions(graph, mapMergedVersions);
			}
		}

		private void SetNewVarIndices(VarTypeProcessor typeProcessor, DirectGraph graph, 
			VarVersionsProcessor previousVersionsProcessor)
		{
			Dictionary<VarVersionPair, VarType> mapExprentMaxTypes = typeProcessor.GetMapExprentMaxTypes
				();
			Dictionary<VarVersionPair, VarType> mapExprentMinTypes = typeProcessor.GetMapExprentMinTypes
				();
			Dictionary<VarVersionPair, int> mapFinalVars = typeProcessor.GetMapFinalVars();
			CounterContainer counters = DecompilerContext.GetCounterContainer();
			Dictionary<VarVersionPair, int> mapVarPaar = new Dictionary<VarVersionPair, int>
				();
			Dictionary<int, int> mapOriginalVarIndices = new Dictionary<int, int>();
			// map var-version pairs on new var indexes
			foreach (VarVersionPair pair in new List<VarVersionPair>(mapExprentMinTypes.Keys))
			{
				if (pair.version >= 0)
				{
					int newIndex = pair.version == 1 ? pair.var : counters.GetCounterAndIncrement(CounterContainer
						.Var_Counter);
					VarVersionPair newVar = new VarVersionPair(newIndex, 0);
					Sharpen.Collections.Put(mapExprentMinTypes, newVar, mapExprentMinTypes.GetOrNull(
						pair));
					Sharpen.Collections.Put(mapExprentMaxTypes, newVar, mapExprentMaxTypes.GetOrNull(
						pair));
					if (mapFinalVars.ContainsKey(pair))
					{
						Sharpen.Collections.Put(mapFinalVars, newVar, Sharpen.Collections.Remove(mapFinalVars
							, pair));
					}
					Sharpen.Collections.Put(mapVarPaar, pair, newIndex);
					Sharpen.Collections.Put(mapOriginalVarIndices, newIndex, pair.var);
				}
			}
			// set new vars
			graph.IterateExprents((Exprent exprent) => 			{
					List<Exprent> lst = exprent.GetAllExprents(true);
					lst.Add(exprent);
					foreach (Exprent expr in lst)
					{
						if (expr.type == Exprent.Exprent_Var)
						{
							VarExprent newVar = (VarExprent)expr;
							int? newVarIndex = mapVarPaar.GetOrNullable(new VarVersionPair(newVar));
							if (newVarIndex != null)
							{
								newVar.SetIndex(newVarIndex.Value);
								newVar.SetVersion(0);
							}
						}
						else if (expr.type == Exprent.Exprent_Const)
						{
							VarType maxType = mapExprentMaxTypes.GetOrNull(new VarVersionPair(expr.id, -1));
							if (maxType != null && maxType.Equals(VarType.Vartype_Char))
							{
								((ConstExprent)expr).SetConstType(maxType);
							}
						}
					}
					return 0;
				}
);
			if (previousVersionsProcessor != null)
			{
				Dictionary<int, int> oldIndices = previousVersionsProcessor.GetMapOriginalVarIndices
					();
				this.mapOriginalVarIndices = new Dictionary<int, int>(mapOriginalVarIndices.Count
					);
				foreach (KeyValuePair<int, int> entry in mapOriginalVarIndices)
				{
					int value = entry.Value;
					int? oldValue = oldIndices.GetOrNullable(value);
					value = oldValue != null ? oldValue.Value : value;
					Sharpen.Collections.Put(this.mapOriginalVarIndices, entry.Key, value);
				}
			}
			else
			{
				this.mapOriginalVarIndices = mapOriginalVarIndices;
			}
		}

		public virtual VarType GetVarType(VarVersionPair pair)
		{
			return typeProcessor.GetVarType(pair);
		}

		public virtual void SetVarType(VarVersionPair pair, VarType type)
		{
			typeProcessor.SetVarType(pair, type);
		}

		public virtual int GetVarFinal(VarVersionPair pair)
		{
			int? fin = typeProcessor.GetMapFinalVars().GetOrNullable(pair);
			return fin == null ? VarTypeProcessor.Var_Final : fin.Value;
		}

		public virtual void SetVarFinal(VarVersionPair pair, int finalType)
		{
			Sharpen.Collections.Put(typeProcessor.GetMapFinalVars(), pair, finalType);
		}

		public virtual Dictionary<int, int> GetMapOriginalVarIndices()
		{
			return mapOriginalVarIndices;
		}
	}
}
