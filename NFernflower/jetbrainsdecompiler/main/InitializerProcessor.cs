// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main
{
	public class InitializerProcessor
	{
		public static void ExtractInitializers(ClassWrapper wrapper)
		{
			MethodWrapper method = wrapper.GetMethodWrapper(ICodeConstants.Clinit_Name, "()V"
				);
			if (method != null && method.root != null)
			{
				// successfully decompiled static constructor
				ExtractStaticInitializers(wrapper, method);
			}
			ExtractDynamicInitializers(wrapper);
			// required e.g. if anonymous class is being decompiled as a standard one.
			// This can happen if InnerClasses attributes are erased
			LiftConstructor(wrapper);
			if (DecompilerContext.GetOption(IIFernflowerPreferences.Hide_Empty_Super))
			{
				HideEmptySuper(wrapper);
			}
		}

		private static void LiftConstructor(ClassWrapper wrapper)
		{
			foreach (MethodWrapper method in wrapper.GetMethods())
			{
				if (ICodeConstants.Init_Name.Equals(method.methodStruct.GetName()) && method.root
					 != null)
				{
					Statement firstData = Statements.FindFirstData(method.root);
					if (firstData == null)
					{
						return;
					}
					int index = 0;
					List<Exprent> lstExprents = firstData.GetExprents();
					foreach (Exprent exprent in lstExprents)
					{
						int action = 0;
						if (exprent.type == Exprent.Exprent_Assignment)
						{
							AssignmentExprent assignExpr = (AssignmentExprent)exprent;
							if (assignExpr.GetLeft().type == Exprent.Exprent_Field && assignExpr.GetRight().type
								 == Exprent.Exprent_Var)
							{
								FieldExprent fExpr = (FieldExprent)assignExpr.GetLeft();
								if (fExpr.GetClassname().Equals(wrapper.GetClassStruct().qualifiedName))
								{
									StructField structField = wrapper.GetClassStruct().GetField(fExpr.GetName(), fExpr
										.GetDescriptor().descriptorString);
									if (structField != null && structField.HasModifier(ICodeConstants.Acc_Final))
									{
										action = 1;
									}
								}
							}
						}
						else if (index > 0 && exprent.type == Exprent.Exprent_Invocation && Statements.IsInvocationInitConstructor
							((InvocationExprent)exprent, method, wrapper, true))
						{
							// this() or super()
							lstExprents.Add(0, lstExprents.RemoveAtReturningValue(index));
							action = 2;
						}
						if (action != 1)
						{
							break;
						}
						index++;
					}
				}
			}
		}

		private static void HideEmptySuper(ClassWrapper wrapper)
		{
			foreach (MethodWrapper method in wrapper.GetMethods())
			{
				if (ICodeConstants.Init_Name.Equals(method.methodStruct.GetName()) && method.root
					 != null)
				{
					Statement firstData = Statements.FindFirstData(method.root);
					if (firstData == null || (firstData.GetExprents().Count == 0))
					{
						return;
					}
					Exprent exprent = firstData.GetExprents()[0];
					if (exprent.type == Exprent.Exprent_Invocation)
					{
						InvocationExprent invExpr = (InvocationExprent)exprent;
						if (Statements.IsInvocationInitConstructor(invExpr, method, wrapper, false) && (invExpr
							.GetLstParameters().Count == 0))
						{
							firstData.GetExprents().RemoveAtReturningValue(0);
						}
					}
				}
			}
		}

		private static void ExtractStaticInitializers(ClassWrapper wrapper, MethodWrapper
			 method)
		{
			RootStatement root = method.root;
			StructClass cl = wrapper.GetClassStruct();
			Statement firstData = Statements.FindFirstData(root);
			if (firstData != null)
			{
				bool inlineInitializers = cl.HasModifier(ICodeConstants.Acc_Interface) || cl.HasModifier
					(ICodeConstants.Acc_Enum);
				while (!(firstData.GetExprents().Count == 0))
				{
					Exprent exprent = firstData.GetExprents()[0];
					bool found = false;
					if (exprent.type == Exprent.Exprent_Assignment)
					{
						AssignmentExprent assignExpr = (AssignmentExprent)exprent;
						if (assignExpr.GetLeft().type == Exprent.Exprent_Field)
						{
							FieldExprent fExpr = (FieldExprent)assignExpr.GetLeft();
							if (fExpr.IsStatic() && fExpr.GetClassname().Equals(cl.qualifiedName) && cl.HasField
								(fExpr.GetName(), fExpr.GetDescriptor().descriptorString))
							{
								// interfaces fields should always be initialized inline
								if (inlineInitializers || IsExprentIndependent(assignExpr.GetRight(), method))
								{
									string keyField = InterpreterUtil.MakeUniqueKey(fExpr.GetName(), fExpr.GetDescriptor
										().descriptorString);
									if (!wrapper.GetStaticFieldInitializers().ContainsKey(keyField))
									{
										wrapper.GetStaticFieldInitializers().AddWithKey(assignExpr.GetRight(), keyField);
										firstData.GetExprents().RemoveAtReturningValue(0);
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
				}
			}
		}

		private static void ExtractDynamicInitializers(ClassWrapper wrapper)
		{
			StructClass cl = wrapper.GetClassStruct();
			bool isAnonymous = DecompilerContext.GetClassProcessor().GetMapRootClasses().GetOrNull
				(cl.qualifiedName).type == ClassesProcessor.ClassNode.Class_Anonymous;
			List<List<Exprent>> lstFirst = new List<List<Exprent>>();
			List<MethodWrapper> lstMethodWrappers = new List<MethodWrapper>();
			foreach (MethodWrapper method in wrapper.GetMethods())
			{
				if (ICodeConstants.Init_Name.Equals(method.methodStruct.GetName()) && method.root
					 != null)
				{
					// successfully decompiled constructor
					Statement firstData = Statements.FindFirstData(method.root);
					if (firstData == null || (firstData.GetExprents().Count == 0))
					{
						return;
					}
					lstFirst.Add(firstData.GetExprents());
					lstMethodWrappers.Add(method);
					Exprent exprent = firstData.GetExprents()[0];
					if (!isAnonymous)
					{
						// FIXME: doesn't make sense
						if (exprent.type != Exprent.Exprent_Invocation || !Statements.IsInvocationInitConstructor
							((InvocationExprent)exprent, method, wrapper, false))
						{
							return;
						}
					}
				}
			}
			if ((lstFirst.Count == 0))
			{
				return;
			}
			while (true)
			{
				string fieldWithDescr = null;
				Exprent value = null;
				for (int i = 0; i < lstFirst.Count; i++)
				{
					List<Exprent> lst = lstFirst[i];
					if (lst.Count < (isAnonymous ? 1 : 2))
					{
						return;
					}
					Exprent exprent = lst[isAnonymous ? 0 : 1];
					bool found = false;
					if (exprent.type == Exprent.Exprent_Assignment)
					{
						AssignmentExprent assignExpr = (AssignmentExprent)exprent;
						if (assignExpr.GetLeft().type == Exprent.Exprent_Field)
						{
							FieldExprent fExpr = (FieldExprent)assignExpr.GetLeft();
							if (!fExpr.IsStatic() && fExpr.GetClassname().Equals(cl.qualifiedName) && cl.HasField
								(fExpr.GetName(), fExpr.GetDescriptor().descriptorString))
							{
								// check for the physical existence of the field. Could be defined in a superclass.
								if (IsExprentIndependent(assignExpr.GetRight(), lstMethodWrappers[i]))
								{
									string fieldKey = InterpreterUtil.MakeUniqueKey(fExpr.GetName(), fExpr.GetDescriptor
										().descriptorString);
									if (fieldWithDescr == null)
									{
										fieldWithDescr = fieldKey;
										value = assignExpr.GetRight();
									}
									else if (!fieldWithDescr.Equals(fieldKey) || !value.Equals(assignExpr.GetRight()))
									{
										return;
									}
									found = true;
								}
							}
						}
					}
					if (!found)
					{
						return;
					}
				}
				if (!wrapper.GetDynamicFieldInitializers().ContainsKey(fieldWithDescr))
				{
					wrapper.GetDynamicFieldInitializers().AddWithKey(value, fieldWithDescr);
					foreach (List<Exprent> lst in lstFirst)
					{
						lst.RemoveAtReturningValue(isAnonymous ? 0 : 1);
					}
				}
				else
				{
					return;
				}
			}
		}

		private static bool IsExprentIndependent(Exprent exprent, MethodWrapper method)
		{
			List<Exprent> lst = exprent.GetAllExprents(true);
			lst.Add(exprent);
			foreach (Exprent expr in lst)
			{
				switch (expr.type)
				{
					case Exprent.Exprent_Var:
					{
						VarVersionPair varPair = new VarVersionPair((VarExprent)expr);
						if (!method.varproc.GetExternalVars().Contains(varPair))
						{
							string varName = method.varproc.GetVarName(varPair);
							if (!varName.Equals("this") && !varName.EndsWith(".this"))
							{
								// FIXME: remove direct comparison with strings
								return false;
							}
						}
						break;
					}

					case Exprent.Exprent_Field:
					{
						return false;
					}
				}
			}
			return true;
		}
	}
}
