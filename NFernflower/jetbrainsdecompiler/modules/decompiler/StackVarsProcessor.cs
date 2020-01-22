// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Sforms;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class StackVarsProcessor
	{
		public virtual void SimplifyStackVars(RootStatement root, StructMethod mt, StructClass
			 cl)
		{
			HashSet<int> setReorderedIfs = new HashSet<int>();
			SSAUConstructorSparseEx ssau = null;
			while (true)
			{
				bool found = false;
				SSAConstructorSparseEx ssa = new SSAConstructorSparseEx();
				ssa.SplitVariables(root, mt);
				SimplifyExprentsHelper sehelper = new SimplifyExprentsHelper(ssau == null);
				while (sehelper.SimplifyStackVarsStatement(root, setReorderedIfs, ssa, cl))
				{
					found = true;
				}
				SetVersionsToNull(root);
				SequenceHelper.CondenseSequences(root);
				ssau = new SSAUConstructorSparseEx();
				ssau.SplitVariables(root, mt);
				if (IterateStatements(root, ssau))
				{
					found = true;
				}
				SetVersionsToNull(root);
				if (!found)
				{
					break;
				}
			}
			// remove unused assignments
			ssau = new SSAUConstructorSparseEx();
			ssau.SplitVariables(root, mt);
			IterateStatements(root, ssau);
			SetVersionsToNull(root);
		}

		private static void SetVersionsToNull(Statement stat)
		{
			if (stat.GetExprents() == null)
			{
				foreach (object obj in stat.GetSequentialObjects())
				{
					if (obj is Statement)
					{
						SetVersionsToNull((Statement)obj);
					}
					else if (obj is Exprent)
					{
						SetExprentVersionsToNull((Exprent)obj);
					}
				}
			}
			else
			{
				foreach (Exprent exprent in stat.GetExprents())
				{
					SetExprentVersionsToNull(exprent);
				}
			}
		}

		private static void SetExprentVersionsToNull(Exprent exprent)
		{
			List<Exprent> lst = exprent.GetAllExprents(true);
			lst.Add(exprent);
			foreach (Exprent expr in lst)
			{
				if (expr.type == Exprent.Exprent_Var)
				{
					((VarExprent)expr).SetVersion(0);
				}
			}
		}

		private bool IterateStatements(RootStatement root, SSAUConstructorSparseEx ssa)
		{
			FlattenStatementsHelper flatthelper = new FlattenStatementsHelper();
			DirectGraph dgraph = flatthelper.BuildDirectGraph(root);
			bool res = false;
			HashSet<DirectNode> setVisited = new HashSet<DirectNode>();
			LinkedList<DirectNode> stack = new LinkedList<DirectNode>();
			LinkedList<IDictionary<VarVersionPair, Exprent>> stackMaps = new LinkedList<IDictionary
				<VarVersionPair, Exprent>>();
			stack.Add(dgraph.first);
			stackMaps.Add(new Dictionary<VarVersionPair, Exprent>());
			while (!(stack.Count == 0))
			{
				DirectNode nd = Sharpen.Collections.RemoveFirst(stack);
				IDictionary<VarVersionPair, Exprent> mapVarValues = Sharpen.Collections.RemoveFirst
					(stackMaps);
				if (setVisited.Contains(nd))
				{
					continue;
				}
				setVisited.Add(nd);
				List<List<Exprent>> lstLists = new List<List<Exprent>>();
				if (!(nd.exprents.Count == 0))
				{
					lstLists.Add(nd.exprents);
				}
				if (nd.succs.Count == 1)
				{
					DirectNode ndsucc = nd.succs[0];
					if (ndsucc.type == DirectNode.Node_Tail && !(ndsucc.exprents.Count == 0))
					{
						lstLists.Add(nd.succs[0].exprents);
						nd = ndsucc;
					}
				}
				for (int i = 0; i < lstLists.Count; i++)
				{
					List<Exprent> lst = lstLists[i];
					int index = 0;
					while (index < lst.Count)
					{
						Exprent next = null;
						if (index == lst.Count - 1)
						{
							if (i < lstLists.Count - 1)
							{
								next = lstLists[i + 1][0];
							}
						}
						else
						{
							next = lst[index + 1];
						}
						int[] ret = IterateExprent(lst, index, next, mapVarValues, ssa);
						if (ret[0] >= 0)
						{
							index = ret[0];
						}
						else
						{
							index++;
						}
						res |= (ret[1] == 1);
					}
				}
				foreach (DirectNode ndx in nd.succs)
				{
					stack.Add(ndx);
					stackMaps.Add(new Dictionary<VarVersionPair, Exprent>(mapVarValues));
				}
				// make sure the 3 special exprent lists in a loop (init, condition, increment) are not empty
				// change loop type if necessary
				if ((nd.exprents.Count == 0) && (nd.type == DirectNode.Node_Init || nd.type == DirectNode
					.Node_Condition || nd.type == DirectNode.Node_Increment))
				{
					nd.exprents.Add(null);
					if (nd.statement.type == Statement.Type_Do)
					{
						DoStatement loop = (DoStatement)nd.statement;
						if (loop.GetLooptype() == DoStatement.Loop_For && loop.GetInitExprent() == null &&
							 loop.GetIncExprent() == null)
						{
							// "downgrade" loop to 'while'
							loop.SetLooptype(DoStatement.Loop_While);
						}
					}
				}
			}
			return res;
		}

		private static Exprent IsReplaceableVar(Exprent exprent, IDictionary<VarVersionPair
			, Exprent> mapVarValues)
		{
			Exprent dest = null;
			if (exprent.type == Exprent.Exprent_Var)
			{
				VarExprent var = (VarExprent)exprent;
				dest = mapVarValues.GetOrNull(new VarVersionPair(var));
			}
			return dest;
		}

		private static void ReplaceSingleVar(Exprent parent, VarExprent var, Exprent dest
			, SSAUConstructorSparseEx ssau)
		{
			parent.ReplaceExprent(var, dest);
			// live sets
			SFormsFastMapDirect livemap = ssau.GetLiveVarVersionsMap(new VarVersionPair(var));
			HashSet<VarVersionPair> setVars = GetAllVersions(dest);
			foreach (VarVersionPair varpaar in setVars)
			{
				VarVersionNode node = ssau.GetSsuversions().nodes.GetWithKey(varpaar);
				for (IEnumerator<KeyValuePair<int, FastSparseSetFactory.FastSparseSet<int>>> itent
					 = node.live.EntryList().GetEnumerator(); itent.MoveNext(); )
				{
					KeyValuePair<int, FastSparseSetFactory.FastSparseSet<int>> ent = itent.Current;
					int key = ent.Key;
					if (!livemap.ContainsKey(key))
					{
						itent.Remove();
					}
					else
					{
						FastSparseSetFactory.FastSparseSet<int> set = ent.Value;
						set.Complement(livemap.Get(key));
						if (set.IsEmpty())
						{
							itent.Remove();
						}
					}
				}
			}
		}

		private int[] IterateExprent(List<Exprent> lstExprents, int index, Exprent next, 
			IDictionary<VarVersionPair, Exprent> mapVarValues, SSAUConstructorSparseEx ssau)
		{
			Exprent exprent = lstExprents[index];
			int changed = 0;
			foreach (Exprent expr in exprent.GetAllExprents())
			{
				while (true)
				{
					object[] arr = IterateChildExprent(expr, exprent, next, mapVarValues, ssau);
					Exprent retexpr = (Exprent)arr[0];
					changed |= (bool)arr[1] ? 1 : 0;
					bool isReplaceable = (bool)arr[2];
					if (retexpr != null)
					{
						if (isReplaceable)
						{
							ReplaceSingleVar(exprent, (VarExprent)expr, retexpr, ssau);
							expr = retexpr;
						}
						else
						{
							exprent.ReplaceExprent(expr, retexpr);
						}
						changed = 1;
					}
					if (!isReplaceable)
					{
						break;
					}
				}
			}
			// no var on the highest level, so no replacing
			VarExprent left = null;
			Exprent right = null;
			if (exprent.type == Exprent.Exprent_Assignment)
			{
				AssignmentExprent @as = (AssignmentExprent)exprent;
				if (@as.GetLeft().type == Exprent.Exprent_Var)
				{
					left = (VarExprent)@as.GetLeft();
					right = @as.GetRight();
				}
			}
			if (left == null)
			{
				return new int[] { -1, changed };
			}
			VarVersionPair leftpaar = new VarVersionPair(left);
			List<VarVersionNode> usedVers = new List<VarVersionNode>();
			bool notdom = GetUsedVersions(ssau, leftpaar, usedVers);
			if (!notdom && (usedVers.Count == 0))
			{
				if (left.IsStack() && (right.type == Exprent.Exprent_Invocation || right.type == 
					Exprent.Exprent_Assignment || right.type == Exprent.Exprent_New))
				{
					if (right.type == Exprent.Exprent_New)
					{
						// new Object(); permitted
						NewExprent nexpr = (NewExprent)right;
						if (nexpr.IsAnonymous() || nexpr.GetNewType().arrayDim > 0 || nexpr.GetNewType().
							type != ICodeConstants.Type_Object)
						{
							return new int[] { -1, changed };
						}
					}
					lstExprents[index] = right;
					return new int[] { index + 1, 1 };
				}
				else if (right.type == Exprent.Exprent_Var)
				{
					lstExprents.RemoveAtReturningValue(index);
					return new int[] { index, 1 };
				}
				else
				{
					return new int[] { -1, changed };
				}
			}
			int useflags = right.GetExprentUse();
			// stack variables only
			if (!left.IsStack() && (right.type != Exprent.Exprent_Var || ((VarExprent)right).
				IsStack()))
			{
				// special case catch(... ex)
				return new int[] { -1, changed };
			}
			if ((useflags & Exprent.Multiple_Uses) == 0 && (notdom || usedVers.Count > 1))
			{
				return new int[] { -1, changed };
			}
			IDictionary<int, HashSet<VarVersionPair>> mapVars = GetAllVarVersions(leftpaar, right
				, ssau);
			bool isSelfReference = mapVars.ContainsKey(leftpaar.var);
			if (isSelfReference && notdom)
			{
				return new int[] { -1, changed };
			}
			HashSet<VarVersionPair> setNextVars = next == null ? null : GetAllVersions(next);
			// FIXME: fix the entire method!
			if (right.type != Exprent.Exprent_Const && right.type != Exprent.Exprent_Var && setNextVars
				 != null && mapVars.ContainsKey(leftpaar.var))
			{
				foreach (VarVersionNode usedvar in usedVers)
				{
					if (!setNextVars.Contains(new VarVersionPair(usedvar.var, usedvar.version)))
					{
						return new int[] { -1, changed };
					}
				}
			}
			Sharpen.Collections.Remove(mapVars, leftpaar.var);
			bool vernotreplaced = false;
			bool verreplaced = false;
			HashSet<VarVersionPair> setTempUsedVers = new HashSet<VarVersionPair>();
			foreach (VarVersionNode usedvar in usedVers)
			{
				VarVersionPair usedver = new VarVersionPair(usedvar.var, usedvar.version);
				if (IsVersionToBeReplaced(usedver, mapVars, ssau, leftpaar) && (right.type == Exprent
					.Exprent_Const || right.type == Exprent.Exprent_Var || right.type == Exprent.Exprent_Field
					 || setNextVars == null || setNextVars.Contains(usedver)))
				{
					setTempUsedVers.Add(usedver);
					verreplaced = true;
				}
				else
				{
					vernotreplaced = true;
				}
			}
			if (isSelfReference && vernotreplaced)
			{
				return new int[] { -1, changed };
			}
			else
			{
				foreach (VarVersionPair usedver in setTempUsedVers)
				{
					Exprent copy = right.Copy();
					if (right.type == Exprent.Exprent_Field && ssau.GetMapFieldVars().ContainsKey(right
						.id))
					{
						Sharpen.Collections.Put(ssau.GetMapFieldVars(), copy.id, ssau.GetMapFieldVars().GetOrNullable
							(right.id));
					}
					Sharpen.Collections.Put(mapVarValues, usedver, copy);
				}
			}
			if (!notdom && !vernotreplaced)
			{
				// remove assignment
				lstExprents.RemoveAtReturningValue(index);
				return new int[] { index, 1 };
			}
			else if (verreplaced)
			{
				return new int[] { index + 1, changed };
			}
			else
			{
				return new int[] { -1, changed };
			}
		}

		private static HashSet<VarVersionPair> GetAllVersions(Exprent exprent)
		{
			HashSet<VarVersionPair> res = new HashSet<VarVersionPair>();
			List<Exprent> listTemp = new List<Exprent>(exprent.GetAllExprents(true));
			listTemp.Add(exprent);
			foreach (Exprent expr in listTemp)
			{
				if (expr.type == Exprent.Exprent_Var)
				{
					VarExprent var = (VarExprent)expr;
					res.Add(new VarVersionPair(var));
				}
			}
			return res;
		}

		private static object[] IterateChildExprent(Exprent exprent, Exprent parent, Exprent
			 next, IDictionary<VarVersionPair, Exprent> mapVarValues, SSAUConstructorSparseEx
			 ssau)
		{
			bool changed = false;
			foreach (Exprent expr in exprent.GetAllExprents())
			{
				while (true)
				{
					object[] arr = IterateChildExprent(expr, parent, next, mapVarValues, ssau);
					Exprent retexpr = (Exprent)arr[0];
					changed |= (bool)arr[1];
					bool isReplaceable = (bool)arr[2];
					if (retexpr != null)
					{
						if (isReplaceable)
						{
							ReplaceSingleVar(exprent, (VarExprent)expr, retexpr, ssau);
							expr = retexpr;
						}
						else
						{
							exprent.ReplaceExprent(expr, retexpr);
						}
						changed = true;
					}
					if (!isReplaceable)
					{
						break;
					}
				}
			}
			Exprent dest = IsReplaceableVar(exprent, mapVarValues);
			if (dest != null)
			{
				return new object[] { dest, true, true };
			}
			VarExprent left = null;
			Exprent right = null;
			if (exprent.type == Exprent.Exprent_Assignment)
			{
				AssignmentExprent @as = (AssignmentExprent)exprent;
				if (@as.GetLeft().type == Exprent.Exprent_Var)
				{
					left = (VarExprent)@as.GetLeft();
					right = @as.GetRight();
				}
			}
			if (left == null)
			{
				return new object[] { null, changed, false };
			}
			bool isHeadSynchronized = false;
			if (next == null && parent.type == Exprent.Exprent_Monitor)
			{
				MonitorExprent monexpr = (MonitorExprent)parent;
				if (monexpr.GetMonType() == MonitorExprent.Monitor_Enter && exprent.Equals(monexpr
					.GetValue()))
				{
					isHeadSynchronized = true;
				}
			}
			// stack variable or synchronized head exprent
			if (!left.IsStack() && !isHeadSynchronized)
			{
				return new object[] { null, changed, false };
			}
			VarVersionPair leftpaar = new VarVersionPair(left);
			List<VarVersionNode> usedVers = new List<VarVersionNode>();
			bool notdom = GetUsedVersions(ssau, leftpaar, usedVers);
			if (!notdom && (usedVers.Count == 0))
			{
				return new object[] { right, changed, false };
			}
			// stack variables only
			if (!left.IsStack())
			{
				return new object[] { null, changed, false };
			}
			int useflags = right.GetExprentUse();
			if ((useflags & Exprent.Both_Flags) != Exprent.Both_Flags)
			{
				return new object[] { null, changed, false };
			}
			IDictionary<int, HashSet<VarVersionPair>> mapVars = GetAllVarVersions(leftpaar, right
				, ssau);
			if (mapVars.ContainsKey(leftpaar.var) && notdom)
			{
				return new object[] { null, changed, false };
			}
			Sharpen.Collections.Remove(mapVars, leftpaar.var);
			HashSet<VarVersionPair> setAllowedVars = GetAllVersions(parent);
			if (next != null)
			{
				Sharpen.Collections.AddAll(setAllowedVars, GetAllVersions(next));
			}
			bool vernotreplaced = false;
			HashSet<VarVersionPair> setTempUsedVers = new HashSet<VarVersionPair>();
			foreach (VarVersionNode usedvar in usedVers)
			{
				VarVersionPair usedver = new VarVersionPair(usedvar.var, usedvar.version);
				if (IsVersionToBeReplaced(usedver, mapVars, ssau, leftpaar) && (right.type == Exprent
					.Exprent_Var || setAllowedVars.Contains(usedver)))
				{
					setTempUsedVers.Add(usedver);
				}
				else
				{
					vernotreplaced = true;
				}
			}
			if (!notdom && !vernotreplaced)
			{
				foreach (VarVersionPair usedver in setTempUsedVers)
				{
					Exprent copy = right.Copy();
					if (right.type == Exprent.Exprent_Field && ssau.GetMapFieldVars().ContainsKey(right
						.id))
					{
						Sharpen.Collections.Put(ssau.GetMapFieldVars(), copy.id, ssau.GetMapFieldVars().GetOrNullable
							(right.id));
					}
					Sharpen.Collections.Put(mapVarValues, usedver, copy);
				}
				// remove assignment
				return new object[] { right, changed, false };
			}
			return new object[] { null, changed, false };
		}

		private static bool GetUsedVersions<_T0>(SSAUConstructorSparseEx ssa, VarVersionPair
			 var, List<_T0> res)
		{
			VarVersionsGraph ssuversions = ssa.GetSsuversions();
			VarVersionNode varnode = ssuversions.nodes.GetWithKey(var);
			HashSet<VarVersionNode> setVisited = new HashSet<VarVersionNode>();
			HashSet<VarVersionNode> setNotDoms = new HashSet<VarVersionNode>();
			LinkedList<VarVersionNode> stack = new LinkedList<VarVersionNode>();
			stack.Add(varnode);
			while (!(stack.Count == 0))
			{
				VarVersionNode nd = stack.RemoveAtReturningValue(0);
				setVisited.Add(nd);
				if (nd != varnode && (nd.flags & VarVersionNode.Flag_Phantom_Finexit) == 0)
				{
					res.Add(nd);
				}
				foreach (VarVersionEdge edge in nd.succs)
				{
					VarVersionNode succ = edge.dest;
					if (!setVisited.Contains(edge.dest))
					{
						bool isDominated = true;
						foreach (VarVersionEdge prededge in succ.preds)
						{
							if (!setVisited.Contains(prededge.source))
							{
								isDominated = false;
								break;
							}
						}
						if (isDominated)
						{
							stack.Add(succ);
						}
						else
						{
							setNotDoms.Add(succ);
						}
					}
				}
			}
			setNotDoms.RemoveAll(setVisited);
			return !(setNotDoms.Count == 0);
		}

		private static bool IsVersionToBeReplaced(VarVersionPair usedvar, IDictionary<int
			, HashSet<VarVersionPair>> mapVars, SSAUConstructorSparseEx ssau, VarVersionPair
			 leftpaar)
		{
			VarVersionsGraph ssuversions = ssau.GetSsuversions();
			SFormsFastMapDirect mapLiveVars = ssau.GetLiveVarVersionsMap(usedvar);
			if (mapLiveVars == null)
			{
				// dummy version, predecessor of a phi node
				return false;
			}
			// compare protected ranges
			if (!InterpreterUtil.EqualObjects(ssau.GetMapVersionFirstRange().GetOrNullable(leftpaar
				), ssau.GetMapVersionFirstRange().GetOrNullable(usedvar)))
			{
				return false;
			}
			foreach (KeyValuePair<int, HashSet<VarVersionPair>> ent in mapVars)
			{
				FastSparseSetFactory.FastSparseSet<int> liveverset = mapLiveVars.Get(ent.Key);
				if (liveverset == null)
				{
					return false;
				}
				HashSet<VarVersionNode> domset = new HashSet<VarVersionNode>();
				foreach (VarVersionPair verpaar in ent.Value)
				{
					domset.Add(ssuversions.nodes.GetWithKey(verpaar));
				}
				bool isdom = false;
				foreach (int livever in liveverset)
				{
					VarVersionNode node = ssuversions.nodes.GetWithKey(new VarVersionPair(ent.Key, livever
						));
					if (ssuversions.IsDominatorSet(node, domset))
					{
						isdom = true;
						break;
					}
				}
				if (!isdom)
				{
					return false;
				}
			}
			return true;
		}

		private static IDictionary<int, HashSet<VarVersionPair>> GetAllVarVersions(VarVersionPair
			 leftvar, Exprent exprent, SSAUConstructorSparseEx ssau)
		{
			IDictionary<int, HashSet<VarVersionPair>> map = new Dictionary<int, HashSet<VarVersionPair
				>>();
			SFormsFastMapDirect mapLiveVars = ssau.GetLiveVarVersionsMap(leftvar);
			List<Exprent> lst = exprent.GetAllExprents(true);
			lst.Add(exprent);
			foreach (Exprent expr in lst)
			{
				if (expr.type == Exprent.Exprent_Var)
				{
					int varindex = ((VarExprent)expr).GetIndex();
					if (leftvar.var != varindex)
					{
						if (mapLiveVars.ContainsKey(varindex))
						{
							HashSet<VarVersionPair> verset = new HashSet<VarVersionPair>();
							foreach (int vers in mapLiveVars.Get(varindex))
							{
								verset.Add(new VarVersionPair(varindex, vers));
							}
							Sharpen.Collections.Put(map, varindex, verset);
						}
						else
						{
							throw new Exception("inkonsistent live map!");
						}
					}
					else
					{
						Sharpen.Collections.Put(map, varindex, null);
					}
				}
				else if (expr.type == Exprent.Exprent_Field)
				{
					if (ssau.GetMapFieldVars().ContainsKey(expr.id))
					{
						int? varindex = ssau.GetMapFieldVars().GetOrNullable(expr.id);
						if (mapLiveVars.ContainsKey(varindex.Value))
						{
							HashSet<VarVersionPair> verset = new HashSet<VarVersionPair>();
							foreach (int vers in mapLiveVars.Get(varindex.Value))
							{
								verset.Add(new VarVersionPair(varindex.Value, vers));
							}
							Sharpen.Collections.Put(map, varindex.Value, verset);
						}
					}
				}
			}
			return map;
		}
	}
}
