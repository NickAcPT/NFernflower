/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System.Collections.Generic;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class SwitchExprent : Exprent
	{
		private Exprent value;

		private List<List<Exprent>> caseValues = new List<List<Exprent>>();

		public SwitchExprent(Exprent value, HashSet<int> bytecodeOffsets)
			: base(Exprent_Switch)
		{
			this.value = value;
			AddBytecodeOffsets(bytecodeOffsets);
		}

		public override Exprent Copy()
		{
			SwitchExprent swExpr = new SwitchExprent(value.Copy(), bytecode);
			List<List<Exprent>> lstCaseValues = new List<List<Exprent>>();
			foreach (List<Exprent> lst in caseValues)
			{
				lstCaseValues.Add(new List<Exprent>(lst));
			}
			swExpr.SetCaseValues(lstCaseValues);
			return swExpr;
		}

		public override VarType GetExprType()
		{
			return value.GetExprType();
		}

		public override CheckTypesResult CheckExprTypeBounds()
		{
			CheckTypesResult result = new CheckTypesResult();
			result.AddMinTypeExprent(value, VarType.Vartype_Bytechar);
			result.AddMaxTypeExprent(value, VarType.Vartype_Int);
			VarType valType = value.GetExprType();
			foreach (List<Exprent> lst in caseValues)
			{
				foreach (Exprent expr in lst)
				{
					if (expr != null)
					{
						VarType caseType = expr.GetExprType();
						if (!caseType.Equals(valType))
						{
							valType = VarType.GetCommonSupertype(caseType, valType);
							result.AddMinTypeExprent(value, valType);
						}
					}
				}
			}
			return result;
		}

		public override List<Exprent> GetAllExprents()
		{
			List<Exprent> lst = new List<Exprent>();
			lst.Add(value);
			return lst;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			tracer.AddMapping(bytecode);
			return value.ToJava(indent, tracer).Enclose("switch(", ")");
		}

		public override void ReplaceExprent(Exprent oldExpr, Exprent newExpr)
		{
			if (oldExpr == value)
			{
				value = newExpr;
			}
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is SwitchExprent))
			{
				return false;
			}
			SwitchExprent sw = (SwitchExprent)o;
			return InterpreterUtil.EqualObjects(value, sw.GetValue());
		}

		public virtual Exprent GetValue()
		{
			return value;
		}

		public virtual void SetCaseValues(List<List<Exprent>> caseValues)
		{
			this.caseValues = caseValues;
		}
	}
}
