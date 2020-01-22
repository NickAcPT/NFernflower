// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class Statements
	{
		public static Statement FindFirstData(Statement stat)
		{
			if (stat.GetExprents() != null)
			{
				return stat;
			}
			else if (stat.IsLabeled())
			{
				// FIXME: Why??
				return null;
			}
			switch (stat.type)
			{
				case Statement.Type_Sequence:
				case Statement.Type_If:
				case Statement.Type_Root:
				case Statement.Type_Switch:
				case Statement.Type_Syncronized:
				{
					return FindFirstData(stat.GetFirst());
				}

				default:
				{
					return null;
				}
			}
		}

		public static bool IsInvocationInitConstructor(InvocationExprent inv, MethodWrapper
			 method, ClassWrapper wrapper, bool withThis)
		{
			if (inv.GetFunctype() == InvocationExprent.Typ_Init && inv.GetInstance().type == 
				Exprent.Exprent_Var)
			{
				VarExprent instVar = (VarExprent)inv.GetInstance();
				VarVersionPair varPair = new VarVersionPair(instVar);
				string className = method.varproc.GetThisVars().GetOrNull(varPair);
				if (className != null)
				{
					// any this instance. TODO: Restrict to current class?
					return withThis || !wrapper.GetClassStruct().qualifiedName.Equals(inv.GetClassname
						());
				}
			}
			return false;
		}
	}
}
