// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct
{
	public class StructField : StructMember
	{
		private readonly string name;

		private readonly string descriptor;

		/// <exception cref="System.IO.IOException"/>
		public StructField(DataInputFullStream @in, StructClass clStruct)
		{
			/*
			field_info {
			u2 access_flags;
			u2 name_index;
			u2 descriptor_index;
			u2 attributes_count;
			attribute_info attributes[attributes_count];
			}
			*/
			accessFlags = @in.ReadUnsignedShort();
			int nameIndex = @in.ReadUnsignedShort();
			int descriptorIndex = @in.ReadUnsignedShort();
			ConstantPool pool = clStruct.GetPool();
			string[] values = pool.GetClassElement(ConstantPool.Field, clStruct.qualifiedName
				, nameIndex, descriptorIndex);
			name = values[0];
			descriptor = values[1];
			attributes = ReadAttributes(@in, pool);
		}

		public virtual string GetName()
		{
			return name;
		}

		public virtual string GetDescriptor()
		{
			return descriptor;
		}

		public override string ToString()
		{
			return name;
		}
	}
}
