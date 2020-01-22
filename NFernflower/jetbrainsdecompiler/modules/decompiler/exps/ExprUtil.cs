// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class ExprUtil
	{
		public static List<VarVersionPair> GetSyntheticParametersMask(string className, 
			string descriptor, int parameters)
		{
			ClassesProcessor.ClassNode node = DecompilerContext.GetClassProcessor().GetMapRootClasses
				().GetOrNull(className);
			return node != null ? GetSyntheticParametersMask(node, descriptor, parameters) : 
				null;
		}

		public static List<VarVersionPair> GetSyntheticParametersMask(ClassesProcessor.ClassNode
			 node, string descriptor, int parameters)
		{
			List<VarVersionPair> mask = null;
			ClassWrapper wrapper = node.GetWrapper();
			if (wrapper != null)
			{
				// own class
				MethodWrapper methodWrapper = wrapper.GetMethodWrapper(ICodeConstants.Init_Name, 
					descriptor);
				if (methodWrapper == null)
				{
					if (DecompilerContext.GetOption(IFernflowerPreferences.Ignore_Invalid_Bytecode))
					{
						return null;
					}
					throw new Exception("Constructor " + node.classStruct.qualifiedName + "." + ICodeConstants
						.Init_Name + descriptor + " not found");
				}
				mask = methodWrapper.synthParameters;
			}
			else if (parameters > 0 && node.type == ClassesProcessor.ClassNode.Class_Member &&
				 (node.access & ICodeConstants.Acc_Static) == 0)
			{
				// non-static member class
				mask = new List<VarVersionPair>(Enumerable.Repeat<VarVersionPair>(null, parameters));
				mask[0] = new VarVersionPair(-1, 0);
			}
			return mask;
		}
	}
}
