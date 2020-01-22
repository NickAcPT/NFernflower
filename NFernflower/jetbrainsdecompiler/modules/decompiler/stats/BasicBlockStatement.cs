// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Code.Cfg;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class BasicBlockStatement : Statement
	{
		private readonly BasicBlock block;

		public BasicBlockStatement(BasicBlock block)
		{
			// *****************************************************************************
			// private fields
			// *****************************************************************************
			// *****************************************************************************
			// constructors
			// *****************************************************************************
			type = Statement.Type_Basicblock;
			this.block = block;
			id = block.id;
			CounterContainer coun = DecompilerContext.GetCounterContainer();
			if (id >= coun.GetCounter(CounterContainer.Statement_Counter))
			{
				coun.SetCounter(CounterContainer.Statement_Counter, id + 1);
			}
			Instruction instr = block.GetLastInstruction();
			if (instr != null)
			{
				if (instr.group == ICodeConstants.Group_Jump && instr.opcode != ICodeConstants.opc_goto)
				{
					lastBasicType = Lastbasictype_If;
				}
				else if (instr.group == ICodeConstants.Group_Switch)
				{
					lastBasicType = Lastbasictype_Switch;
				}
			}
			// monitorenter and monitorexits
			BuildMonitorFlags();
		}

		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer tb = ExprProcessor.ListToJava(varDefinitions, indent, tracer);
			tb.Append(ExprProcessor.ListToJava(exprents, indent, tracer));
			return tb;
		}

		public override Statement GetSimpleCopy()
		{
			BasicBlock newblock = new BasicBlock(DecompilerContext.GetCounterContainer().GetCounterAndIncrement
				(CounterContainer.Statement_Counter));
			SimpleInstructionSequence seq = new SimpleInstructionSequence();
			for (int i = 0; i < block.GetSeq().Length(); i++)
			{
				seq.AddInstruction(block.GetSeq().GetInstr(i).Clone(), -1);
			}
			newblock.SetSeq(seq);
			return new BasicBlockStatement(newblock);
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual BasicBlock GetBlock()
		{
			return block;
		}
	}
}
