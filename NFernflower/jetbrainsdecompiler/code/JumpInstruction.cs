// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Code
{
	public class JumpInstruction : Instruction
	{
		public int destination;

		public JumpInstruction(int opcode, int group, bool wide, int bytecodeVersion, int
			[] operands)
			: base(opcode, group, wide, bytecodeVersion, operands)
		{
		}

		public override void InitInstruction(InstructionSequence seq)
		{
			destination = seq.GetPointerByRelOffset(this.Operand(0));
		}

		public override Instruction Clone()
		{
			JumpInstruction copy = (JumpInstruction)base.Clone();
			copy.destination = destination;
			return copy;
		}
	}
}
