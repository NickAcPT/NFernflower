// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Sforms;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main
{
	public class ClassReference14Processor
	{
		private static readonly ExitExprent Body_Expr;

		private static readonly ExitExprent Handler_Expr;

		static ClassReference14Processor()
		{
			InvocationExprent invFor = new InvocationExprent();
			invFor.SetName("forName");
			invFor.SetClassname("java/lang/Class");
			invFor.SetStringDescriptor("(Ljava/lang/String;)Ljava/lang/Class;");
			invFor.SetDescriptor(MethodDescriptor.ParseDescriptor("(Ljava/lang/String;)Ljava/lang/Class;"
				));
			invFor.SetStatic(true);
			invFor.SetLstParameters(System.Linq.Enumerable.ToList(new [] {new VarExprent(0, VarType
				.Vartype_String, null)}));
			Body_Expr = new ExitExprent(ExitExprent.Exit_Return, invFor, VarType.Vartype_Class
				, null);
			InvocationExprent ctor = new InvocationExprent();
			ctor.SetName(ICodeConstants.Init_Name);
			ctor.SetClassname("java/lang/NoClassDefFoundError");
			ctor.SetStringDescriptor("()V");
			ctor.SetFunctype(InvocationExprent.Typ_Init);
			ctor.SetDescriptor(MethodDescriptor.ParseDescriptor("()V"));
			NewExprent newExpr = new NewExprent(new VarType(ICodeConstants.Type_Object, 0, "java/lang/NoClassDefFoundError"
				), new List<Exprent>(), null);
			newExpr.SetConstructor(ctor);
			InvocationExprent invCause = new InvocationExprent();
			invCause.SetName("initCause");
			invCause.SetClassname("java/lang/NoClassDefFoundError");
			invCause.SetStringDescriptor("(Ljava/lang/Throwable;)Ljava/lang/Throwable;");
			invCause.SetDescriptor(MethodDescriptor.ParseDescriptor("(Ljava/lang/Throwable;)Ljava/lang/Throwable;"
				));
			invCause.SetInstance(newExpr);
			invCause.SetLstParameters(System.Linq.Enumerable.ToList(new [] {new VarExprent(2, 
				new VarType(ICodeConstants.Type_Object, 0, "java/lang/ClassNotFoundException"), 
				null)}));
			Handler_Expr = new ExitExprent(ExitExprent.Exit_Throw, invCause, null, null);
		}

		public static void ProcessClassReferences(ClassesProcessor.ClassNode node)
		{
			// find the synthetic method Class class$(String) if present
			IDictionary<ClassWrapper, MethodWrapper> mapClassMeths = new Dictionary<ClassWrapper
				, MethodWrapper>();
			MapClassMethods(node, mapClassMeths);
			if ((mapClassMeths.Count == 0))
			{
				return;
			}
			HashSet<ClassWrapper> setFound = new HashSet<ClassWrapper>();
			ProcessClassRec(node, mapClassMeths, setFound);
			if (!(setFound.Count == 0))
			{
				foreach (ClassWrapper wrp in setFound)
				{
					StructMethod mt = mapClassMeths.GetOrNull(wrp).methodStruct;
					wrp.GetHiddenMembers().Add(InterpreterUtil.MakeUniqueKey(mt.GetName(), mt.GetDescriptor
						()));
				}
			}
		}

		private static void ProcessClassRec<_T0>(ClassesProcessor.ClassNode node, IDictionary
			<ClassWrapper, MethodWrapper> mapClassMeths, HashSet<_T0> setFound)
		{
			ClassWrapper wrapper = node.GetWrapper();
			// search code
			foreach (MethodWrapper meth in wrapper.GetMethods())
			{
				RootStatement root = meth.root;
				if (root != null)
				{
					DirectGraph graph = meth.GetOrBuildGraph();
					graph.IterateExprents((Exprent exprent) => 					{
						foreach (KeyValuePair<ClassWrapper, MethodWrapper> ent in mapClassMeths)
						{
							if (ReplaceInvocations(exprent, ent.Key, ent.Value))
							{
								setFound.Add(ent.Key);
							}
						}
						return 0;
					}
);
				}
			}
			// search initializers
			for (int j = 0; j < 2; j++)
			{
				VBStyleCollection<Exprent, string> initializers = j == 0 ? wrapper.GetStaticFieldInitializers
					() : wrapper.GetDynamicFieldInitializers();
				for (int i = 0; i < initializers.Count; i++)
				{
					foreach (KeyValuePair<ClassWrapper, MethodWrapper> ent in mapClassMeths)
					{
						Exprent exprent = initializers[i];
						if (ReplaceInvocations(exprent, ent.Key, ent.Value))
						{
							setFound.Add(ent.Key);
						}
						string cl = IsClass14Invocation(exprent, ent.Key, ent.Value);
						if (cl != null)
						{
							initializers[i] = new ConstExprent(VarType.Vartype_Class, cl.Replace('.', '/'), exprent
								.bytecode);
							setFound.Add(ent.Key);
						}
					}
				}
			}
			// iterate nested classes
			foreach (ClassesProcessor.ClassNode nd in node.nested)
			{
				ProcessClassRec(nd, mapClassMeths, setFound);
			}
		}

		private static void MapClassMethods(ClassesProcessor.ClassNode node, IDictionary<
			ClassWrapper, MethodWrapper> map)
		{
			bool noSynthFlag = DecompilerContext.GetOption(IIFernflowerPreferences.Synthetic_Not_Set
				);
			ClassWrapper wrapper = node.GetWrapper();
			foreach (MethodWrapper method in wrapper.GetMethods())
			{
				StructMethod mt = method.methodStruct;
				if ((noSynthFlag || mt.IsSynthetic()) && mt.GetDescriptor().Equals("(Ljava/lang/String;)Ljava/lang/Class;"
					) && mt.HasModifier(ICodeConstants.Acc_Static))
				{
					RootStatement root = method.root;
					if (root != null && root.GetFirst().type == Statement.Type_Trycatch)
					{
						CatchStatement cst = (CatchStatement)root.GetFirst();
						if (cst.GetStats().Count == 2 && cst.GetFirst().type == Statement.Type_Basicblock
							 && cst.GetStats()[1].type == Statement.Type_Basicblock && cst.GetVars()[0].GetVarType
							().Equals(new VarType(ICodeConstants.Type_Object, 0, "java/lang/ClassNotFoundException"
							)))
						{
							BasicBlockStatement body = (BasicBlockStatement)cst.GetFirst();
							BasicBlockStatement handler = (BasicBlockStatement)cst.GetStats()[1];
							if (body.GetExprents().Count == 1 && handler.GetExprents().Count == 1)
							{
								if (Body_Expr.Equals(body.GetExprents()[0]) && Handler_Expr.Equals(handler.GetExprents
									()[0]))
								{
									Sharpen.Collections.Put(map, wrapper, method);
									break;
								}
							}
						}
					}
				}
			}
			// iterate nested classes
			foreach (ClassesProcessor.ClassNode nd in node.nested)
			{
				MapClassMethods(nd, map);
			}
		}

		private static bool ReplaceInvocations(Exprent exprent, ClassWrapper wrapper, MethodWrapper
			 meth)
		{
			bool res = false;
			while (true)
			{
				bool found = false;
				foreach (Exprent expr in exprent.GetAllExprents())
				{
					string cl = IsClass14Invocation(expr, wrapper, meth);
					if (cl != null)
					{
						exprent.ReplaceExprent(expr, new ConstExprent(VarType.Vartype_Class, cl.Replace('.'
							, '/'), expr.bytecode));
						found = true;
						res = true;
						break;
					}
					res |= ReplaceInvocations(expr, wrapper, meth);
				}
				if (!found)
				{
					break;
				}
			}
			return res;
		}

		private static string IsClass14Invocation(Exprent exprent, ClassWrapper wrapper, 
			MethodWrapper meth)
		{
			if (exprent.type == Exprent.Exprent_Function)
			{
				FunctionExprent fexpr = (FunctionExprent)exprent;
				if (fexpr.GetFuncType() == FunctionExprent.Function_Iif)
				{
					if (fexpr.GetLstOperands()[0].type == Exprent.Exprent_Function)
					{
						FunctionExprent headexpr = (FunctionExprent)fexpr.GetLstOperands()[0];
						if (headexpr.GetFuncType() == FunctionExprent.Function_Eq)
						{
							if (headexpr.GetLstOperands()[0].type == Exprent.Exprent_Field && headexpr.GetLstOperands
								()[1].type == Exprent.Exprent_Const && ((ConstExprent)headexpr.GetLstOperands()[
								1]).GetConstType().Equals(VarType.Vartype_Null))
							{
								FieldExprent field = (FieldExprent)headexpr.GetLstOperands()[0];
								ClassesProcessor.ClassNode fieldnode = DecompilerContext.GetClassProcessor().GetMapRootClasses
									().GetOrNull(field.GetClassname());
								if (fieldnode != null && fieldnode.classStruct.qualifiedName.Equals(wrapper.GetClassStruct
									().qualifiedName))
								{
									// source class
									StructField fd = wrapper.GetClassStruct().GetField(field.GetName(), field.GetDescriptor
										().descriptorString);
									// FIXME: can be null! why??
									if (fd != null && fd.HasModifier(ICodeConstants.Acc_Static) && (fd.IsSynthetic() 
										|| DecompilerContext.GetOption(IIFernflowerPreferences.Synthetic_Not_Set)))
									{
										if (fexpr.GetLstOperands()[1].type == Exprent.Exprent_Assignment && fexpr.GetLstOperands
											()[2].Equals(field))
										{
											AssignmentExprent asexpr = (AssignmentExprent)fexpr.GetLstOperands()[1];
											if (asexpr.GetLeft().Equals(field) && asexpr.GetRight().type == Exprent.Exprent_Invocation)
											{
												InvocationExprent invexpr = (InvocationExprent)asexpr.GetRight();
												if (invexpr.GetClassname().Equals(wrapper.GetClassStruct().qualifiedName) && invexpr
													.GetName().Equals(meth.methodStruct.GetName()) && invexpr.GetStringDescriptor().
													Equals(meth.methodStruct.GetDescriptor()))
												{
													if (invexpr.GetLstParameters()[0].type == Exprent.Exprent_Const)
													{
														wrapper.GetHiddenMembers().Add(InterpreterUtil.MakeUniqueKey(fd.GetName(), fd.GetDescriptor
															()));
														// hide synthetic field
														return ((ConstExprent)invexpr.GetLstParameters()[0]).GetValue().ToString();
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
			return null;
		}
	}
}
