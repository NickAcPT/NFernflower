// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Code
{
	public class SimpleInstructionSequence : InstructionSequence
	{
		public SimpleInstructionSequence()
		{
		}

		public SimpleInstructionSequence(VBStyleCollection<Instruction, int> collinstr)
			: base(collinstr)
		{
		}

		public override InstructionSequence Clone()
		{
			SimpleInstructionSequence newseq = new SimpleInstructionSequence(((VBStyleCollection
				<Instruction, int>)collinstr.Clone()));
			newseq.SetPointer(this.GetPointer());
			return newseq;
		}
	}
}
