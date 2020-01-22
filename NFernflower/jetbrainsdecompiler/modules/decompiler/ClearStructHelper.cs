// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class ClearStructHelper
	{
		public static void ClearStatements(RootStatement root)
		{
			LinkedList<Statement> stack = new LinkedList<Statement>();
			stack.Add(root);
			while (!(stack.Count == 0))
			{
				Statement stat = Sharpen.Collections.RemoveFirst(stack);
				stat.ClearTempInformation();
				Sharpen.Collections.AddAll(stack, stat.GetStats());
			}
		}
	}
}
