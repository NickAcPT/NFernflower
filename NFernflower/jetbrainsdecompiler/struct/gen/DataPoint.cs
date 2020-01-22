// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Gen
{
	public class DataPoint
	{
		private List<VarType> localVariables = new List<VarType>();

		private ListStack<VarType> stack = new ListStack<VarType>();

		public virtual void SetVariable(int index, VarType value)
		{
			if (index >= localVariables.Count)
			{
				for (int i = localVariables.Count; i <= index; i++)
				{
					localVariables.Add(new VarType(ICodeConstants.Type_Notinitialized));
				}
			}
			localVariables[index] = value;
		}

		public virtual VarType GetVariable(int index)
		{
			if (index < localVariables.Count)
			{
				return localVariables[index];
			}
			else
			{
				return new VarType(ICodeConstants.Type_Notinitialized);
			}
		}

		public virtual DataPoint Copy()
		{
			DataPoint point = new DataPoint();
			point.SetLocalVariables(new List<VarType>(localVariables));
			point.SetStack(((ListStack<VarType>)stack.Clone()));
			return point;
		}

		public static DataPoint GetInitialDataPoint(StructMethod mt)
		{
			DataPoint point = new DataPoint();
			MethodDescriptor md = MethodDescriptor.ParseDescriptor(mt.GetDescriptor());
			int k = 0;
			if (!mt.HasModifier(ICodeConstants.Acc_Static))
			{
				point.SetVariable(k++, new VarType(ICodeConstants.Type_Object, 0, null));
			}
			for (int i = 0; i < md.@params.Length; i++)
			{
				VarType var = md.@params[i];
				point.SetVariable(k++, var);
				if (var.stackSize == 2)
				{
					point.SetVariable(k++, new VarType(ICodeConstants.Type_Group2empty));
				}
			}
			return point;
		}

		public virtual List<VarType> GetLocalVariables()
		{
			return localVariables;
		}

		public virtual void SetLocalVariables(List<VarType> localVariables)
		{
			this.localVariables = localVariables;
		}

		public virtual ListStack<VarType> GetStack()
		{
			return stack;
		}

		public virtual void SetStack(ListStack<VarType> stack)
		{
			this.stack = stack;
		}
	}
}
