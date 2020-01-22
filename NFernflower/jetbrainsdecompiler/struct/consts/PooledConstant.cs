// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Code;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Consts
{
	public class PooledConstant : ICodeConstants
	{
		public readonly int type;

		public PooledConstant(int type)
		{
			this.type = type;
		}

		public virtual void ResolveConstant(ConstantPool pool)
		{
		}
	}
}
