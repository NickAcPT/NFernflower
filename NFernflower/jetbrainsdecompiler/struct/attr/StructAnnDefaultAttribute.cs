// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructAnnDefaultAttribute : StructGeneralAttribute
	{
		private Exprent defaultValue;

		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			defaultValue = StructAnnotationAttribute.ParseAnnotationElement(data, pool);
		}

		public virtual Exprent GetDefaultValue()
		{
			return defaultValue;
		}
	}
}
