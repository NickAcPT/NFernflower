// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Sforms;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main.Rels
{
	public class NestedClassProcessor
	{
		public virtual void ProcessClass(ClassesProcessor.ClassNode root, ClassesProcessor.ClassNode
			 node)
		{
			// hide synthetic lambda content methods
			if (node.type == ClassesProcessor.ClassNode.Class_Lambda && !node.lambdaInformation
				.is_method_reference)
			{
				ClassesProcessor.ClassNode node_content = DecompilerContext.GetClassProcessor().GetMapRootClasses
					().GetOrNull(node.classStruct.qualifiedName);
				if (node_content != null && node_content.GetWrapper() != null)
				{
					node_content.GetWrapper().GetHiddenMembers().Add(node.lambdaInformation.content_method_key
						);
				}
			}
			if ((node.nested.Count == 0))
			{
				return;
			}
			if (node.type != ClassesProcessor.ClassNode.Class_Lambda)
			{
				ComputeLocalVarsAndDefinitions(node);
				// for each local or anonymous class ensure not empty enclosing method
				CheckNotFoundClasses(root, node);
			}
			int nameless = 0;
			int synthetics = 0;
			foreach (ClassesProcessor.ClassNode child in node.nested)
			{
				StructClass cl = child.classStruct;
				// ensure not-empty class name
				if ((child.type == ClassesProcessor.ClassNode.Class_Local || child.type == ClassesProcessor.ClassNode
					.Class_Member) && child.simpleName == null)
				{
					if ((child.access & ICodeConstants.Acc_Synthetic) != 0 || cl.IsSynthetic())
					{
						child.simpleName = "SyntheticClass_" + (++synthetics);
					}
					else
					{
						string message = "Nameless local or member class " + cl.qualifiedName + "!";
						DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
							);
						child.simpleName = "NamelessClass_" + (++nameless);
					}
				}
			}
			foreach (ClassesProcessor.ClassNode child in node.nested)
			{
				if (child.type == ClassesProcessor.ClassNode.Class_Lambda)
				{
					SetLambdaVars(node, child);
				}
				else if (child.type != ClassesProcessor.ClassNode.Class_Member || (child.access &
					 ICodeConstants.Acc_Static) == 0)
				{
					InsertLocalVars(node, child);
					if (child.type == ClassesProcessor.ClassNode.Class_Local && child.enclosingMethod
						 != null)
					{
						MethodWrapper enclosingMethodWrapper = node.GetWrapper().GetMethods().GetWithKey(
							child.enclosingMethod);
						if (enclosingMethodWrapper != null)
						{
							// e.g. in case of switch-on-enum. FIXME: some proper handling of multiple enclosing classes 
							SetLocalClassDefinition(enclosingMethodWrapper, child);
						}
					}
				}
			}
			foreach (ClassesProcessor.ClassNode child in node.nested)
			{
				ProcessClass(root, child);
			}
		}

		private static void SetLambdaVars(ClassesProcessor.ClassNode parent, ClassesProcessor.ClassNode
			 child)
		{
			if (child.lambdaInformation.is_method_reference)
			{
				// method reference, no code and no parameters
				return;
			}
			MethodWrapper method = parent.GetWrapper().GetMethods().GetWithKey(child.lambdaInformation
				.content_method_key);
			MethodWrapper enclosingMethod = parent.GetWrapper().GetMethods().GetWithKey(child
				.enclosingMethod);
			MethodDescriptor md_lambda = MethodDescriptor.ParseDescriptor(child.lambdaInformation
				.method_descriptor);
			MethodDescriptor md_content = MethodDescriptor.ParseDescriptor(child.lambdaInformation
				.content_method_descriptor);
			int vars_count = md_content.@params.Length - md_lambda.@params.Length;
			bool is_static_lambda_content = child.lambdaInformation.is_content_method_static;
			string parent_class_name = parent.GetWrapper().GetClassStruct().qualifiedName;
			string lambda_class_name = child.simpleName;
			VarType lambda_class_type = new VarType(lambda_class_name, true);
			// this pointer
			if (!is_static_lambda_content && DecompilerContext.GetOption(IIFernflowerPreferences
				.Lambda_To_Anonymous_Class))
			{
				Sharpen.Collections.Put(method.varproc.GetThisVars(), new VarVersionPair(0, 0), parent_class_name
					);
				method.varproc.SetVarName(new VarVersionPair(0, 0), parent.simpleName + ".this");
			}
			IDictionary<VarVersionPair, string> mapNewNames = new Dictionary<VarVersionPair, 
				string>();
			enclosingMethod.GetOrBuildGraph().IterateExprents((Exprent exprent) => 			{
				List<Exprent> lst = exprent.GetAllExprents(true);
				lst.Add(exprent);
				foreach (Exprent expr in lst)
				{
					if (expr.type == Exprent.Exprent_New)
					{
						NewExprent new_expr = (NewExprent)expr;
						VarNamesCollector enclosingCollector = new VarNamesCollector(enclosingMethod.varproc
							.GetVarNames());
						if (new_expr.IsLambda() && lambda_class_type.Equals(new_expr.GetNewType()))
						{
							InvocationExprent inv_dynamic = new_expr.GetConstructor();
							int param_index = is_static_lambda_content ? 0 : 1;
							int varIndex = is_static_lambda_content ? 0 : 1;
							for (int i = 0; i < md_content.@params.Length; ++i)
							{
								VarVersionPair varVersion = new VarVersionPair(varIndex, 0);
								if (i < vars_count)
								{
									Exprent param = inv_dynamic.GetLstParameters()[param_index + i];
									if (param.type == Exprent.Exprent_Var)
									{
										Sharpen.Collections.Put(mapNewNames, varVersion, enclosingMethod.varproc.GetVarName
											(new VarVersionPair((VarExprent)param)));
									}
								}
								else
								{
									Sharpen.Collections.Put(mapNewNames, varVersion, enclosingCollector.GetFreeName(method
										.varproc.GetVarName(varVersion)));
								}
								varIndex += md_content.@params[i].stackSize;
							}
						}
					}
				}
				return 0;
			}
);
			// update names of local variables
			HashSet<string> setNewOuterNames = new HashSet<string>(mapNewNames.Values);
			setNewOuterNames.RemoveAll(method.setOuterVarNames);
			method.varproc.RefreshVarNames(new VarNamesCollector(setNewOuterNames));
			Sharpen.Collections.AddAll(method.setOuterVarNames, setNewOuterNames);
			foreach (KeyValuePair<VarVersionPair, string> entry in mapNewNames)
			{
				method.varproc.SetVarName(entry.Key, entry.Value);
			}
		}

		private static void CheckNotFoundClasses(ClassesProcessor.ClassNode root, ClassesProcessor.ClassNode
			 node)
		{
			List<ClassesProcessor.ClassNode> copy = new List<ClassesProcessor.ClassNode>(node
				.nested);
			foreach (ClassesProcessor.ClassNode child in copy)
			{
				if (child.classStruct.IsSynthetic())
				{
					continue;
				}
				if ((child.type == ClassesProcessor.ClassNode.Class_Local || child.type == ClassesProcessor.ClassNode
					.Class_Anonymous) && child.enclosingMethod == null)
				{
					HashSet<string> setEnclosing = child.enclosingClasses;
					if (!(setEnclosing.Count == 0))
					{
						StructEnclosingMethodAttribute attr = child.classStruct.GetAttribute(StructGeneralAttribute
							.Attribute_Enclosing_Method);
						if (attr != null && attr.GetMethodName() != null && node.classStruct.qualifiedName
							.Equals(attr.GetClassName()) && node.classStruct.GetMethod(attr.GetMethodName(), 
							attr.GetMethodDescriptor()) != null)
						{
							child.enclosingMethod = InterpreterUtil.MakeUniqueKey(attr.GetMethodName(), attr.
								GetMethodDescriptor());
							continue;
						}
					}
					node.nested.Remove(child);
					child.parent = null;
					setEnclosing.Remove(node.classStruct.qualifiedName);
					bool hasEnclosing = !(setEnclosing.Count == 0) && InsertNestedClass(root, child);
					if (!hasEnclosing)
					{
						if (child.type == ClassesProcessor.ClassNode.Class_Anonymous)
						{
							string message = "Unreferenced anonymous class " + child.classStruct.qualifiedName
								 + "!";
							DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
								);
						}
						else if (child.type == ClassesProcessor.ClassNode.Class_Local)
						{
							string message = "Unreferenced local class " + child.classStruct.qualifiedName + 
								"!";
							DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
								);
						}
					}
				}
			}
		}

		private static bool InsertNestedClass(ClassesProcessor.ClassNode root, ClassesProcessor.ClassNode
			 child)
		{
			HashSet<string> setEnclosing = child.enclosingClasses;
			LinkedList<ClassesProcessor.ClassNode> stack = new LinkedList<ClassesProcessor.ClassNode
				>();
			stack.Add(root);
			while (!(stack.Count == 0))
			{
				ClassesProcessor.ClassNode node = Sharpen.Collections.RemoveFirst(stack);
				if (setEnclosing.Contains(node.classStruct.qualifiedName))
				{
					node.nested.Add(child);
					child.parent = node;
					return true;
				}
				// note: ordered list
				Sharpen.Collections.AddAll(stack, node.nested);
			}
			return false;
		}

		private static void ComputeLocalVarsAndDefinitions(ClassesProcessor.ClassNode node
			)
		{
			// class name -> constructor descriptor -> var to field link
			IDictionary<string, IDictionary<string, List<NestedClassProcessor.VarFieldPair>>
				> mapVarMasks = new Dictionary<string, IDictionary<string, List<NestedClassProcessor.VarFieldPair
				>>>();
			int clTypes = 0;
			foreach (ClassesProcessor.ClassNode nd in node.nested)
			{
				if (nd.type != ClassesProcessor.ClassNode.Class_Lambda && !nd.classStruct.IsSynthetic
					() && (nd.access & ICodeConstants.Acc_Static) == 0 && (nd.access & ICodeConstants
					.Acc_Interface) == 0)
				{
					clTypes |= nd.type;
					IDictionary<string, List<NestedClassProcessor.VarFieldPair>> mask = GetMaskLocalVars
						(nd.GetWrapper());
					if ((mask.Count == 0))
					{
						string message = "Nested class " + nd.classStruct.qualifiedName + " has no constructor!";
						DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
							);
					}
					else
					{
						Sharpen.Collections.Put(mapVarMasks, nd.classStruct.qualifiedName, mask);
					}
				}
			}
			// local var masks
			IDictionary<string, IDictionary<string, List<NestedClassProcessor.VarFieldPair>>
				> mapVarFieldPairs = new Dictionary<string, IDictionary<string, List<NestedClassProcessor.VarFieldPair
				>>>();
			if (clTypes != ClassesProcessor.ClassNode.Class_Member)
			{
				// iterate enclosing class
				foreach (MethodWrapper method in node.GetWrapper().GetMethods())
				{
					if (method.root != null)
					{
						// neither abstract, nor native
						method.GetOrBuildGraph().IterateExprents((Exprent exprent) => 						{
							List<Exprent> lst = exprent.GetAllExprents(true);
							lst.Add(exprent);
							foreach (Exprent expr in lst)
							{
								if (expr.type == Exprent.Exprent_New)
								{
									InvocationExprent constructor = ((NewExprent)expr).GetConstructor();
									if (constructor != null && mapVarMasks.ContainsKey(constructor.GetClassname()))
									{
										// non-static inner class constructor
										string refClassName = constructor.GetClassname();
										ClassesProcessor.ClassNode nestedClassNode = node.GetClassNode(refClassName);
										if (nestedClassNode.type != ClassesProcessor.ClassNode.Class_Member)
										{
											List<NestedClassProcessor.VarFieldPair> mask = mapVarMasks.GetOrNull(refClassName
												).GetOrNull(constructor.GetStringDescriptor());
											if (!mapVarFieldPairs.ContainsKey(refClassName))
											{
												Sharpen.Collections.Put(mapVarFieldPairs, refClassName, new Dictionary<string, IList
													<NestedClassProcessor.VarFieldPair>>());
											}
											List<NestedClassProcessor.VarFieldPair> lstTemp = new List<NestedClassProcessor.VarFieldPair
												>();
											for (int i = 0; i < mask.Count; i++)
											{
												Exprent param = constructor.GetLstParameters()[i];
												NestedClassProcessor.VarFieldPair pair = null;
												if (param.type == Exprent.Exprent_Var && mask[i] != null)
												{
													VarVersionPair varPair = new VarVersionPair((VarExprent)param);
													// FIXME: flags of variables are wrong! Correct the entire functionality.
													// if(method.varproc.getVarFinal(varPair) != VarTypeProcessor.VAR_NON_FINAL) {
													pair = new NestedClassProcessor.VarFieldPair(mask[i].fieldKey, varPair);
												}
												// }
												lstTemp.Add(pair);
											}
											List<NestedClassProcessor.VarFieldPair> pairMask = mapVarFieldPairs.GetOrNull(refClassName
												).GetOrNull(constructor.GetStringDescriptor());
											if (pairMask == null)
											{
												pairMask = lstTemp;
											}
											else
											{
												for (int i = 0; i < pairMask.Count; i++)
												{
													if (!InterpreterUtil.EqualObjects(pairMask[i], lstTemp[i]))
													{
														pairMask[i] = null;
													}
												}
											}
											Sharpen.Collections.Put(mapVarFieldPairs.GetOrNull(refClassName), constructor.GetStringDescriptor
												(), pairMask);
											nestedClassNode.enclosingMethod = InterpreterUtil.MakeUniqueKey(method.methodStruct
												.GetName(), method.methodStruct.GetDescriptor());
										}
									}
								}
							}
							return 0;
						}
);
					}
				}
			}
			// merge var masks
			foreach (KeyValuePair<string, IDictionary<string, List<NestedClassProcessor.VarFieldPair
				>>> enclosing in mapVarMasks)
			{
				ClassesProcessor.ClassNode nestedNode = node.GetClassNode(enclosing.Key);
				// intersection
				List<NestedClassProcessor.VarFieldPair> interPairMask = null;
				// merge referenced constructors
				if (mapVarFieldPairs.ContainsKey(enclosing.Key))
				{
					foreach (List<NestedClassProcessor.VarFieldPair> mask in mapVarFieldPairs.GetOrNull
						(enclosing.Key).Values)
					{
						if (interPairMask == null)
						{
							interPairMask = new List<NestedClassProcessor.VarFieldPair>(mask);
						}
						else
						{
							MergeListSignatures(interPairMask, mask, false);
						}
					}
				}
				List<NestedClassProcessor.VarFieldPair> interMask = null;
				// merge all constructors
				foreach (List<NestedClassProcessor.VarFieldPair> mask in enclosing.Value.Values)
				{
					if (interMask == null)
					{
						interMask = new List<NestedClassProcessor.VarFieldPair>(mask);
					}
					else
					{
						MergeListSignatures(interMask, mask, false);
					}
				}
				if (interPairMask == null)
				{
					// member or local and never instantiated
					interPairMask = interMask != null ? new List<NestedClassProcessor.VarFieldPair>(interMask
						) : new List<NestedClassProcessor.VarFieldPair>();
					bool found = false;
					for (int i = 0; i < interPairMask.Count; i++)
					{
						if (interPairMask[i] != null)
						{
							if (found)
							{
								interPairMask[i] = null;
							}
							found = true;
						}
					}
				}
				MergeListSignatures(interPairMask, interMask, true);
				foreach (NestedClassProcessor.VarFieldPair pair in interPairMask)
				{
					if (pair != null && !(pair.fieldKey.Length == 0))
					{
						Sharpen.Collections.Put(nestedNode.mapFieldsToVars, pair.fieldKey, pair.varPair);
					}
				}
				// set resulting constructor signatures
				foreach (KeyValuePair<string, List<NestedClassProcessor.VarFieldPair>> entry in 
					enclosing.Value)
				{
					MergeListSignatures(entry.Value, interPairMask, false);
					List<VarVersionPair> mask = new List<VarVersionPair>(entry.Value.Count);
					foreach (NestedClassProcessor.VarFieldPair pair in entry.Value)
					{
						mask.Add(pair != null && !(pair.fieldKey.Length == 0) ? pair.varPair : null);
					}
					nestedNode.GetWrapper().GetMethodWrapper(ICodeConstants.Init_Name, entry.Key).synthParameters
						 = mask;
				}
			}
		}

		private static void InsertLocalVars(ClassesProcessor.ClassNode parent, ClassesProcessor.ClassNode
			 child)
		{
			// enclosing method, is null iff member class
			MethodWrapper enclosingMethod = parent.GetWrapper().GetMethods().GetWithKey(child
				.enclosingMethod);
			// iterate all child methods
			foreach (MethodWrapper method in child.GetWrapper().GetMethods())
			{
				if (method.root != null)
				{
					// neither abstract nor native
					IDictionary<VarVersionPair, string> mapNewNames = new Dictionary<VarVersionPair, 
						string>();
					// local var names
					IDictionary<VarVersionPair, VarType> mapNewTypes = new Dictionary<VarVersionPair, 
						VarType>();
					// local var types
					IDictionary<int, VarVersionPair> mapParamsToNewVars = new Dictionary<int, VarVersionPair
						>();
					if (method.synthParameters != null)
					{
						int index = 0;
						int varIndex = 1;
						MethodDescriptor md = MethodDescriptor.ParseDescriptor(method.methodStruct.GetDescriptor
							());
						foreach (VarVersionPair pair in method.synthParameters)
						{
							if (pair != null)
							{
								VarVersionPair newVar = new VarVersionPair(method.counter.GetCounterAndIncrement(
									CounterContainer.Var_Counter), 0);
								Sharpen.Collections.Put(mapParamsToNewVars, varIndex, newVar);
								string varName = null;
								VarType varType = null;
								if (child.type != ClassesProcessor.ClassNode.Class_Member)
								{
									varName = enclosingMethod.varproc.GetVarName(pair);
									varType = enclosingMethod.varproc.GetVarType(pair);
									enclosingMethod.varproc.SetVarFinal(pair, VarTypeProcessor.Var_Explicit_Final);
								}
								if (pair.var == -1 || "this".Equals(varName))
								{
									if (parent.simpleName == null)
									{
										// anonymous enclosing class, no access to this
										varName = VarExprent.Var_Nameless_Enclosure;
									}
									else
									{
										varName = parent.simpleName + ".this";
									}
									Sharpen.Collections.Put(method.varproc.GetThisVars(), newVar, parent.classStruct.
										qualifiedName);
								}
								Sharpen.Collections.Put(mapNewNames, newVar, varName);
								Sharpen.Collections.Put(mapNewTypes, newVar, varType);
							}
							varIndex += md.@params[index++].stackSize;
						}
					}
					IDictionary<string, VarVersionPair> mapFieldsToNewVars = new Dictionary<string, VarVersionPair
						>();
					for (ClassesProcessor.ClassNode classNode = child; classNode != null; classNode =
						 classNode.parent)
					{
						foreach (KeyValuePair<string, VarVersionPair> entry in classNode.mapFieldsToVars)
						{
							VarVersionPair newVar = new VarVersionPair(method.counter.GetCounterAndIncrement(
								CounterContainer.Var_Counter), 0);
							Sharpen.Collections.Put(mapFieldsToNewVars, InterpreterUtil.MakeUniqueKey(classNode
								.classStruct.qualifiedName, entry.Key), newVar);
							string varName = null;
							VarType varType = null;
							if (classNode.type != ClassesProcessor.ClassNode.Class_Member)
							{
								MethodWrapper enclosing_method = classNode.parent.GetWrapper().GetMethods().GetWithKey
									(classNode.enclosingMethod);
								varName = enclosing_method.varproc.GetVarName(entry.Value);
								varType = enclosing_method.varproc.GetVarType(entry.Value);
								enclosing_method.varproc.SetVarFinal(entry.Value, VarTypeProcessor.Var_Explicit_Final
									);
							}
							if (entry.Value.var == -1 || "this".Equals(varName))
							{
								if (classNode.parent.simpleName == null)
								{
									// anonymous enclosing class, no access to this
									varName = VarExprent.Var_Nameless_Enclosure;
								}
								else
								{
									varName = classNode.parent.simpleName + ".this";
								}
								Sharpen.Collections.Put(method.varproc.GetThisVars(), newVar, classNode.parent.classStruct
									.qualifiedName);
							}
							Sharpen.Collections.Put(mapNewNames, newVar, varName);
							Sharpen.Collections.Put(mapNewTypes, newVar, varType);
							// hide synthetic field
							if (classNode == child)
							{
								// fields higher up the chain were already handled with their classes
								StructField fd = child.classStruct.GetFields().GetWithKey(entry.Key);
								child.GetWrapper().GetHiddenMembers().Add(InterpreterUtil.MakeUniqueKey(fd.GetName
									(), fd.GetDescriptor()));
							}
						}
					}
					HashSet<string> setNewOuterNames = new HashSet<string>(mapNewNames.Values);
					setNewOuterNames.RemoveAll(method.setOuterVarNames);
					method.varproc.RefreshVarNames(new VarNamesCollector(setNewOuterNames));
					Sharpen.Collections.AddAll(method.setOuterVarNames, setNewOuterNames);
					foreach (KeyValuePair<VarVersionPair, string> entry in mapNewNames)
					{
						VarVersionPair pair = entry.Key;
						VarType type = mapNewTypes.GetOrNull(pair);
						method.varproc.SetVarName(pair, entry.Value);
						if (type != null)
						{
							method.varproc.SetVarType(pair, type);
						}
					}
					method.GetOrBuildGraph().IterateExprents(new _IExprentIterator_497(child, mapFieldsToNewVars
						, method, mapParamsToNewVars));
				}
			}
		}

		private sealed class _IExprentIterator_497 : DirectGraph.IExprentIterator
		{
			public _IExprentIterator_497(ClassesProcessor.ClassNode child, IDictionary<string
				, VarVersionPair> mapFieldsToNewVars, MethodWrapper method, IDictionary<int, VarVersionPair
				> mapParamsToNewVars)
			{
				this.child = child;
				this.mapFieldsToNewVars = mapFieldsToNewVars;
				this.method = method;
				this.mapParamsToNewVars = mapParamsToNewVars;
			}

			public int ProcessExprent(Exprent exprent)
			{
				if (exprent.type == Exprent.Exprent_Assignment)
				{
					AssignmentExprent assignExpr = (AssignmentExprent)exprent;
					if (assignExpr.GetLeft().type == Exprent.Exprent_Field)
					{
						FieldExprent fExpr = (FieldExprent)assignExpr.GetLeft();
						string qName = child.classStruct.qualifiedName;
						if (fExpr.GetClassname().Equals(qName) && mapFieldsToNewVars.ContainsKey(InterpreterUtil
							.MakeUniqueKey(qName, fExpr.GetName(), fExpr.GetDescriptor().descriptorString)))
						{
							// process this class only
							return 2;
						}
					}
				}
				if (child.type == ClassesProcessor.ClassNode.Class_Anonymous && ICodeConstants.Init_Name
					.Equals(method.methodStruct.GetName()) && exprent.type == Exprent.Exprent_Invocation)
				{
					InvocationExprent invokeExpr = (InvocationExprent)exprent;
					if (invokeExpr.GetFunctype() == InvocationExprent.Typ_Init)
					{
						// invocation of the super constructor in an anonymous class
						child.superInvocation = invokeExpr;
						// FIXME: save original names of parameters
						return 2;
					}
				}
				this.ReplaceExprent(exprent);
				return 0;
			}

			private Exprent ReplaceExprent(Exprent exprent)
			{
				if (exprent.type == Exprent.Exprent_Var)
				{
					int varIndex = ((VarExprent)exprent).GetIndex();
					if (mapParamsToNewVars.ContainsKey(varIndex))
					{
						VarVersionPair newVar = mapParamsToNewVars.GetOrNull(varIndex);
						method.varproc.GetExternalVars().Add(newVar);
						return new VarExprent(newVar.var, method.varproc.GetVarType(newVar), method.varproc
							);
					}
				}
				else if (exprent.type == Exprent.Exprent_Field)
				{
					FieldExprent fExpr = (FieldExprent)exprent;
					string key = InterpreterUtil.MakeUniqueKey(fExpr.GetClassname(), fExpr.GetName(), 
						fExpr.GetDescriptor().descriptorString);
					if (mapFieldsToNewVars.ContainsKey(key))
					{
						//if(fExpr.getClassname().equals(child.classStruct.qualifiedName) &&
						//		mapFieldsToNewVars.containsKey(key)) {
						VarVersionPair newVar = mapFieldsToNewVars.GetOrNull(key);
						method.varproc.GetExternalVars().Add(newVar);
						return new VarExprent(newVar.var, method.varproc.GetVarType(newVar), method.varproc
							);
					}
				}
				bool replaced = true;
				while (replaced)
				{
					replaced = false;
					foreach (Exprent expr in exprent.GetAllExprents())
					{
						Exprent retExpr = this.ReplaceExprent(expr);
						if (retExpr != null)
						{
							exprent.ReplaceExprent(expr, retExpr);
							replaced = true;
							break;
						}
					}
				}
				return null;
			}

			private readonly ClassesProcessor.ClassNode child;

			private readonly IDictionary<string, VarVersionPair> mapFieldsToNewVars;

			private readonly MethodWrapper method;

			private readonly IDictionary<int, VarVersionPair> mapParamsToNewVars;
		}

		private static IDictionary<string, List<NestedClassProcessor.VarFieldPair>> GetMaskLocalVars
			(ClassWrapper wrapper)
		{
			IDictionary<string, List<NestedClassProcessor.VarFieldPair>> mapMasks = new Dictionary
				<string, List<NestedClassProcessor.VarFieldPair>>();
			StructClass cl = wrapper.GetClassStruct();
			// iterate over constructors
			foreach (StructMethod mt in cl.GetMethods())
			{
				if (ICodeConstants.Init_Name.Equals(mt.GetName()))
				{
					MethodDescriptor md = MethodDescriptor.ParseDescriptor(mt.GetDescriptor());
					MethodWrapper method = wrapper.GetMethodWrapper(ICodeConstants.Init_Name, mt.GetDescriptor
						());
					DirectGraph graph = method.GetOrBuildGraph();
					if (graph != null)
					{
						// something gone wrong, should not be null
						List<NestedClassProcessor.VarFieldPair> fields = new List<NestedClassProcessor.VarFieldPair
							>(md.@params.Length);
						int varIndex = 1;
						for (int i = 0; i < md.@params.Length; i++)
						{
							// no static methods allowed
							string keyField = GetEnclosingVarField(cl, method, graph, varIndex);
							fields.Add(keyField == null ? null : new NestedClassProcessor.VarFieldPair(keyField
								, new VarVersionPair(-1, 0)));
							// TODO: null?
							varIndex += md.@params[i].stackSize;
						}
						Sharpen.Collections.Put(mapMasks, mt.GetDescriptor(), fields);
					}
				}
			}
			return mapMasks;
		}

		private static string GetEnclosingVarField(StructClass cl, MethodWrapper method, 
			DirectGraph graph, int index)
		{
			string field = string.Empty;
			// parameter variable final
			if (method.varproc.GetVarFinal(new VarVersionPair(index, 0)) == VarTypeProcessor.
				Var_Non_Final)
			{
				return null;
			}
			bool noSynthFlag = DecompilerContext.GetOption(IIFernflowerPreferences.Synthetic_Not_Set
				);
			// no loop at the begin
			DirectNode firstNode = graph.first;
			if ((firstNode.preds.Count == 0))
			{
				// assignment to a synthetic field?
				foreach (Exprent exprent in firstNode.exprents)
				{
					if (exprent.type == Exprent.Exprent_Assignment)
					{
						AssignmentExprent assignExpr = (AssignmentExprent)exprent;
						if (assignExpr.GetRight().type == Exprent.Exprent_Var && ((VarExprent)assignExpr.
							GetRight()).GetIndex() == index && assignExpr.GetLeft().type == Exprent.Exprent_Field)
						{
							FieldExprent left = (FieldExprent)assignExpr.GetLeft();
							StructField fd = cl.GetField(left.GetName(), left.GetDescriptor().descriptorString
								);
							if (fd != null && cl.qualifiedName.Equals(left.GetClassname()) && (fd.IsSynthetic
								() || noSynthFlag && PossiblySyntheticField(fd)))
							{
								// local (== not inherited) field
								field = InterpreterUtil.MakeUniqueKey(left.GetName(), left.GetDescriptor().descriptorString
									);
								break;
							}
						}
					}
				}
			}
			return field;
		}

		private static bool PossiblySyntheticField(StructField fd)
		{
			return fd.GetName().Contains("$") && fd.HasModifier(ICodeConstants.Acc_Final) && 
				fd.HasModifier(ICodeConstants.Acc_Private);
		}

		private static void MergeListSignatures(List<NestedClassProcessor.VarFieldPair> 
			first, List<NestedClassProcessor.VarFieldPair> second, bool both)
		{
			int i = 1;
			while (first.Count > i && second.Count > i)
			{
				NestedClassProcessor.VarFieldPair fObj = first[first.Count - i];
				NestedClassProcessor.VarFieldPair sObj = second[second.Count - i];
				if (!IsEqual(both, fObj, sObj))
				{
					first[first.Count - i] = null;
					if (both)
					{
						second[second.Count - i] = null;
					}
				}
				else if (fObj != null)
				{
					if (fObj.varPair.var == -1)
					{
						fObj.varPair = sObj.varPair;
					}
					else
					{
						sObj.varPair = fObj.varPair;
					}
				}
				i++;
			}
			for (int j = 1; j <= first.Count - i; j++)
			{
				first[j] = null;
			}
			if (both)
			{
				for (int j = 1; j <= second.Count - i; j++)
				{
					second[j] = null;
				}
			}
			// first
			if ((first.Count == 0))
			{
				if (!(second.Count == 0) && both)
				{
					second[0] = null;
				}
			}
			else if ((second.Count == 0))
			{
				first[0] = null;
			}
			else
			{
				NestedClassProcessor.VarFieldPair fObj = first[0];
				NestedClassProcessor.VarFieldPair sObj = second[0];
				if (!IsEqual(both, fObj, sObj))
				{
					first[0] = null;
					if (both)
					{
						second[0] = null;
					}
				}
				else if (fObj != null)
				{
					if (fObj.varPair.var == -1)
					{
						fObj.varPair = sObj.varPair;
					}
					else
					{
						sObj.varPair = fObj.varPair;
					}
				}
			}
		}

		private static bool IsEqual(bool both, NestedClassProcessor.VarFieldPair fObj, NestedClassProcessor.VarFieldPair
			 sObj)
		{
			bool eq;
			if (fObj == null || sObj == null)
			{
				eq = (fObj == sObj);
			}
			else
			{
				eq = true;
				if (fObj.fieldKey.Length == 0)
				{
					fObj.fieldKey = sObj.fieldKey;
				}
				else if (sObj.fieldKey.Length == 0)
				{
					if (both)
					{
						sObj.fieldKey = fObj.fieldKey;
					}
				}
				else
				{
					eq = fObj.fieldKey.Equals(sObj.fieldKey);
				}
			}
			return eq;
		}

		private static void SetLocalClassDefinition(MethodWrapper method, ClassesProcessor.ClassNode
			 node)
		{
			RootStatement root = method.root;
			HashSet<Statement> setStats = new HashSet<Statement>();
			VarType classType = new VarType(node.classStruct.qualifiedName, true);
			Statement statement = GetDefStatement(root, classType, setStats);
			if (statement == null)
			{
				// unreferenced local class
				statement = root.GetFirst();
			}
			Statement first = FindFirstBlock(statement, setStats);
			List<Exprent> lst;
			if (first == null)
			{
				lst = statement.GetVarDefinitions();
			}
			else if (first.GetExprents() == null)
			{
				lst = first.GetVarDefinitions();
			}
			else
			{
				lst = first.GetExprents();
			}
			int addIndex = 0;
			foreach (Exprent expr in lst)
			{
				if (SearchForClass(expr, classType))
				{
					break;
				}
				addIndex++;
			}
			VarExprent var = new VarExprent(method.counter.GetCounterAndIncrement(CounterContainer
				.Var_Counter), classType, method.varproc);
			var.SetDefinition(true);
			var.SetClassDef(true);
			lst.Add(addIndex, var);
		}

		private static Statement FindFirstBlock(Statement stat, HashSet<Statement> setStats
			)
		{
			LinkedList<Statement> stack = new LinkedList<Statement>();
			stack.Add(stat);
			while (!(stack.Count == 0))
			{
				Statement st = stack.RemoveAtReturningValue(0);
				if ((stack.Count == 0) || setStats.Contains(st))
				{
					if (st.IsLabeled() && !(stack.Count == 0) || st.GetExprents() != null)
					{
						return st;
					}
					stack.Clear();
					switch (st.type)
					{
						case Statement.Type_Sequence:
						{
							stack.AddAll(0, st.GetStats());
							break;
						}

						case Statement.Type_If:
						case Statement.Type_Root:
						case Statement.Type_Switch:
						case Statement.Type_Syncronized:
						{
							stack.Add(st.GetFirst());
							break;
						}

						default:
						{
							return st;
						}
					}
				}
			}
			return null;
		}

		private static Statement GetDefStatement<_T0>(Statement stat, VarType classType, 
			HashSet<_T0> setStats)
		{
			List<Exprent> lst = new List<Exprent>();
			Statement retStat = null;
			if (stat.GetExprents() == null)
			{
				int counter = 0;
				foreach (object obj in stat.GetSequentialObjects())
				{
					if (obj is Statement)
					{
						Statement st = (Statement)obj;
						Statement stTemp = GetDefStatement(st, classType, setStats);
						if (stTemp != null)
						{
							if (counter == 1)
							{
								retStat = stat;
								break;
							}
							retStat = stTemp;
							counter++;
						}
						if (st.type == Statement.Type_Do)
						{
							DoStatement dost = (DoStatement)st;
							Sharpen.Collections.AddAll(lst, dost.GetInitExprentList());
							Sharpen.Collections.AddAll(lst, dost.GetConditionExprentList());
						}
					}
					else if (obj is Exprent)
					{
						lst.Add((Exprent)obj);
					}
				}
			}
			else
			{
				lst = stat.GetExprents();
			}
			if (retStat != stat)
			{
				foreach (Exprent exprent in lst)
				{
					if (exprent != null && SearchForClass(exprent, classType))
					{
						retStat = stat;
						break;
					}
				}
			}
			if (retStat != null)
			{
				setStats.Add(stat);
			}
			return retStat;
		}

		private static bool SearchForClass(Exprent exprent, VarType classType)
		{
			List<Exprent> lst = exprent.GetAllExprents(true);
			lst.Add(exprent);
			string classname = classType.value;
			foreach (Exprent expr in lst)
			{
				bool res = false;
				switch (expr.type)
				{
					case Exprent.Exprent_Const:
					{
						ConstExprent constExpr = (ConstExprent)expr;
						res = (VarType.Vartype_Class.Equals(constExpr.GetConstType()) && classname.Equals
							(constExpr.GetValue()) || classType.Equals(constExpr.GetConstType()));
						break;
					}

					case Exprent.Exprent_Field:
					{
						res = classname.Equals(((FieldExprent)expr).GetClassname());
						break;
					}

					case Exprent.Exprent_Invocation:
					{
						res = classname.Equals(((InvocationExprent)expr).GetClassname());
						break;
					}

					case Exprent.Exprent_New:
					{
						VarType newType = expr.GetExprType();
						res = newType.type == ICodeConstants.Type_Object && classname.Equals(newType.value
							);
						break;
					}

					case Exprent.Exprent_Var:
					{
						VarExprent varExpr = (VarExprent)expr;
						if (varExpr.IsDefinition())
						{
							VarType varType = varExpr.GetVarType();
							if (classType.Equals(varType) || (varType.arrayDim > 0 && classType.value.Equals(
								varType.value)))
							{
								res = true;
							}
						}
						break;
					}
				}
				if (res)
				{
					return true;
				}
			}
			return false;
		}

		private class VarFieldPair
		{
			public string fieldKey;

			public VarVersionPair varPair;

			internal VarFieldPair(string field, VarVersionPair varPair)
			{
				this.fieldKey = field;
				this.varPair = varPair;
			}

			public override bool Equals(object o)
			{
				if (o == this)
				{
					return true;
				}
				if (!(o is NestedClassProcessor.VarFieldPair))
				{
					return false;
				}
				NestedClassProcessor.VarFieldPair pair = (NestedClassProcessor.VarFieldPair)o;
				return fieldKey.Equals(pair.fieldKey) && varPair.Equals(pair.varPair);
			}

			public override int GetHashCode()
			{
				return fieldKey.GetHashCode() + varPair.GetHashCode();
			}
		}
	}
}
