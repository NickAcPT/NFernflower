/*
* Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System.Collections.Generic;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructMethodParametersAttribute : StructGeneralAttribute
	{
		private List<StructMethodParametersAttribute.Entry> myEntries;

		/*
		u1 parameters_count;
		{   u2 name_index;
		u2 access_flags;
		} parameters[parameters_count];
		*/
		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			int len = data.ReadUnsignedByte();
			List<StructMethodParametersAttribute.Entry> entries;
			if (len > 0)
			{
				entries = new List<StructMethodParametersAttribute.Entry>(len);
				for (int i = 0; i < len; i++)
				{
					int nameIndex = data.ReadUnsignedShort();
					string name = nameIndex != 0 ? pool.GetPrimitiveConstant(nameIndex).GetString() : 
						null;
					int access_flags = data.ReadUnsignedShort();
					entries.Add(new StructMethodParametersAttribute.Entry(name, access_flags));
				}
			}
			else
			{
				entries = new System.Collections.Generic.List<StructMethodParametersAttribute.Entry
					>();
			}
			myEntries = (entries);
		}

		public virtual List<StructMethodParametersAttribute.Entry> GetEntries()
		{
			return myEntries;
		}

		public class Entry
		{
			public readonly string myName;

			public readonly int myAccessFlags;

			public Entry(string name, int accessFlags)
			{
				myName = name;
				myAccessFlags = accessFlags;
			}
		}
	}
}
