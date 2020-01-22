// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructGenericSignatureAttribute : StructGeneralAttribute
	{
		private string signature;

		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			int index = data.ReadUnsignedShort();
			signature = pool.GetPrimitiveConstant(index).GetString();
		}

		public virtual string GetSignature()
		{
			return signature;
		}
	}
}
