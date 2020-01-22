// Copyright 2000-2020 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class DummyExitStatement : Statement
	{
		public HashSet<int> bytecode = null;

		public DummyExitStatement()
		{
			// offsets of bytecode instructions mapped to dummy exit
			type = Statement.Type_Dummyexit;
		}

		public virtual void AddBytecodeOffsets(ICollection<int> bytecodeOffsets)
		{
			if (bytecodeOffsets != null && !(bytecodeOffsets.Count == 0))
			{
				if (bytecode == null)
				{
					bytecode = new HashSet<int>(bytecodeOffsets);
				}
				else
				{
					Sharpen.Collections.AddAll(bytecode, bytecodeOffsets);
				}
			}
		}
	}
}
