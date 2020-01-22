// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class PrimitiveExprsList
	{
		private readonly List<Exprent> lstExprents = new List<Exprent>();

		private ExprentStack stack = new ExprentStack();

		public PrimitiveExprsList()
		{
		}

		public virtual PrimitiveExprsList CopyStack()
		{
			PrimitiveExprsList prlst = new PrimitiveExprsList();
			prlst.SetStack(((ExprentStack)stack.Clone()));
			return prlst;
		}

		public virtual List<Exprent> GetLstExprents()
		{
			return lstExprents;
		}

		public virtual ExprentStack GetStack()
		{
			return stack;
		}

		public virtual void SetStack(ExprentStack stack)
		{
			this.stack = stack;
		}
	}
}
