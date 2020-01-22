// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructExceptionsAttribute : StructGeneralAttribute
	{
		private List<int> throwsExceptions;

		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			int len = data.ReadUnsignedShort();
			if (len > 0)
			{
				throwsExceptions = new List<int>(len);
				for (int i = 0; i < len; i++)
				{
					throwsExceptions.Add(data.ReadUnsignedShort());
				}
			}
			else
			{
				throwsExceptions = new System.Collections.Generic.List<int>();
			}
		}

		public virtual string GetExcClassname(int index, ConstantPool pool)
		{
			return pool.GetPrimitiveConstant(throwsExceptions[index]).GetString();
		}

		public virtual List<int> GetThrowsExceptions()
		{
			return throwsExceptions;
		}
	}
}
