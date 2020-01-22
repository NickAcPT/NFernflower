// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using System.Linq;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Vars
{
	public class VarDefinitionHelper
	{
		private readonly Dictionary<int, Statement> mapVarDefStatements;

		private readonly Dictionary<int, HashSet<int>> mapStatementVars;

		private readonly HashSet<int> implDefVars;

		private readonly VarProcessor varproc;

		public VarDefinitionHelper(Statement root, StructMethod mt, VarProcessor varproc)
		{
			// statement.id, defined vars
			mapVarDefStatements = new Dictionary<int, Statement>();
			mapStatementVars = new Dictionary<int, HashSet<int>>();
			implDefVars = new HashSet<int>();
			this.varproc = varproc;
			VarNamesCollector vc = varproc.GetVarNamesCollector();
			bool thisvar = !mt.HasModifier(ICodeConstants.Acc_Static);
			MethodDescriptor md = MethodDescriptor.ParseDescriptor(mt.GetDescriptor());
			int paramcount = 0;
			if (thisvar)
			{
				paramcount = 1;
			}
			paramcount += md.@params.Length;
			// method parameters are implicitly defined
			int varindex = 0;
			for (int i = 0; i < paramcount; i++)
			{
				implDefVars.Add(varindex);
				varproc.SetVarName(new VarVersionPair(varindex, 0), vc.GetFreeName(varindex));
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
			if (thisvar)
			{
				StructClass current_class = (StructClass)DecompilerContext.GetProperty(DecompilerContext
					.Current_Class);
				Sharpen.Collections.Put(varproc.GetThisVars(), new VarVersionPair(0, 0), current_class
					.qualifiedName);
				varproc.SetVarName(new VarVersionPair(0, 0), "this");
				vc.AddName("this");
			}
			// catch variables are implicitly defined
			LinkedList<Statement> stack = new LinkedList<Statement>();
			stack.AddLast(root);
			while (!(stack.Count == 0))
			{
				Statement st = Sharpen.Collections.RemoveFirst(stack);
				List<VarExprent> lstVars = null;
				if (st.type == Statement.Type_Catchall)
				{
					lstVars = ((CatchAllStatement)st).GetVars();
				}
				else if (st.type == Statement.Type_Trycatch)
				{
					lstVars = ((CatchStatement)st).GetVars();
				}
				if (lstVars != null)
				{
					foreach (VarExprent var in lstVars)
					{
						implDefVars.Add(var.GetIndex());
						varproc.SetVarName(new VarVersionPair(var), vc.GetFreeName(var.GetIndex()));
						var.SetDefinition(true);
					}
				}
				Sharpen.Collections.AddAll(stack, st.GetStats());
			}
			InitStatement(root);
		}

		public virtual void SetVarDefinitions()
		{
			VarNamesCollector vc = varproc.GetVarNamesCollector();
			foreach (KeyValuePair<int, Statement> en in mapVarDefStatements)
			{
				Statement stat = en.Value;
				int index = en.Key;
				if (implDefVars.Contains(index))
				{
					// already implicitly defined
					continue;
				}
				varproc.SetVarName(new VarVersionPair(index, 0), vc.GetFreeName(index));
				// special case for
				if (stat.type == Statement.Type_Do)
				{
					DoStatement dstat = (DoStatement)stat;
					if (dstat.GetLooptype() == DoStatement.Loop_For)
					{
						if (dstat.GetInitExprent() != null && SetDefinition(dstat.GetInitExprent(), index
							))
						{
							continue;
						}
						else
						{
							List<Exprent> lstSpecial = Sharpen.Arrays.AsList(dstat.GetConditionExprent(), dstat
								.GetIncExprent());
							foreach (VarExprent var in GetAllVars(lstSpecial))
							{
								if (var.GetIndex() == index)
								{
									stat = stat.GetParent();
									break;
								}
							}
						}
					}
				}
				Statement first = FindFirstBlock(stat, index);
				List<Exprent> lst;
				if (first == null)
				{
					lst = stat.GetVarDefinitions();
				}
				else if (first.GetExprents() == null)
				{
					lst = first.GetVarDefinitions();
				}
				else
				{
					lst = first.GetExprents();
				}
				bool defset = false;
				// search for the first assignment to var [index]
				int addindex = 0;
				foreach (Exprent expr in lst)
				{
					if (SetDefinition(expr, index))
					{
						defset = true;
						break;
					}
					else
					{
						bool foundvar = false;
						foreach (Exprent exp in expr.GetAllExprents(true))
						{
							if (exp.type == Exprent.Exprent_Var && ((VarExprent)exp).GetIndex() == index)
							{
								foundvar = true;
								break;
							}
						}
						if (foundvar)
						{
							break;
						}
					}
					addindex++;
				}
				if (!defset)
				{
					VarExprent var = new VarExprent(index, varproc.GetVarType(new VarVersionPair(index
						, 0)), varproc);
					var.SetDefinition(true);
					lst.Add(addindex, var);
				}
			}
		}

		// *****************************************************************************
		// private methods
		// *****************************************************************************
		private Statement FindFirstBlock(Statement stat, int varindex)
		{
			LinkedList<Statement> stack = new LinkedList<Statement>();
			stack.AddLast(stat);
			while (!(stack.Count == 0))
			{
				Statement st = stack.RemoveAtReturningValue(0);
				if ((stack.Count == 0) || mapStatementVars.GetOrNull(st.id).Contains(varindex))
				{
					if (st.IsLabeled() && !(stack.Count == 0))
					{
						return st;
					}
					if (st.GetExprents() != null)
					{
						return st;
					}
					else
					{
						stack.Clear();
						switch (st.type)
						{
							case Statement.Type_Sequence:
							{
								stack = new LinkedList<Statement>(st.GetStats().Concat(stack));
								break;
							}

							case Statement.Type_If:
							case Statement.Type_Root:
							case Statement.Type_Switch:
							case Statement.Type_Syncronized:
							{
								stack.AddLast(st.GetFirst());
								break;
							}

							default:
							{
								return st;
							}
						}
					}
				}
			}
			return null;
		}

		private HashSet<int> InitStatement(Statement stat)
		{
			Dictionary<int, int> mapCount = new Dictionary<int, int>();
			List<VarExprent> condlst;
			if (stat.GetExprents() == null)
			{
				// recurse on children statements
				List<int> childVars = new List<int>();
				List<Exprent> currVars = new List<Exprent>();
				foreach (object obj in stat.GetSequentialObjects())
				{
					if (obj is Statement)
					{
						Statement st = (Statement)obj;
						Sharpen.Collections.AddAll(childVars, InitStatement(st));
						if (st.type == DoStatement.Type_Do)
						{
							DoStatement dost = (DoStatement)st;
							if (dost.GetLooptype() != DoStatement.Loop_For && dost.GetLooptype() != DoStatement
								.Loop_Do)
							{
								currVars.Add(dost.GetConditionExprent());
							}
						}
						else if (st.type == DoStatement.Type_Catchall)
						{
							CatchAllStatement fin = (CatchAllStatement)st;
							if (fin.IsFinally() && fin.GetMonitor() != null)
							{
								currVars.Add(fin.GetMonitor());
							}
						}
					}
					else if (obj is Exprent)
					{
						currVars.Add((Exprent)obj);
					}
				}
				// children statements
				foreach (int index in childVars)
				{
					int? count = mapCount.GetOrNullable(index);
					if (count == null)
					{
						count = 0;
					}
					Sharpen.Collections.Put(mapCount, index, count.Value + 1);
				}
				condlst = GetAllVars(currVars);
			}
			else
			{
				condlst = GetAllVars(stat.GetExprents());
			}
			// this statement
			foreach (VarExprent var in condlst)
			{
				Sharpen.Collections.Put(mapCount, var.GetIndex(), 2);
			}
			HashSet<int> set = new HashSet<int>(mapCount.Keys);
			// put all variables defined in this statement into the set
			foreach (KeyValuePair<int, int> en in mapCount)
			{
				if (en.Value > 1)
				{
					Sharpen.Collections.Put(mapVarDefStatements, en.Key, stat);
				}
			}
			Sharpen.Collections.Put(mapStatementVars, stat.id, set);
			return set;
		}

		private static List<VarExprent> GetAllVars(List<Exprent> lst)
		{
			List<VarExprent> res = new List<VarExprent>();
			List<Exprent> listTemp = new List<Exprent>();
			foreach (Exprent expr in lst)
			{
				Sharpen.Collections.AddAll(listTemp, expr.GetAllExprents(true));
				listTemp.Add(expr);
			}
			foreach (Exprent exprent in listTemp)
			{
				if (exprent.type == Exprent.Exprent_Var)
				{
					res.Add((VarExprent)exprent);
				}
			}
			return res;
		}

		private static bool SetDefinition(Exprent expr, int index)
		{
			if (expr.type == Exprent.Exprent_Assignment)
			{
				Exprent left = ((AssignmentExprent)expr).GetLeft();
				if (left.type == Exprent.Exprent_Var)
				{
					VarExprent var = (VarExprent)left;
					if (var.GetIndex() == index)
					{
						var.SetDefinition(true);
						return true;
					}
				}
			}
			return false;
		}
	}
}
