// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.Threading;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main.Rels
{
	public class ClassWrapper
	{
		private readonly StructClass classStruct;

		private readonly HashSet<string> hiddenMembers = new HashSet<string>();

		private readonly VBStyleCollection<Exprent, string> staticFieldInitializers = new 
			VBStyleCollection<Exprent, string>();

		private readonly VBStyleCollection<Exprent, string> dynamicFieldInitializers = new 
			VBStyleCollection<Exprent, string>();

		private readonly VBStyleCollection<MethodWrapper, string> methods = new VBStyleCollection
			<MethodWrapper, string>();

		public ClassWrapper(StructClass classStruct)
		{
			this.classStruct = classStruct;
		}

		public virtual void Init()
		{
			DecompilerContext.SetProperty(DecompilerContext.Current_Class, classStruct);
			DecompilerContext.SetProperty(DecompilerContext.Current_Class_Wrapper, this);
			DecompilerContext.GetLogger().StartClass(classStruct.qualifiedName);
			int maxSec = System.Convert.ToInt32(DecompilerContext.GetProperty(IIFernflowerPreferences
				.Max_Processing_Method).ToString());
			bool testMode = DecompilerContext.GetOption(IIFernflowerPreferences.Unit_Test_Mode
				);
			foreach (StructMethod mt in classStruct.GetMethods())
			{
				DecompilerContext.GetLogger().StartMethod(mt.GetName() + " " + mt.GetDescriptor()
					);
				MethodDescriptor md = MethodDescriptor.ParseDescriptor(mt.GetDescriptor());
				VarProcessor varProc = new VarProcessor(mt, md);
				DecompilerContext.StartMethod(varProc);
				VarNamesCollector vc = varProc.GetVarNamesCollector();
				CounterContainer counter = DecompilerContext.GetCounterContainer();
				RootStatement root = null;
				bool isError = false;
				try
				{
					if (mt.ContainsCode())
					{
						if (maxSec == 0 || testMode)
						{
							root = MethodProcessorRunnable.CodeToJava(mt, md, varProc);
						}
						else
						{
							MethodProcessorRunnable mtProc = new MethodProcessorRunnable(mt, md, varProc, DecompilerContext
								.GetCurrentContext());
							Thread mtThread = new Thread(mtProc, "Java decompiler");
							long stopAt = Runtime.CurrentTimeMillis() + maxSec * 1000L;
							mtThread.Start();
							while (!mtProc.IsFinished())
							{
								try
								{
									lock (mtProc.Lock)
									{
										Sharpen.Runtime.Wait(mtProc.Lock, 200);
									}
								}
								catch (Exception e)
								{
									KillThread(mtThread);
									throw;
								}
								if (Runtime.CurrentTimeMillis() >= stopAt)
								{
									string message = "Processing time limit exceeded for method " + mt.GetName() + ", execution interrupted.";
									DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Error
										);
									KillThread(mtThread);
									isError = true;
									break;
								}
							}
							if (!isError)
							{
								root = mtProc.GetResult();
							}
						}
					}
					else
					{
						bool thisVar = !mt.HasModifier(ICodeConstants.Acc_Static);
						int paramCount = 0;
						if (thisVar)
						{
							Sharpen.Collections.Put(varProc.GetThisVars(), new VarVersionPair(0, 0), classStruct
								.qualifiedName);
							paramCount = 1;
						}
						paramCount += md.@params.Length;
						int varIndex = 0;
						for (int i = 0; i < paramCount; i++)
						{
							varProc.SetVarName(new VarVersionPair(varIndex, 0), vc.GetFreeName(varIndex));
							if (thisVar)
							{
								if (i == 0)
								{
									varIndex++;
								}
								else
								{
									varIndex += md.@params[i - 1].stackSize;
								}
							}
							else
							{
								varIndex += md.@params[i].stackSize;
							}
						}
					}
				}
				catch (Exception t)
				{
					string message = "Method " + mt.GetName() + " " + mt.GetDescriptor() + " couldn't be decompiled.";
					DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
						, t);
					isError = true;
				}
				MethodWrapper methodWrapper = new MethodWrapper(root, varProc, mt, counter);
				methodWrapper.decompiledWithErrors = isError;
				methods.AddWithKey(methodWrapper, InterpreterUtil.MakeUniqueKey(mt.GetName(), mt.
					GetDescriptor()));
				if (!isError)
				{
					// rename vars so that no one has the same name as a field
					VarNamesCollector namesCollector = new VarNamesCollector();
					classStruct.GetFields().ForEach((StructField f) => namesCollector.AddName(f.GetName
						()));
					varProc.RefreshVarNames(namesCollector);
					// if debug information present and should be used
					if (DecompilerContext.GetOption(IIFernflowerPreferences.Use_Debug_Var_Names))
					{
						StructLocalVariableTableAttribute attr = mt.GetLocalVariableAttr();
						if (attr != null)
						{
							// only param names here
							varProc.SetDebugVarNames(attr.GetMapParamNames());
							// the rest is here
							methodWrapper.GetOrBuildGraph().IterateExprents((Exprent exprent) => 							{
								List<Exprent> lst = exprent.GetAllExprents(true);
								lst.Add(exprent);
								lst.Stream().Filter((Exprent e) => e.type == Exprent.Exprent_Var).ForEach((Exprent
									 e) => 								{
									VarExprent varExprent = (VarExprent)e;
									string name = varExprent.GetDebugName(mt);
									if (name != null)
									{
										varProc.SetVarName(varExprent.GetVarVersionPair(), name);
									}
								}
);
								return 0;
							}
);
						}
					}
				}
				DecompilerContext.GetLogger().EndMethod();
			}
			DecompilerContext.GetLogger().EndClass();
		}

		private static void KillThread(Thread thread)
		{
			thread.Stop();
		}

		public virtual MethodWrapper GetMethodWrapper(string name, string descriptor)
		{
			return methods.GetWithKey(InterpreterUtil.MakeUniqueKey(name, descriptor));
		}

		public virtual StructClass GetClassStruct()
		{
			return classStruct;
		}

		public virtual VBStyleCollection<MethodWrapper, string> GetMethods()
		{
			return methods;
		}

		public virtual HashSet<string> GetHiddenMembers()
		{
			return hiddenMembers;
		}

		public virtual VBStyleCollection<Exprent, string> GetStaticFieldInitializers()
		{
			return staticFieldInitializers;
		}

		public virtual VBStyleCollection<Exprent, string> GetDynamicFieldInitializers()
		{
			return dynamicFieldInitializers;
		}

		public override string ToString()
		{
			return classStruct.qualifiedName;
		}
	}
}
