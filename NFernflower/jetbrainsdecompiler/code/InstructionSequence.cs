// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Text;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Code
{
	public abstract class InstructionSequence
	{
		protected internal readonly VBStyleCollection<Instruction, int> collinstr;

		protected internal int pointer = 0;

		protected internal ExceptionTable exceptionTable = ExceptionTable.Empty;

		protected internal InstructionSequence()
			: this(new VBStyleCollection<Instruction, int>())
		{
		}

		protected internal InstructionSequence(VBStyleCollection<Instruction, int> collinstr
			)
		{
			// *****************************************************************************
			// private fields
			// *****************************************************************************
			this.collinstr = collinstr;
		}

		// *****************************************************************************
		// public methods
		// *****************************************************************************
		// to nbe overwritten
		public virtual InstructionSequence Clone()
		{
			return null;
		}

		public virtual void Clear()
		{
			collinstr.Clear();
			pointer = 0;
			exceptionTable = ExceptionTable.Empty;
		}

		public virtual void AddInstruction(Instruction inst, int offset)
		{
			collinstr.AddWithKey(inst, offset);
		}

		public virtual void AddInstruction(int index, Instruction inst, int offset)
		{
			collinstr.AddWithKeyAndIndex(index, inst, offset);
		}

		public virtual void AddSequence(InstructionSequence seq)
		{
			for (int i = 0; i < seq.Length(); i++)
			{
				AddInstruction(seq.GetInstr(i), -1);
			}
		}

		// TODO: any sensible value possible?
		public virtual void RemoveInstruction(int index)
		{
			collinstr.RemoveAtReturningValue(index);
		}

		public virtual void RemoveLast()
		{
			if (!(collinstr.Count == 0))
			{
				collinstr.RemoveAtReturningValue(collinstr.Count - 1);
			}
		}

		public virtual Instruction GetInstr(int index)
		{
			return collinstr[index];
		}

		public virtual Instruction GetLastInstr()
		{
			return collinstr.GetLast();
		}

		public virtual int GetOffset(int index)
		{
			return collinstr.GetKey(index);
		}

		public virtual int GetPointerByAbsOffset(int offset)
		{
			int absoffset = offset;
			if (collinstr.ContainsKey(absoffset))
			{
				return collinstr.GetIndexByKey(absoffset);
			}
			else
			{
				return -1;
			}
		}

		public virtual int GetPointerByRelOffset(int offset)
		{
			int absoffset = collinstr.GetKey(pointer) + offset;
			if (collinstr.ContainsKey(absoffset))
			{
				return collinstr.GetIndexByKey(absoffset);
			}
			else
			{
				return -1;
			}
		}

		public virtual int Length()
		{
			return collinstr.Count;
		}

		public virtual bool IsEmpty()
		{
			return (collinstr.Count == 0);
		}

		public virtual void AddToPointer(int diff)
		{
			this.pointer += diff;
		}

		public override string ToString()
		{
			return ToString(0);
		}

		public virtual string ToString(int indent)
		{
			string new_line_separator = DecompilerContext.GetNewLineSeparator();
			StringBuilder buf = new StringBuilder();
			for (int i = 0; i < collinstr.Count; i++)
			{
				buf.Append(TextUtil.GetIndentString(indent));
				buf.Append(collinstr.GetKey(i));
				buf.Append(": ");
				buf.Append(collinstr[i].ToString());
				buf.Append(new_line_separator);
			}
			return buf.ToString();
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual int GetPointer()
		{
			return pointer;
		}

		public virtual void SetPointer(int pointer)
		{
			this.pointer = pointer;
		}

		public virtual ExceptionTable GetExceptionTable()
		{
			return exceptionTable;
		}
	}
}
