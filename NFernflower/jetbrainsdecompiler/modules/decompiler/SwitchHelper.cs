// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class SwitchHelper
	{
		public static void Simplify(SwitchStatement switchStatement)
		{
			SwitchExprent switchExprent = (SwitchExprent)switchStatement.GetHeadexprent();
			Exprent value = switchExprent.GetValue();
			if (IsEnumArray(value))
			{
				List<List<Exprent>> caseValues = switchStatement.GetCaseValues();
				Dictionary<Exprent, Exprent> mapping = new Dictionary<Exprent, Exprent>(caseValues
					.Count);
				ArrayExprent array = (ArrayExprent)value;
				FieldExprent arrayField = (FieldExprent)array.GetArray();
				ClassesProcessor.ClassNode classNode = DecompilerContext.GetClassProcessor().GetMapRootClasses
					().GetOrNull(arrayField.GetClassname());
				if (classNode != null)
				{
					MethodWrapper wrapper = classNode.GetWrapper().GetMethodWrapper(ICodeConstants.Clinit_Name
						, "()V");
					if (wrapper != null && wrapper.root != null)
					{
						wrapper.GetOrBuildGraph().IterateExprents((Exprent exprent) => 						{
								if (exprent is AssignmentExprent)
								{
									AssignmentExprent assignment = (AssignmentExprent)exprent;
									Exprent left = assignment.GetLeft();
									if (left.type == Exprent.Exprent_Array && ((ArrayExprent)left).GetArray().Equals(
										    arrayField))
									{
										Sharpen.Collections.Put(mapping, assignment.GetRight(), ((InvocationExprent)((ArrayExprent
											)left).GetIndex()).GetInstance());
									}
								}
								return 0;
							}
);
					}
				}
				List<List<Exprent>> realCaseValues = new List<List<Exprent>>(caseValues.Count);
				foreach (List<Exprent> caseValue in caseValues)
				{
					List<Exprent> values = new List<Exprent>(caseValue.Count);
					realCaseValues.Add(values);
					foreach (Exprent exprent in caseValue)
					{
						if (exprent == null)
						{
							values.Add(null);
						}
						else
						{
							Exprent realConst = mapping.GetOrNull(exprent);
							if (realConst == null)
							{
								DecompilerContext.GetLogger().WriteMessage("Unable to simplify switch on enum: " 
									+ exprent + " not found, available: " + mapping, IFernflowerLogger.Severity.Error
									);
								return;
							}
							values.Add(realConst.Copy());
						}
					}
				}
				caseValues.Clear();
				Sharpen.Collections.AddAll(caseValues, realCaseValues);
				switchExprent.ReplaceExprent(value, ((InvocationExprent)array.GetIndex()).GetInstance
					().Copy());
			}
		}

		private static bool IsEnumArray(Exprent exprent)
		{
			if (exprent is ArrayExprent)
			{
				Exprent field = ((ArrayExprent)exprent).GetArray();
				Exprent index = ((ArrayExprent)exprent).GetIndex();
				return field is FieldExprent && (((FieldExprent)field).GetName().StartsWith("$SwitchMap"
					) || (index is InvocationExprent && ((InvocationExprent)index).GetName().Equals(
					"ordinal")));
			}
			return false;
		}
	}
}
