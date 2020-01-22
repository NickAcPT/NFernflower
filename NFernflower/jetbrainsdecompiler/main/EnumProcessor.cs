// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main
{
	public class EnumProcessor
	{
		public static void ClearEnum(ClassWrapper wrapper)
		{
			StructClass cl = wrapper.GetClassStruct();
			// hide values/valueOf methods and super() invocations
			foreach (MethodWrapper method in wrapper.GetMethods())
			{
				StructMethod mt = method.methodStruct;
				string name = mt.GetName();
				string descriptor = mt.GetDescriptor();
				if ("values".Equals(name))
				{
					if (descriptor.Equals("()[L" + cl.qualifiedName + ";"))
					{
						wrapper.GetHiddenMembers().Add(InterpreterUtil.MakeUniqueKey(name, descriptor));
					}
				}
				else if ("valueOf".Equals(name))
				{
					if (descriptor.Equals("(Ljava/lang/String;)L" + cl.qualifiedName + ";"))
					{
						wrapper.GetHiddenMembers().Add(InterpreterUtil.MakeUniqueKey(name, descriptor));
					}
				}
				else if (ICodeConstants.Init_Name.Equals(name))
				{
					Statement firstData = Statements.FindFirstData(method.root);
					if (firstData != null && !(firstData.GetExprents().Count == 0))
					{
						Exprent exprent = firstData.GetExprents()[0];
						if (exprent.type == Exprent.Exprent_Invocation)
						{
							InvocationExprent invExpr = (InvocationExprent)exprent;
							if (Statements.IsInvocationInitConstructor(invExpr, method, wrapper, false))
							{
								firstData.GetExprents().RemoveAtReturningValue(0);
							}
						}
					}
				}
			}
			// hide synthetic fields of enum and it's constants
			foreach (StructField fd in cl.GetFields())
			{
				string descriptor = fd.GetDescriptor();
				if (fd.IsSynthetic() && descriptor.Equals("[L" + cl.qualifiedName + ";"))
				{
					wrapper.GetHiddenMembers().Add(InterpreterUtil.MakeUniqueKey(fd.GetName(), descriptor
						));
				}
			}
		}
	}
}
