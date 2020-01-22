// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructInnerClassesAttribute : StructGeneralAttribute
	{
		public class Entry
		{
			public readonly int outerNameIdx;

			public readonly int simpleNameIdx;

			public readonly int accessFlags;

			public readonly string innerName;

			public readonly string enclosingName;

			public readonly string simpleName;

			internal Entry(int outerNameIdx, int simpleNameIdx, int accessFlags, string innerName
				, string enclosingName, string simpleName)
			{
				this.outerNameIdx = outerNameIdx;
				this.simpleNameIdx = simpleNameIdx;
				this.accessFlags = accessFlags;
				this.innerName = innerName;
				this.enclosingName = enclosingName;
				this.simpleName = simpleName;
			}
		}

		private List<StructInnerClassesAttribute.Entry> entries;

		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			int len = data.ReadUnsignedShort();
			if (len > 0)
			{
				entries = new List<StructInnerClassesAttribute.Entry>(len);
				for (int i = 0; i < len; i++)
				{
					int innerNameIdx = data.ReadUnsignedShort();
					int outerNameIdx = data.ReadUnsignedShort();
					int simpleNameIdx = data.ReadUnsignedShort();
					int accessFlags = data.ReadUnsignedShort();
					string innerName = pool.GetPrimitiveConstant(innerNameIdx).GetString();
					string outerName = outerNameIdx != 0 ? pool.GetPrimitiveConstant(outerNameIdx).GetString
						() : null;
					string simpleName = simpleNameIdx != 0 ? pool.GetPrimitiveConstant(simpleNameIdx)
						.GetString() : null;
					entries.Add(new StructInnerClassesAttribute.Entry(outerNameIdx, simpleNameIdx, accessFlags
						, innerName, outerName, simpleName));
				}
			}
			else
			{
				entries = new System.Collections.Generic.List<StructInnerClassesAttribute.Entry>();
			}
		}

		public virtual List<StructInnerClassesAttribute.Entry> GetEntries()
		{
			return entries;
		}
	}
}
