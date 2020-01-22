// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructLocalVariableTypeTableAttribute : StructGeneralAttribute
	{
		private readonly StructLocalVariableTableAttribute backingAttribute = new StructLocalVariableTableAttribute
			();

		/*
		u2 local_variable_type_table_length;
		{   u2 start_pc;
		u2 length;
		u2 name_index;
		u2 signature_index;
		u2 index;
		} local_variable_type_table[local_variable_type_table_length];
		*/
		// store signature instead of descriptor
		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			backingAttribute.InitContent(data, pool);
		}

		public virtual void Add(StructLocalVariableTypeTableAttribute attr)
		{
			backingAttribute.Add(attr.backingAttribute);
		}

		public virtual string GetSignature(int index, int visibleOffset)
		{
			return backingAttribute.GetDescriptor(index, visibleOffset);
		}
	}
}
