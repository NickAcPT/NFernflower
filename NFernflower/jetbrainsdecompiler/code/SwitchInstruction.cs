// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Code
{
	public class SwitchInstruction : Instruction
	{
		private int[] destinations;

		private int[] values;

		private int defaultDestination;

		public SwitchInstruction(int opcode, int group, bool wide, int bytecodeVersion, int
			[] operands)
			: base(opcode, group, wide, bytecodeVersion, operands)
		{
		}

		public override void InitInstruction(InstructionSequence seq)
		{
			defaultDestination = seq.GetPointerByRelOffset(operands[0]);
			int prefix = opcode == ICodeConstants.opc_tableswitch ? 3 : 2;
			int len = operands.Length - prefix;
			int low = 0;
			if (opcode == ICodeConstants.opc_lookupswitch)
			{
				len /= 2;
			}
			else
			{
				low = operands[1];
			}
			destinations = new int[len];
			values = new int[len];
			for (int i = 0, k = 0; i < len; i++, k++)
			{
				if (opcode == ICodeConstants.opc_lookupswitch)
				{
					values[i] = operands[prefix + k];
					k++;
				}
				else
				{
					values[i] = low + k;
				}
				destinations[i] = seq.GetPointerByRelOffset(operands[prefix + k]);
			}
		}

		public virtual int[] GetDestinations()
		{
			return destinations;
		}

		public virtual int[] GetValues()
		{
			return values;
		}

		public virtual int GetDefaultDestination()
		{
			return defaultDestination;
		}

		public override Instruction Clone()
		{
			SwitchInstruction copy = (SwitchInstruction)base.Clone();
			copy.defaultDestination = defaultDestination;
			copy.destinations = (int[]) destinations.Clone();
			copy.values = (int[]) values.Clone();
			return copy;
		}
	}
}
