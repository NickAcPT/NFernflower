// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Sforms;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Struct.Match;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class SimplifyExprentsHelper
	{
		private static readonly MatchEngine class14Builder = new MatchEngine("statement type:if iftype:if exprsize:-1\n"
			 + " exprent position:head type:if\n" + "  exprent type:function functype:eq\n" 
			+ "   exprent type:field name:$fieldname$\n" + "   exprent type:constant consttype:null\n"
			 + " statement type:basicblock\n" + "  exprent position:-1 type:assignment ret:$assignfield$\n"
			 + "   exprent type:var index:$var$\n" + "   exprent type:field name:$fieldname$\n"
			 + " statement type:sequence statsize:2\n" + "  statement type:trycatch\n" + "   statement type:basicblock exprsize:1\n"
			 + "    exprent type:assignment\n" + "     exprent type:var index:$var$\n" + "     exprent type:invocation invclass:java/lang/Class signature:forName(Ljava/lang/String;)Ljava/lang/Class;\n"
			 + "      exprent position:0 type:constant consttype:string constvalue:$classname$\n"
			 + "   statement type:basicblock exprsize:1\n" + "    exprent type:exit exittype:throw\n"
			 + "  statement type:basicblock exprsize:1\n" + "   exprent type:assignment\n" +
			 "    exprent type:field name:$fieldname$ ret:$field$\n" + "    exprent type:var index:$var$"
			);

		private readonly bool firstInvocation;

		public SimplifyExprentsHelper(bool firstInvocation)
		{
			this.firstInvocation = firstInvocation;
		}

		public virtual bool SimplifyStackVarsStatement(Statement stat, HashSet<int> setReorderedIfs
			, SSAConstructorSparseEx ssa, StructClass cl)
		{
			bool res = false;
			List<Exprent> expressions = stat.GetExprents();
			if (expressions == null)
			{
				bool processClass14 = DecompilerContext.GetOption(IIFernflowerPreferences.Decompile_Class_1_4
					);
				while (true)
				{
					bool changed = false;
					foreach (Statement st in stat.GetStats())
					{
						res |= SimplifyStackVarsStatement(st, setReorderedIfs, ssa, cl);
						changed = IfHelper.MergeIfs(st, setReorderedIfs) || BuildIff(st, ssa) || processClass14
							 && CollapseInlinedClass14(st);
						// collapse composed if's
						// collapse iff ?: statement
						// collapse inlined .class property in version 1.4 and before
						if (changed)
						{
							break;
						}
					}
					res |= changed;
					if (!changed)
					{
						break;
					}
				}
			}
			else
			{
				res = SimplifyStackVarsExprents(expressions, cl);
			}
			return res;
		}

		private bool SimplifyStackVarsExprents(List<Exprent> list, StructClass cl)
		{
			bool res = false;
			int index = 0;
			while (index < list.Count)
			{
				Exprent current = list[index];
				Exprent ret = IsSimpleConstructorInvocation(current);
				if (ret != null)
				{
					list[index] = ret;
					res = true;
					continue;
				}
				// lambda expression (Java 8)
				ret = IsLambda(current, cl);
				if (ret != null)
				{
					list[index] = ret;
					res = true;
					continue;
				}
				// remove monitor exit
				if (IsMonitorExit(current))
				{
					list.RemoveAtReturningValue(index);
					res = true;
					continue;
				}
				// trivial assignment of a stack variable
				if (IsTrivialStackAssignment(current))
				{
					list.RemoveAtReturningValue(index);
					res = true;
					continue;
				}
				if (index == list.Count - 1)
				{
					break;
				}
				Exprent next = list[index + 1];
				// constructor invocation
				if (IsConstructorInvocationRemote(list, index))
				{
					list.RemoveAtReturningValue(index);
					res = true;
					continue;
				}
				// remove getClass() invocation, which is part of a qualified new
				if (DecompilerContext.GetOption(IIFernflowerPreferences.Remove_Get_Class_New))
				{
					if (IsQualifiedNewGetClass(current, next))
					{
						list.RemoveAtReturningValue(index);
						res = true;
						continue;
					}
				}
				// direct initialization of an array
				int arrCount = IsArrayInitializer(list, index);
				if (arrCount > 0)
				{
					for (int i = 0; i < arrCount; i++)
					{
						list.RemoveAtReturningValue(index + 1);
					}
					res = true;
					continue;
				}
				// add array initializer expression
				if (AddArrayInitializer(current, next))
				{
					list.RemoveAtReturningValue(index + 1);
					res = true;
					continue;
				}
				// integer ++expr and --expr  (except for vars!)
				Exprent func = IsPPIorMMI(current);
				if (func != null)
				{
					list[index] = func;
					res = true;
					continue;
				}
				// expr++ and expr--
				if (IsIPPorIMM(current, next) || IsIPPorIMM2(current, next))
				{
					list.RemoveAtReturningValue(index + 1);
					res = true;
					continue;
				}
				// assignment on stack
				if (IsStackAssignment(current, next))
				{
					list.RemoveAtReturningValue(index + 1);
					res = true;
					continue;
				}
				if (!firstInvocation && IsStackAssignment2(current, next))
				{
					list.RemoveAtReturningValue(index + 1);
					res = true;
					continue;
				}
				index++;
			}
			return res;
		}

		private static bool AddArrayInitializer(Exprent first, Exprent second)
		{
			if (first.type == Exprent.Exprent_Assignment)
			{
				AssignmentExprent @as = (AssignmentExprent)first;
				if (@as.GetRight().type == Exprent.Exprent_New && @as.GetLeft().type == Exprent.Exprent_Var)
				{
					NewExprent newExpr = (NewExprent)@as.GetRight();
					if (!(newExpr.GetLstArrayElements().Count == 0))
					{
						VarExprent arrVar = (VarExprent)@as.GetLeft();
						if (second.type == Exprent.Exprent_Assignment)
						{
							AssignmentExprent aas = (AssignmentExprent)second;
							if (aas.GetLeft().type == Exprent.Exprent_Array)
							{
								ArrayExprent arrExpr = (ArrayExprent)aas.GetLeft();
								if (arrExpr.GetArray().type == Exprent.Exprent_Var && arrVar.Equals(arrExpr.GetArray
									()) && arrExpr.GetIndex().type == Exprent.Exprent_Const)
								{
									int constValue = ((ConstExprent)arrExpr.GetIndex()).GetIntValue();
									if (constValue < newExpr.GetLstArrayElements().Count)
									{
										Exprent init = newExpr.GetLstArrayElements()[constValue];
										if (init.type == Exprent.Exprent_Const)
										{
											ConstExprent cinit = (ConstExprent)init;
											VarType arrType = newExpr.GetNewType().DecreaseArrayDim();
											ConstExprent defaultVal = ExprProcessor.GetDefaultArrayValue(arrType);
											if (cinit.Equals(defaultVal))
											{
												Exprent tempExpr = aas.GetRight();
												if (!tempExpr.ContainsExprent(arrVar))
												{
													newExpr.GetLstArrayElements()[constValue] = tempExpr;
													if (tempExpr.type == Exprent.Exprent_New)
													{
														NewExprent tempNewExpr = (NewExprent)tempExpr;
														int dims = newExpr.GetNewType().arrayDim;
														if (dims > 1 && !(tempNewExpr.GetLstArrayElements().Count == 0))
														{
															tempNewExpr.SetDirectArrayInit(true);
														}
													}
													return true;
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
			return false;
		}

		private static int IsArrayInitializer(List<Exprent> list, int index)
		{
			Exprent current = list[index];
			if (current.type == Exprent.Exprent_Assignment)
			{
				AssignmentExprent @as = (AssignmentExprent)current;
				if (@as.GetRight().type == Exprent.Exprent_New && @as.GetLeft().type == Exprent.Exprent_Var)
				{
					NewExprent newExpr = (NewExprent)@as.GetRight();
					if (newExpr.GetExprType().arrayDim > 0 && newExpr.GetLstDims().Count == 1 && (newExpr
						.GetLstArrayElements().Count == 0) && newExpr.GetLstDims()[0].type == Exprent.Exprent_Const)
					{
						int size = (int)((ConstExprent)newExpr.GetLstDims()[0]).GetValue();
						if (size == 0)
						{
							return 0;
						}
						VarExprent arrVar = (VarExprent)@as.GetLeft();
						IDictionary<int, Exprent> mapInit = new Dictionary<int, Exprent>();
						int i = 1;
						while (index + i < list.Count && i <= size)
						{
							bool found = false;
							Exprent expr = list[index + i];
							if (expr.type == Exprent.Exprent_Assignment)
							{
								AssignmentExprent aas = (AssignmentExprent)expr;
								if (aas.GetLeft().type == Exprent.Exprent_Array)
								{
									ArrayExprent arrExpr = (ArrayExprent)aas.GetLeft();
									if (arrExpr.GetArray().type == Exprent.Exprent_Var && arrVar.Equals(arrExpr.GetArray
										()) && arrExpr.GetIndex().type == Exprent.Exprent_Const)
									{
										// TODO: check for a number type. Failure extremely improbable, but nevertheless...
										int constValue = ((ConstExprent)arrExpr.GetIndex()).GetIntValue();
										if (constValue < size && !mapInit.ContainsKey(constValue))
										{
											if (!aas.GetRight().ContainsExprent(arrVar))
											{
												Sharpen.Collections.Put(mapInit, constValue, aas.GetRight());
												found = true;
											}
										}
									}
								}
							}
							if (!found)
							{
								break;
							}
							i++;
						}
						double fraction = ((double)mapInit.Count) / size;
						if ((arrVar.IsStack() && fraction > 0) || (size <= 7 && fraction >= 0.3) || (size
							 > 7 && fraction >= 0.7))
						{
							List<Exprent> lstRet = new List<Exprent>();
							VarType arrayType = newExpr.GetNewType().DecreaseArrayDim();
							ConstExprent defaultVal = ExprProcessor.GetDefaultArrayValue(arrayType);
							for (int j = 0; j < size; j++)
							{
								lstRet.Add(defaultVal.Copy());
							}
							int dims = newExpr.GetNewType().arrayDim;
							foreach (KeyValuePair<int, Exprent> ent in mapInit)
							{
								Exprent tempExpr = ent.Value;
								lstRet[ent.Key] = tempExpr;
								if (tempExpr.type == Exprent.Exprent_New)
								{
									NewExprent tempNewExpr = (NewExprent)tempExpr;
									if (dims > 1 && !(tempNewExpr.GetLstArrayElements().Count == 0))
									{
										tempNewExpr.SetDirectArrayInit(true);
									}
								}
							}
							newExpr.SetLstArrayElements(lstRet);
							return mapInit.Count;
						}
					}
				}
			}
			return 0;
		}

		private static bool IsTrivialStackAssignment(Exprent first)
		{
			if (first.type == Exprent.Exprent_Assignment)
			{
				AssignmentExprent asf = (AssignmentExprent)first;
				if (asf.GetLeft().type == Exprent.Exprent_Var && asf.GetRight().type == Exprent.Exprent_Var)
				{
					VarExprent left = (VarExprent)asf.GetLeft();
					VarExprent right = (VarExprent)asf.GetRight();
					return left.GetIndex() == right.GetIndex() && left.IsStack() && right.IsStack();
				}
			}
			return false;
		}

		private static bool IsStackAssignment2(Exprent first, Exprent second)
		{
			// e.g. 1.4-style class invocation
			if (first.type == Exprent.Exprent_Assignment && second.type == Exprent.Exprent_Assignment)
			{
				AssignmentExprent asf = (AssignmentExprent)first;
				AssignmentExprent ass = (AssignmentExprent)second;
				if (asf.GetLeft().type == Exprent.Exprent_Var && ass.GetRight().type == Exprent.Exprent_Var
					 && asf.GetLeft().Equals(ass.GetRight()) && ((VarExprent)asf.GetLeft()).IsStack(
					))
				{
					if (ass.GetLeft().type != Exprent.Exprent_Var || !((VarExprent)ass.GetLeft()).IsStack
						())
					{
						asf.SetRight(new AssignmentExprent(ass.GetLeft(), asf.GetRight(), ass.bytecode));
						return true;
					}
				}
			}
			return false;
		}

		private static bool IsStackAssignment(Exprent first, Exprent second)
		{
			if (first.type == Exprent.Exprent_Assignment && second.type == Exprent.Exprent_Assignment)
			{
				AssignmentExprent asf = (AssignmentExprent)first;
				AssignmentExprent ass = (AssignmentExprent)second;
				while (true)
				{
					if (asf.GetRight().Equals(ass.GetRight()))
					{
						if ((asf.GetLeft().type == Exprent.Exprent_Var && ((VarExprent)asf.GetLeft()).IsStack
							()) && (ass.GetLeft().type != Exprent.Exprent_Var || !((VarExprent)ass.GetLeft()
							).IsStack()))
						{
							if (!ass.GetLeft().ContainsExprent(asf.GetLeft()))
							{
								asf.SetRight(ass);
								return true;
							}
						}
					}
					if (asf.GetRight().type == Exprent.Exprent_Assignment)
					{
						asf = (AssignmentExprent)asf.GetRight();
					}
					else
					{
						break;
					}
				}
			}
			return false;
		}

		private static Exprent IsPPIorMMI(Exprent first)
		{
			if (first.type == Exprent.Exprent_Assignment)
			{
				AssignmentExprent @as = (AssignmentExprent)first;
				if (@as.GetRight().type == Exprent.Exprent_Function)
				{
					FunctionExprent func = (FunctionExprent)@as.GetRight();
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
							if (left.type != Exprent.Exprent_Var && left.Equals(econd))
							{
								int type = func.GetFuncType() == FunctionExprent.Function_Add ? FunctionExprent.Function_Ppi
									 : FunctionExprent.Function_Mmi;
								FunctionExprent ret = new FunctionExprent(type, econd, func.bytecode);
								ret.SetImplicitType(VarType.Vartype_Int);
								return ret;
							}
						}
					}
				}
			}
			return null;
		}

		private static bool IsIPPorIMM(Exprent first, Exprent second)
		{
			if (first.type == Exprent.Exprent_Assignment && second.type == Exprent.Exprent_Function)
			{
				AssignmentExprent @as = (AssignmentExprent)first;
				FunctionExprent @in = (FunctionExprent)second;
				if ((@in.GetFuncType() == FunctionExprent.Function_Mmi || @in.GetFuncType() == FunctionExprent
					.Function_Ppi) && @in.GetLstOperands()[0].Equals(@as.GetRight()))
				{
					if (@in.GetFuncType() == FunctionExprent.Function_Mmi)
					{
						@in.SetFuncType(FunctionExprent.Function_Imm);
					}
					else
					{
						@in.SetFuncType(FunctionExprent.Function_Ipp);
					}
					@as.SetRight(@in);
					return true;
				}
			}
			return false;
		}

		private static bool IsIPPorIMM2(Exprent first, Exprent second)
		{
			if (first.type != Exprent.Exprent_Assignment || second.type != Exprent.Exprent_Assignment)
			{
				return false;
			}
			AssignmentExprent af = (AssignmentExprent)first;
			AssignmentExprent @as = (AssignmentExprent)second;
			if (@as.GetRight().type != Exprent.Exprent_Function)
			{
				return false;
			}
			FunctionExprent func = (FunctionExprent)@as.GetRight();
			if (func.GetFuncType() != FunctionExprent.Function_Add && func.GetFuncType() != FunctionExprent
				.Function_Sub)
			{
				return false;
			}
			Exprent econd = func.GetLstOperands()[0];
			Exprent econst = func.GetLstOperands()[1];
			if (econst.type != Exprent.Exprent_Const && econd.type == Exprent.Exprent_Const &&
				 func.GetFuncType() == FunctionExprent.Function_Add)
			{
				econd = econst;
				econst = func.GetLstOperands()[0];
			}
			if (econst.type == Exprent.Exprent_Const && ((ConstExprent)econst).HasValueOne() 
				&& af.GetLeft().Equals(econd) && af.GetRight().Equals(@as.GetLeft()) && (af.GetLeft
				().GetExprentUse() & Exprent.Multiple_Uses) != 0)
			{
				int type = func.GetFuncType() == FunctionExprent.Function_Add ? FunctionExprent.Function_Ipp
					 : FunctionExprent.Function_Imm;
				FunctionExprent ret = new FunctionExprent(type, af.GetRight(), func.bytecode);
				ret.SetImplicitType(VarType.Vartype_Int);
				af.SetRight(ret);
				return true;
			}
			return false;
		}

		private static bool IsMonitorExit(Exprent first)
		{
			if (first.type == Exprent.Exprent_Monitor)
			{
				MonitorExprent expr = (MonitorExprent)first;
				return expr.GetMonType() == MonitorExprent.Monitor_Exit && expr.GetValue().type ==
					 Exprent.Exprent_Var && !((VarExprent)expr.GetValue()).IsStack();
			}
			return false;
		}

		private static bool IsQualifiedNewGetClass(Exprent first, Exprent second)
		{
			if (first.type == Exprent.Exprent_Invocation)
			{
				InvocationExprent invocation = (InvocationExprent)first;
				if (!invocation.IsStatic() && invocation.GetInstance().type == Exprent.Exprent_Var
					 && invocation.GetName().Equals("getClass") && invocation.GetStringDescriptor().
					Equals("()Ljava/lang/Class;"))
				{
					List<Exprent> lstExprents = second.GetAllExprents();
					lstExprents.Add(second);
					foreach (Exprent expr in lstExprents)
					{
						if (expr.type == Exprent.Exprent_New)
						{
							NewExprent newExpr = (NewExprent)expr;
							if (newExpr.GetConstructor() != null && !(newExpr.GetConstructor().GetLstParameters
								().Count == 0) && newExpr.GetConstructor().GetLstParameters()[0].Equals(invocation
								.GetInstance()))
							{
								string classname = newExpr.GetNewType().value;
								ClassesProcessor.ClassNode node = DecompilerContext.GetClassProcessor().GetMapRootClasses
									().GetOrNull(classname);
								if (node != null && node.type != ClassesProcessor.ClassNode.Class_Root)
								{
									return true;
								}
							}
						}
					}
				}
			}
			return false;
		}

		// propagate (var = new X) forward to the <init> invocation
		private static bool IsConstructorInvocationRemote(List<Exprent> list, int index)
		{
			Exprent current = list[index];
			if (current.type == Exprent.Exprent_Assignment)
			{
				AssignmentExprent @as = (AssignmentExprent)current;
				if (@as.GetLeft().type == Exprent.Exprent_Var && @as.GetRight().type == Exprent.Exprent_New)
				{
					NewExprent newExpr = (NewExprent)@as.GetRight();
					VarType newType = newExpr.GetNewType();
					VarVersionPair leftPair = new VarVersionPair((VarExprent)@as.GetLeft());
					if (newType.type == ICodeConstants.Type_Object && newType.arrayDim == 0 && newExpr
						.GetConstructor() == null)
					{
						for (int i = index + 1; i < list.Count; i++)
						{
							Exprent remote = list[i];
							// <init> invocation
							if (remote.type == Exprent.Exprent_Invocation)
							{
								InvocationExprent @in = (InvocationExprent)remote;
								if (@in.GetFunctype() == InvocationExprent.Typ_Init && @in.GetInstance().type == 
									Exprent.Exprent_Var && @as.GetLeft().Equals(@in.GetInstance()))
								{
									newExpr.SetConstructor(@in);
									@in.SetInstance(null);
									list[i] = @as.Copy();
									return true;
								}
							}
							// check for variable in use
							HashSet<VarVersionPair> setVars = remote.GetAllVariables();
							if (setVars.Contains(leftPair))
							{
								// variable used somewhere in between -> exit, need a better reduced code
								return false;
							}
						}
					}
				}
			}
			return false;
		}

		private static Exprent IsLambda(Exprent exprent, StructClass cl)
		{
			List<Exprent> lst = exprent.GetAllExprents();
			foreach (Exprent expr in lst)
			{
				Exprent ret = IsLambda(expr, cl);
				if (ret != null)
				{
					exprent.ReplaceExprent(expr, ret);
				}
			}
			if (exprent.type == Exprent.Exprent_Invocation)
			{
				InvocationExprent @in = (InvocationExprent)exprent;
				if (@in.GetInvocationTyp() == InvocationExprent.Invoke_Dynamic)
				{
					string lambda_class_name = cl.qualifiedName + @in.GetInvokeDynamicClassSuffix();
					ClassesProcessor.ClassNode lambda_class = DecompilerContext.GetClassProcessor().GetMapRootClasses
						().GetOrNull(lambda_class_name);
					if (lambda_class != null)
					{
						// real lambda class found, replace invocation with an anonymous class
						NewExprent newExpr = new NewExprent(new VarType(lambda_class_name, true), null, 0
							, @in.bytecode);
						newExpr.SetConstructor(@in);
						// note: we don't set the instance to null with in.setInstance(null) like it is done for a common constructor invocation
						// lambda can also be a reference to a virtual method (e.g. String x; ...(x::toString);)
						// in this case instance will hold the corresponding object
						return newExpr;
					}
				}
			}
			return null;
		}

		private static Exprent IsSimpleConstructorInvocation(Exprent exprent)
		{
			List<Exprent> lst = exprent.GetAllExprents();
			foreach (Exprent expr in lst)
			{
				Exprent ret = IsSimpleConstructorInvocation(expr);
				if (ret != null)
				{
					exprent.ReplaceExprent(expr, ret);
				}
			}
			if (exprent.type == Exprent.Exprent_Invocation)
			{
				InvocationExprent @in = (InvocationExprent)exprent;
				if (@in.GetFunctype() == InvocationExprent.Typ_Init && @in.GetInstance().type == 
					Exprent.Exprent_New)
				{
					NewExprent newExpr = (NewExprent)@in.GetInstance();
					newExpr.SetConstructor(@in);
					@in.SetInstance(null);
					return newExpr;
				}
			}
			return null;
		}

		private static bool BuildIff(Statement stat, SSAConstructorSparseEx ssa)
		{
			if (stat.type == Statement.Type_If && stat.GetExprents() == null)
			{
				IfStatement statement = (IfStatement)stat;
				Exprent ifHeadExpr = statement.GetHeadexprent();
				HashSet<int> ifHeadExprBytecode = (ifHeadExpr == null ? null : ifHeadExpr.bytecode
					);
				if (statement.iftype == IfStatement.Iftype_Ifelse)
				{
					Statement ifStatement = statement.GetIfstat();
					Statement elseStatement = statement.GetElsestat();
					if (ifStatement.GetExprents() != null && ifStatement.GetExprents().Count == 1 && 
						elseStatement.GetExprents() != null && elseStatement.GetExprents().Count == 1 &&
						 ifStatement.GetAllSuccessorEdges().Count == 1 && elseStatement.GetAllSuccessorEdges
						().Count == 1 && ifStatement.GetAllSuccessorEdges()[0].GetDestination() == elseStatement
						.GetAllSuccessorEdges()[0].GetDestination())
					{
						Exprent ifExpr = ifStatement.GetExprents()[0];
						Exprent elseExpr = elseStatement.GetExprents()[0];
						if (ifExpr.type == Exprent.Exprent_Assignment && elseExpr.type == Exprent.Exprent_Assignment)
						{
							AssignmentExprent ifAssign = (AssignmentExprent)ifExpr;
							AssignmentExprent elseAssign = (AssignmentExprent)elseExpr;
							if (ifAssign.GetLeft().type == Exprent.Exprent_Var && elseAssign.GetLeft().type ==
								 Exprent.Exprent_Var)
							{
								VarExprent ifVar = (VarExprent)ifAssign.GetLeft();
								VarExprent elseVar = (VarExprent)elseAssign.GetLeft();
								if (ifVar.GetIndex() == elseVar.GetIndex() && ifVar.IsStack())
								{
									// ifVar.getIndex() >= VarExprent.STACK_BASE) {
									bool found = false;
									foreach (KeyValuePair<VarVersionPair, FastSparseSetFactory.FastSparseSet<int>> ent
										 in ssa.GetPhi())
									{
										if (ent.Key.var == ifVar.GetIndex())
										{
											if (ent.Value.Contains(ifVar.GetVersion()) && ent.Value.Contains(elseVar.GetVersion
												()))
											{
												found = true;
												break;
											}
										}
									}
									if (found)
									{
										List<Exprent> data = new List<Exprent>(statement.GetFirst().GetExprents());
										List<Exprent> operands = Sharpen.Arrays.AsList(statement.GetHeadexprent().GetCondition
											(), ifAssign.GetRight(), elseAssign.GetRight());
										data.Add(new AssignmentExprent(ifVar, new FunctionExprent(FunctionExprent.Function_Iif
											, operands, ifHeadExprBytecode), ifHeadExprBytecode));
										statement.SetExprents(data);
										if ((statement.GetAllSuccessorEdges().Count == 0))
										{
											StatEdge ifEdge = ifStatement.GetAllSuccessorEdges()[0];
											StatEdge edge = new StatEdge(ifEdge.GetType(), statement, ifEdge.GetDestination()
												);
											statement.AddSuccessor(edge);
											if (ifEdge.closure != null)
											{
												ifEdge.closure.AddLabeledEdge(edge);
											}
										}
										SequenceHelper.DestroyAndFlattenStatement(statement);
										return true;
									}
								}
							}
						}
						else if (ifExpr.type == Exprent.Exprent_Exit && elseExpr.type == Exprent.Exprent_Exit)
						{
							ExitExprent ifExit = (ExitExprent)ifExpr;
							ExitExprent elseExit = (ExitExprent)elseExpr;
							if (ifExit.GetExitType() == elseExit.GetExitType() && ifExit.GetValue() != null &&
								 elseExit.GetValue() != null && ifExit.GetExitType() == ExitExprent.Exit_Return)
							{
								// throw is dangerous, because of implicit casting to a common superclass
								// e.g. throws IOException and throw true?new RuntimeException():new IOException(); won't work
								if (ifExit.GetExitType() == ExitExprent.Exit_Throw && !ifExit.GetValue().GetExprType
									().Equals(elseExit.GetValue().GetExprType()))
								{
									// note: getExprType unreliable at this point!
									return false;
								}
								// avoid flattening to 'iff' if any of the branches is an 'iff' already
								if (IsIff(ifExit.GetValue()) || IsIff(elseExit.GetValue()))
								{
									return false;
								}
								List<Exprent> data = new List<Exprent>(statement.GetFirst().GetExprents());
								data.Add(new ExitExprent(ifExit.GetExitType(), new FunctionExprent(FunctionExprent
									.Function_Iif, Sharpen.Arrays.AsList(statement.GetHeadexprent().GetCondition(), 
									ifExit.GetValue(), elseExit.GetValue()), ifHeadExprBytecode), ifExit.GetRetType(
									), ifHeadExprBytecode));
								statement.SetExprents(data);
								StatEdge retEdge = ifStatement.GetAllSuccessorEdges()[0];
								Statement closure = retEdge.closure == statement ? statement.GetParent() : retEdge
									.closure;
								statement.AddSuccessor(new StatEdge(StatEdge.Type_Break, statement, retEdge.GetDestination
									(), closure));
								SequenceHelper.DestroyAndFlattenStatement(statement);
								return true;
							}
						}
					}
				}
			}
			return false;
		}

		private static bool IsIff(Exprent exp)
		{
			return exp.type == Exprent.Exprent_Function && ((FunctionExprent)exp).GetFuncType
				() == FunctionExprent.Function_Iif;
		}

		private static bool CollapseInlinedClass14(Statement stat)
		{
			bool ret = class14Builder.Match(stat);
			if (ret)
			{
				string class_name = (string)class14Builder.GetVariableValue("$classname$");
				AssignmentExprent assignment = (AssignmentExprent)class14Builder.GetVariableValue
					("$assignfield$");
				FieldExprent fieldExpr = (FieldExprent)class14Builder.GetVariableValue("$field$");
				assignment.ReplaceExprent(assignment.GetRight(), new ConstExprent(VarType.Vartype_Class
					, class_name, null));
				List<Exprent> data = new List<Exprent>(stat.GetFirst().GetExprents());
				stat.SetExprents(data);
				SequenceHelper.DestroyAndFlattenStatement(stat);
				ClassWrapper wrapper = (ClassWrapper)DecompilerContext.GetProperty(DecompilerContext
					.Current_Class_Wrapper);
				if (wrapper != null)
				{
					wrapper.GetHiddenMembers().Add(InterpreterUtil.MakeUniqueKey(fieldExpr.GetName(), 
						fieldExpr.GetDescriptor().descriptorString));
				}
			}
			return ret;
		}
	}
}
