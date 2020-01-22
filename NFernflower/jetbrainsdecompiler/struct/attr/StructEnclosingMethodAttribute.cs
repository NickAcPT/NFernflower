// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructEnclosingMethodAttribute : StructGeneralAttribute
	{
		private string className;

		private string methodName;

		private string methodDescriptor;

		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			int classIndex = data.ReadUnsignedShort();
			int methodIndex = data.ReadUnsignedShort();
			className = pool.GetPrimitiveConstant(classIndex).GetString();
			if (methodIndex != 0)
			{
				LinkConstant lk = pool.GetLinkConstant(methodIndex);
				methodName = lk.elementname;
				methodDescriptor = lk.descriptor;
			}
		}

		public virtual string GetClassName()
		{
			return className;
		}

		public virtual string GetMethodDescriptor()
		{
			return methodDescriptor;
		}

		public virtual string GetMethodName()
		{
			return methodName;
		}
	}
}
