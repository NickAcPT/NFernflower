// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Text;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Code
{
	public class Instruction : ICodeConstants
	{
		public static Instruction Create(int opcode, bool wide, int group, int bytecodeVersion
			, int[] operands)
		{
			if (opcode >= opc_ifeq && opcode <= opc_if_acmpne || opcode == opc_ifnull || opcode
				 == opc_ifnonnull || opcode == opc_jsr || opcode == opc_jsr_w || opcode == opc_goto
				 || opcode == opc_goto_w)
			{
				return new JumpInstruction(opcode, group, wide, bytecodeVersion, operands);
			}
			else if (opcode == opc_tableswitch || opcode == opc_lookupswitch)
			{
				return new SwitchInstruction(opcode, group, wide, bytecodeVersion, operands);
			}
			else
			{
				return new Instruction(opcode, group, wide, bytecodeVersion, operands);
			}
		}

		public static bool Equals(Instruction i1, Instruction i2)
		{
			return i1 != null && i2 != null && (i1 == i2 || i1.opcode == i2.opcode && i1.wide
				 == i2.wide && i1.OperandsCount() == i2.OperandsCount());
		}

		public readonly int opcode;

		public readonly int group;

		public readonly bool wide;

		public readonly int bytecodeVersion;

		protected internal readonly int[] operands;

		public Instruction(int opcode, int group, bool wide, int bytecodeVersion, int[] operands
			)
		{
			this.opcode = opcode;
			this.group = group;
			this.wide = wide;
			this.bytecodeVersion = bytecodeVersion;
			this.operands = operands;
		}

		public virtual void InitInstruction(InstructionSequence seq)
		{
		}

		public virtual int OperandsCount()
		{
			return operands == null ? 0 : operands.Length;
		}

		public virtual int Operand(int index)
		{
			return operands[index];
		}

		public virtual bool CanFallThrough()
		{
			return opcode != opc_goto && opcode != opc_goto_w && opcode != opc_ret && !(opcode
				 >= opc_ireturn && opcode <= opc_return) && opcode != opc_athrow && opcode != opc_jsr
				 && opcode != opc_tableswitch && opcode != opc_lookupswitch;
		}

		public override string ToString()
		{
			StringBuilder res = new StringBuilder();
			if (wide)
			{
				res.Append("@wide ");
			}
			res.Append("@").Append(TextUtil.GetInstructionName(opcode));
			int len = OperandsCount();
			for (int i = 0; i < len; i++)
			{
				int op = operands[i];
				if (op < 0)
				{
					res.Append(" -").Append(int.ToHexString(-op));
				}
				else
				{
					res.Append(" ").Append(int.ToHexString(op));
				}
			}
			return res.ToString();
		}

		public virtual Instruction Clone()
		{
			return Create(opcode, wide, group, bytecodeVersion, operands == null ? null : operands
				.MemberwiseClone());
		}
	}
}
