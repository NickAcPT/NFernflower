// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Sforms;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Struct.Gen;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class PPandMMHelper
	{
		private bool exprentReplaced;

		public virtual bool FindPPandMM(RootStatement root)
		{
			FlattenStatementsHelper flatthelper = new FlattenStatementsHelper();
			DirectGraph dgraph = flatthelper.BuildDirectGraph(root);
			LinkedList<DirectNode> stack = new LinkedList<DirectNode>();
			stack.AddLast(dgraph.first);
			HashSet<DirectNode> setVisited = new HashSet<DirectNode>();
			bool res = false;
			while (!(stack.Count == 0))
			{
				DirectNode node = Sharpen.Collections.RemoveFirst(stack);
				if (setVisited.Contains(node))
				{
					continue;
				}
				setVisited.Add(node);
				res |= ProcessExprentList(node.exprents);
				Sharpen.Collections.AddAll(stack, node.succs);
			}
			return res;
		}

		private bool ProcessExprentList(List<Exprent> lst)
		{
			bool result = false;
			for (int i = 0; i < lst.Count; i++)
			{
				Exprent exprent = lst[i];
				exprentReplaced = false;
				Exprent retexpr = ProcessExprentRecursive(exprent);
				if (retexpr != null)
				{
					lst[i] = retexpr;
					result = true;
					i--;
				}
				// process the same exprent again
				result |= exprentReplaced;
			}
			return result;
		}

		private Exprent ProcessExprentRecursive(Exprent exprent)
		{
			bool replaced = true;
			while (replaced)
			{
				replaced = false;
				foreach (Exprent expr in exprent.GetAllExprents())
				{
					Exprent retexpr = ProcessExprentRecursive(expr);
					if (retexpr != null)
					{
						exprent.ReplaceExprent(expr, retexpr);
						replaced = true;
						exprentReplaced = true;
						break;
					}
				}
			}
			if (exprent.type == Exprent.Exprent_Assignment)
			{
				AssignmentExprent @as = (AssignmentExprent)exprent;
				if (@as.GetRight().type == Exprent.Exprent_Function)
				{
					FunctionExprent func = (FunctionExprent)@as.GetRight();
					VarType midlayer = null;
					if (func.GetFuncType() >= FunctionExprent.Function_I2l && func.GetFuncType() <= FunctionExprent
						.Function_I2s)
					{
						midlayer = func.GetSimpleCastType();
						if (func.GetLstOperands()[0].type == Exprent.Exprent_Function)
						{
							func = (FunctionExprent)func.GetLstOperands()[0];
						}
						else
						{
							return null;
						}
					}
					if (func.GetFuncType() == FunctionExprent.Function_Add || func.GetFuncType() == FunctionExprent
						.Function_Sub)
					{
						Exprent econd = func.GetLstOperands()[0];
						Exprent econst = func.GetLstOperands()[1];
						if (econst.type != Exprent.Exprent_Const && econd.type == Exprent.Exprent_Const &&
							 func.GetFuncType() == FunctionExprent.Function_Add)
						{
							econd = econst;
							econst = func.GetLstOperands()[0];
						}
						if (econst.type == Exprent.Exprent_Const && ((ConstExprent)econst).HasValueOne())
						{
							Exprent left = @as.GetLeft();
							VarType condtype = econd.GetExprType();
							if (left.Equals(econd) && (midlayer == null || midlayer.Equals(condtype)))
							{
								FunctionExprent ret = new FunctionExprent(func.GetFuncType() == FunctionExprent.Function_Add
									 ? FunctionExprent.Function_Ppi : FunctionExprent.Function_Mmi, econd, func.bytecode
									);
								ret.SetImplicitType(condtype);
								exprentReplaced = true;
								return ret;
							}
						}
					}
				}
			}
			return null;
		}
	}
}
