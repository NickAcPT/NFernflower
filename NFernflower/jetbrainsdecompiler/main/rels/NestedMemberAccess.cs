// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Sforms;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main.Rels
{
	public class NestedMemberAccess
	{
		[System.Serializable]
		private sealed class MethodAccess : Sharpen.EnumBase
		{
			public static readonly NestedMemberAccess.MethodAccess Normal = new NestedMemberAccess.MethodAccess
				(0, "NORMAL");

			public static readonly NestedMemberAccess.MethodAccess Field_Get = new NestedMemberAccess.MethodAccess
				(1, "FIELD_GET");

			public static readonly NestedMemberAccess.MethodAccess Field_Set = new NestedMemberAccess.MethodAccess
				(2, "FIELD_SET");

			public static readonly NestedMemberAccess.MethodAccess Method = new NestedMemberAccess.MethodAccess
				(3, "METHOD");

			public static readonly NestedMemberAccess.MethodAccess Function = new NestedMemberAccess.MethodAccess
				(4, "FUNCTION");

			private MethodAccess(int ordinal, string name)
				: base(ordinal, name)
			{
			}

			public static MethodAccess[] Values()
			{
				return new MethodAccess[] { Normal, Field_Get, Field_Set, Method, Function };
			}

			static MethodAccess()
			{
				RegisterValues<MethodAccess>(Values());
			}
		}

		private bool noSynthFlag;

		private readonly Dictionary<MethodWrapper, NestedMemberAccess.MethodAccess> mapMethodType
			 = new Dictionary<MethodWrapper, NestedMemberAccess.MethodAccess>();

		public virtual void PropagateMemberAccess(ClassesProcessor.ClassNode root)
		{
			if ((root.nested.Count == 0))
			{
				return;
			}
			noSynthFlag = DecompilerContext.GetOption(IFernflowerPreferences.Synthetic_Not_Set
				);
			ComputeMethodTypes(root);
			EliminateStaticAccess(root);
		}

		private void ComputeMethodTypes(ClassesProcessor.ClassNode node)
		{
			if (node.type == ClassesProcessor.ClassNode.Class_Lambda)
			{
				return;
			}
			foreach (ClassesProcessor.ClassNode nd in node.nested)
			{
				ComputeMethodTypes(nd);
			}
			foreach (MethodWrapper method in node.GetWrapper().GetMethods())
			{
				ComputeMethodType(node, method);
			}
		}

		private void ComputeMethodType(ClassesProcessor.ClassNode node, MethodWrapper method
			)
		{
			NestedMemberAccess.MethodAccess type = NestedMemberAccess.MethodAccess.Normal;
			if (method.root != null)
			{
				DirectGraph graph = method.GetOrBuildGraph();
				StructMethod mt = method.methodStruct;
				if ((noSynthFlag || mt.IsSynthetic()) && mt.HasModifier(ICodeConstants.Acc_Static
					))
				{
					if (graph.nodes.Count == 2)
					{
						// incl. dummy exit node
						if (graph.first.exprents.Count == 1)
						{
							Exprent exprent = graph.first.exprents[0];
							MethodDescriptor mtdesc = MethodDescriptor.ParseDescriptor(mt.GetDescriptor());
							int parcount = mtdesc.@params.Length;
							Exprent exprCore = exprent;
							if (exprent.type == Exprent.Exprent_Exit)
							{
								ExitExprent exexpr = (ExitExprent)exprent;
								if (exexpr.GetExitType() == ExitExprent.Exit_Return && exexpr.GetValue() != null)
								{
									exprCore = exexpr.GetValue();
								}
							}
							switch (exprCore.type)
							{
								case Exprent.Exprent_Field:
								{
									FieldExprent fexpr = (FieldExprent)exprCore;
									if ((parcount == 1 && !fexpr.IsStatic()) || (parcount == 0 && fexpr.IsStatic()))
									{
										if (fexpr.GetClassname().Equals(node.classStruct.qualifiedName))
										{
											// FIXME: check for private flag of the field
											if (fexpr.IsStatic() || (fexpr.GetInstance().type == Exprent.Exprent_Var && ((VarExprent
												)fexpr.GetInstance()).GetIndex() == 0))
											{
												type = NestedMemberAccess.MethodAccess.Field_Get;
											}
										}
									}
									break;
								}

								case Exprent.Exprent_Var:
								{
									// qualified this
									if (parcount == 1)
									{
										// this or final variable
										if (((VarExprent)exprCore).GetIndex() != 0)
										{
											type = NestedMemberAccess.MethodAccess.Field_Get;
										}
									}
									break;
								}

								case Exprent.Exprent_Function:
								{
									// for now detect only increment/decrement
									FunctionExprent functionExprent = (FunctionExprent)exprCore;
									if (functionExprent.GetFuncType() >= FunctionExprent.Function_Imm && functionExprent
										.GetFuncType() <= FunctionExprent.Function_Ppi)
									{
										if (functionExprent.GetLstOperands()[0].type == Exprent.Exprent_Field)
										{
											type = NestedMemberAccess.MethodAccess.Function;
										}
									}
									break;
								}

								case Exprent.Exprent_Invocation:
								{
									type = NestedMemberAccess.MethodAccess.Method;
									break;
								}

								case Exprent.Exprent_Assignment:
								{
									AssignmentExprent asexpr = (AssignmentExprent)exprCore;
									if (asexpr.GetLeft().type == Exprent.Exprent_Field && asexpr.GetRight().type == Exprent
										.Exprent_Var)
									{
										FieldExprent fexpras = (FieldExprent)asexpr.GetLeft();
										if ((parcount == 2 && !fexpras.IsStatic()) || (parcount == 1 && fexpras.IsStatic(
											)))
										{
											if (fexpras.GetClassname().Equals(node.classStruct.qualifiedName))
											{
												// FIXME: check for private flag of the field
												if (fexpras.IsStatic() || (fexpras.GetInstance().type == Exprent.Exprent_Var && (
													(VarExprent)fexpras.GetInstance()).GetIndex() == 0))
												{
													if (((VarExprent)asexpr.GetRight()).GetIndex() == parcount - 1)
													{
														type = NestedMemberAccess.MethodAccess.Field_Set;
													}
												}
											}
										}
									}
									break;
								}
							}
							if (type == NestedMemberAccess.MethodAccess.Method)
							{
								// FIXME: check for private flag of the method
								type = NestedMemberAccess.MethodAccess.Normal;
								InvocationExprent invexpr = (InvocationExprent)exprCore;
								bool isStatic = invexpr.IsStatic();
								if ((isStatic && invexpr.GetLstParameters().Count == parcount) || (!isStatic && invexpr
									.GetInstance().type == Exprent.Exprent_Var && ((VarExprent)invexpr.GetInstance()
									).GetIndex() == 0 && invexpr.GetLstParameters().Count == parcount - 1))
								{
									bool equalpars = true;
									int index = isStatic ? 0 : 1;
									for (int i = 0; i < invexpr.GetLstParameters().Count; i++)
									{
										Exprent parexpr = invexpr.GetLstParameters()[i];
										if (parexpr.type != Exprent.Exprent_Var || ((VarExprent)parexpr).GetIndex() != index)
										{
											equalpars = false;
											break;
										}
										index += mtdesc.@params[i + (isStatic ? 0 : 1)].stackSize;
									}
									if (equalpars)
									{
										type = NestedMemberAccess.MethodAccess.Method;
									}
								}
							}
						}
						else if (graph.first.exprents.Count == 2)
						{
							Exprent exprentFirst = graph.first.exprents[0];
							Exprent exprentSecond = graph.first.exprents[1];
							if (exprentFirst.type == Exprent.Exprent_Assignment && exprentSecond.type == Exprent
								.Exprent_Exit)
							{
								MethodDescriptor mtdesc = MethodDescriptor.ParseDescriptor(mt.GetDescriptor());
								int parcount = mtdesc.@params.Length;
								AssignmentExprent asexpr = (AssignmentExprent)exprentFirst;
								if (asexpr.GetLeft().type == Exprent.Exprent_Field && asexpr.GetRight().type == Exprent
									.Exprent_Var)
								{
									FieldExprent fexpras = (FieldExprent)asexpr.GetLeft();
									if ((parcount == 2 && !fexpras.IsStatic()) || (parcount == 1 && fexpras.IsStatic(
										)))
									{
										if (fexpras.GetClassname().Equals(node.classStruct.qualifiedName))
										{
											// FIXME: check for private flag of the field
											if (fexpras.IsStatic() || (fexpras.GetInstance().type == Exprent.Exprent_Var && (
												(VarExprent)fexpras.GetInstance()).GetIndex() == 0))
											{
												if (((VarExprent)asexpr.GetRight()).GetIndex() == parcount - 1)
												{
													ExitExprent exexpr = (ExitExprent)exprentSecond;
													if (exexpr.GetExitType() == ExitExprent.Exit_Return && exexpr.GetValue() != null)
													{
														if (exexpr.GetValue().type == Exprent.Exprent_Var && ((VarExprent)asexpr.GetRight
															()).GetIndex() == parcount - 1)
														{
															type = NestedMemberAccess.MethodAccess.Field_Set;
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
				}
			}
			if (type != NestedMemberAccess.MethodAccess.Normal)
			{
				Sharpen.Collections.Put(mapMethodType, method, type);
			}
			else
			{
				Sharpen.Collections.Remove(mapMethodType, method);
			}
		}

		private void EliminateStaticAccess(ClassesProcessor.ClassNode node)
		{
			if (node.type == ClassesProcessor.ClassNode.Class_Lambda)
			{
				return;
			}
			foreach (MethodWrapper meth in node.GetWrapper().GetMethods())
			{
				if (meth.root != null)
				{
					bool replaced = false;
					DirectGraph graph = meth.GetOrBuildGraph();
					HashSet<DirectNode> setVisited = new HashSet<DirectNode>();
					LinkedList<DirectNode> stack = new LinkedList<DirectNode>();
					stack.AddLast(graph.first);
					while (!(stack.Count == 0))
					{
						// TODO: replace with interface iterator?
						DirectNode nd = Sharpen.Collections.RemoveFirst(stack);
						if (setVisited.Contains(nd))
						{
							continue;
						}
						setVisited.Add(nd);
						for (int i = 0; i < nd.exprents.Count; i++)
						{
							Exprent exprent = nd.exprents[i];
							replaced |= ReplaceInvocations(node, meth, exprent);
							if (exprent.type == Exprent.Exprent_Invocation)
							{
								Exprent ret = ReplaceAccessExprent(node, meth, (InvocationExprent)exprent);
								if (ret != null)
								{
									nd.exprents[i] = ret;
									replaced = true;
								}
							}
						}
						Sharpen.Collections.AddAll(stack, nd.succs);
					}
					if (replaced)
					{
						ComputeMethodType(node, meth);
					}
				}
			}
			foreach (ClassesProcessor.ClassNode child in node.nested)
			{
				EliminateStaticAccess(child);
			}
		}

		private bool ReplaceInvocations(ClassesProcessor.ClassNode caller, MethodWrapper 
			meth, Exprent exprent)
		{
			bool res = false;
			foreach (Exprent expr in exprent.GetAllExprents())
			{
				res |= ReplaceInvocations(caller, meth, expr);
			}
			while (true)
			{
				bool found = false;
				foreach (Exprent expr in exprent.GetAllExprents())
				{
					if (expr.type == Exprent.Exprent_Invocation)
					{
						Exprent newexpr = ReplaceAccessExprent(caller, meth, (InvocationExprent)expr);
						if (newexpr != null)
						{
							exprent.ReplaceExprent(expr, newexpr);
							found = true;
							res = true;
							break;
						}
					}
				}
				if (!found)
				{
					break;
				}
			}
			return res;
		}

		private static bool SameTree(ClassesProcessor.ClassNode caller, ClassesProcessor.ClassNode
			 callee)
		{
			if (caller.classStruct.qualifiedName.Equals(callee.classStruct.qualifiedName))
			{
				return false;
			}
			while (caller.parent != null)
			{
				caller = caller.parent;
			}
			while (callee.parent != null)
			{
				callee = callee.parent;
			}
			return caller == callee;
		}

		private Exprent ReplaceAccessExprent(ClassesProcessor.ClassNode caller, MethodWrapper
			 methdest, InvocationExprent invexpr)
		{
			ClassesProcessor.ClassNode node = DecompilerContext.GetClassProcessor().GetMapRootClasses
				().GetOrNull(invexpr.GetClassname());
			MethodWrapper methsource = null;
			if (node != null && node.GetWrapper() != null)
			{
				methsource = node.GetWrapper().GetMethodWrapper(invexpr.GetName(), invexpr.GetStringDescriptor
					());
			}
			if (methsource == null || !mapMethodType.ContainsKey(methsource))
			{
				return null;
			}
			// if same method, return
			if (node.classStruct.qualifiedName.Equals(caller.classStruct.qualifiedName) && methsource
				.methodStruct.GetName().Equals(methdest.methodStruct.GetName()) && methsource.methodStruct
				.GetDescriptor().Equals(methdest.methodStruct.GetDescriptor()))
			{
				// no recursive invocations permitted!
				return null;
			}
			NestedMemberAccess.MethodAccess type = mapMethodType.GetOrNull(methsource);
			//		// FIXME: impossible case. MethodAccess.NORMAL is not saved in the map
			//		if(type == MethodAccess.NORMAL) {
			//			return null;
			//		}
			if (!SameTree(caller, node))
			{
				return null;
			}
			DirectGraph graph = methsource.GetOrBuildGraph();
			Exprent source = graph.first.exprents[0];
			Exprent retexprent = null;
			switch (type.ordinal())
			{
				case 1:
				{
					ExitExprent exsource = (ExitExprent)source;
					if (exsource.GetValue().type == Exprent.Exprent_Var)
					{
						// qualified this
						VarExprent var = (VarExprent)exsource.GetValue();
						string varname = methsource.varproc.GetVarName(new VarVersionPair(var));
						if (!methdest.setOuterVarNames.Contains(varname))
						{
							VarNamesCollector vnc = new VarNamesCollector();
							vnc.AddName(varname);
							methdest.varproc.RefreshVarNames(vnc);
							methdest.setOuterVarNames.Add(varname);
						}
						int index = methdest.counter.GetCounterAndIncrement(CounterContainer.Var_Counter);
						VarExprent ret = new VarExprent(index, var.GetVarType(), methdest.varproc);
						methdest.varproc.SetVarName(new VarVersionPair(index, 0), varname);
						retexprent = ret;
					}
					else
					{
						// field
						FieldExprent ret = (FieldExprent)exsource.GetValue().Copy();
						if (!ret.IsStatic())
						{
							ret.ReplaceExprent(ret.GetInstance(), invexpr.GetLstParameters()[0]);
						}
						retexprent = ret;
					}
					break;
				}

				case 2:
				{
					AssignmentExprent ret_1;
					if (source.type == Exprent.Exprent_Exit)
					{
						ExitExprent extex = (ExitExprent)source;
						ret_1 = (AssignmentExprent)extex.GetValue().Copy();
					}
					else
					{
						ret_1 = (AssignmentExprent)source.Copy();
					}
					FieldExprent fexpr = (FieldExprent)ret_1.GetLeft();
					if (fexpr.IsStatic())
					{
						ret_1.ReplaceExprent(ret_1.GetRight(), invexpr.GetLstParameters()[0]);
					}
					else
					{
						ret_1.ReplaceExprent(ret_1.GetRight(), invexpr.GetLstParameters()[1]);
						fexpr.ReplaceExprent(fexpr.GetInstance(), invexpr.GetLstParameters()[0]);
					}
					// do not use copied bytecodes
					ret_1.GetLeft().bytecode = null;
					ret_1.GetRight().bytecode = null;
					retexprent = ret_1;
					break;
				}

				case 4:
				{
					retexprent = ReplaceFunction(invexpr, source);
					break;
				}

				case 3:
				{
					if (source.type == Exprent.Exprent_Exit)
					{
						source = ((ExitExprent)source).GetValue();
					}
					InvocationExprent invret = (InvocationExprent)source.Copy();
					int index_1 = 0;
					if (!invret.IsStatic())
					{
						invret.ReplaceExprent(invret.GetInstance(), invexpr.GetLstParameters()[0]);
						index_1 = 1;
					}
					for (int i = 0; i < invret.GetLstParameters().Count; i++)
					{
						invret.ReplaceExprent(invret.GetLstParameters()[i], invexpr.GetLstParameters()[i 
							+ index_1]);
					}
					retexprent = invret;
					break;
				}
			}
			if (retexprent != null)
			{
				// preserve original bytecodes
				retexprent.bytecode = null;
				retexprent.AddBytecodeOffsets(invexpr.bytecode);
				// hide synthetic access method
				bool hide = true;
				if (node.type == ClassesProcessor.ClassNode.Class_Root || (node.access & ICodeConstants
					.Acc_Static) != 0)
				{
					StructMethod mt = methsource.methodStruct;
					if (!mt.IsSynthetic())
					{
						hide = false;
					}
				}
				if (hide)
				{
					node.GetWrapper().GetHiddenMembers().Add(InterpreterUtil.MakeUniqueKey(invexpr.GetName
						(), invexpr.GetStringDescriptor()));
				}
			}
			return retexprent;
		}

		private static Exprent ReplaceFunction(InvocationExprent invexpr, Exprent source)
		{
			FunctionExprent functionExprent = (FunctionExprent)((ExitExprent)source).GetValue
				().Copy();
			List<Exprent> lstParameters = invexpr.GetLstParameters();
			FieldExprent fieldExprent = (FieldExprent)functionExprent.GetLstOperands()[0];
			if (fieldExprent.IsStatic())
			{
				if (!(lstParameters.Count == 0))
				{
					return null;
				}
				return functionExprent;
			}
			if (lstParameters.Count != 1)
			{
				return null;
			}
			fieldExprent.ReplaceExprent(fieldExprent.GetInstance(), lstParameters[0]);
			return functionExprent;
		}
	}
}
