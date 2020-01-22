// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	[System.Serializable]
	public class ExprentStack : ListStack<Exprent>
	{
		public ExprentStack()
		{
		}

		public ExprentStack(ListStack<Exprent> list)
			: base(list)
		{
			pointer = list.GetPointer();
		}

		public override Exprent Pop()
		{
			return this.RemoveAtReturningValue(--pointer);
		}

		public override object Clone()
		{
			return new ExprentStack(this);
		}
	}
}
