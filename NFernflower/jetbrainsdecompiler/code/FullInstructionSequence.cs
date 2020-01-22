// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Code
{
	public class FullInstructionSequence : InstructionSequence
	{
		public FullInstructionSequence(VBStyleCollection<Instruction, int> collinstr, ExceptionTable
			 extable)
			: base(collinstr)
		{
			// *****************************************************************************
			// constructors
			// *****************************************************************************
			this.exceptionTable = extable;
			// translate raw exception handlers to instr
			foreach (ExceptionHandler handler in extable.GetHandlers())
			{
				handler.from_instr = this.GetPointerByAbsOffset(handler.from);
				handler.to_instr = this.GetPointerByAbsOffset(handler.to);
				handler.handler_instr = this.GetPointerByAbsOffset(handler.handler);
			}
		}
	}
}
