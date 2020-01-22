// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using System.Linq;
using Java.Util;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructLocalVariableTableAttribute : StructGeneralAttribute
	{
		private List<StructLocalVariableTableAttribute.LocalVariable> localVariables = new System.Collections.Generic.List<
			StructLocalVariableTableAttribute.LocalVariable>();

		/*
		u2 local_variable_table_length;
		local_variable {
		u2 start_pc;
		u2 length;
		u2 name_index;
		u2 descriptor_index;
		u2 index;
		}
		*/
		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			int len = data.ReadUnsignedShort();
			if (len > 0)
			{
				localVariables = new List<StructLocalVariableTableAttribute.LocalVariable>(len);
				for (int i = 0; i < len; i++)
				{
					int start_pc = data.ReadUnsignedShort();
					int length = data.ReadUnsignedShort();
					int nameIndex = data.ReadUnsignedShort();
					int descriptorIndex = data.ReadUnsignedShort();
					int varIndex = data.ReadUnsignedShort();
					localVariables.Add(new StructLocalVariableTableAttribute.LocalVariable(start_pc, 
						length, pool.GetPrimitiveConstant(nameIndex).GetString(), pool.GetPrimitiveConstant
						(descriptorIndex).GetString(), varIndex));
				}
			}
			else
			{
				localVariables = new System.Collections.Generic.List<StructLocalVariableTableAttribute.LocalVariable
					>();
			}
		}

		public virtual void Add(StructLocalVariableTableAttribute attr)
		{
			Sharpen.Collections.AddAll(localVariables, attr.localVariables);
		}

		public virtual string GetName(int index, int visibleOffset)
		{
			return MatchingVars(index, visibleOffset).Select((StructLocalVariableTableAttribute.LocalVariable
				 v) => v.name).FirstOrDefault();
		}

		public virtual string GetDescriptor(int? index, int visibleOffset)
		{
			return MatchingVars(index, visibleOffset).Select((StructLocalVariableTableAttribute.LocalVariable
				v) => v.descriptor).FirstOrDefault();
		}

		private IEnumerable<StructLocalVariableTableAttribute.LocalVariable> MatchingVars(int?
			 index, int visibleOffset)
		{
			return localVariables.Where((StructLocalVariableTableAttribute.LocalVariable
				 v) => v.index == index && (visibleOffset >= v.start_pc && visibleOffset < v.start_pc
				 + v.length));
		}

		public virtual bool ContainsName(string name)
		{
			return localVariables.Any((StructLocalVariableTableAttribute.LocalVariable
				 v) => v.name.Equals(name));
		}

		public virtual Dictionary<int, string> GetMapParamNames()
		{
			return localVariables.Where((StructLocalVariableTableAttribute.LocalVariable
				 v) => v.start_pc == 0).ToDictionary((StructLocalVariableTableAttribute.LocalVariable
				 v) => v.index, (StructLocalVariableTableAttribute.LocalVariable v) => v.name);
		}

		private class LocalVariable
		{
			internal readonly int start_pc;

			internal readonly int length;

			internal readonly string name;

			internal readonly string descriptor;

			internal readonly int index;

			internal LocalVariable(int start_pc, int length, string name, string descriptor, int
				 index)
			{
				this.start_pc = start_pc;
				this.length = length;
				this.name = name;
				this.descriptor = descriptor;
				this.index = index;
			}
		}
	}
}
