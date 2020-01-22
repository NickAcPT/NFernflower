// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Modules.Decompiler.Decompose;
using Sharpen;

namespace JetBrainsDecompiler.Code.Cfg
{
	public class BasicBlock : IIGraphNode
	{
		public int id;

		public int mark = 0;

		private InstructionSequence seq = new SimpleInstructionSequence();

		private readonly List<BasicBlock> preds = new List<BasicBlock>();

		private readonly List<BasicBlock> succs = new List<BasicBlock>();

		private readonly List<int> instrOldOffsets = new List<int>();

		private readonly List<BasicBlock> predExceptions = new List<BasicBlock>();

		private readonly List<BasicBlock> succExceptions = new List<BasicBlock>();

		public BasicBlock(int id)
		{
			// *****************************************************************************
			// public fields
			// *****************************************************************************
			// *****************************************************************************
			// private fields
			// *****************************************************************************
			this.id = id;
		}

		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public virtual BasicBlock Clone()
		{
			BasicBlock block = new BasicBlock(id);
			block.SetSeq(seq.Clone());
			Sharpen.Collections.AddAll(block.instrOldOffsets, instrOldOffsets);
			return block;
		}

		public virtual Instruction GetInstruction(int index)
		{
			return seq.GetInstr(index);
		}

		public virtual Instruction GetLastInstruction()
		{
			if (seq.IsEmpty())
			{
				return null;
			}
			else
			{
				return seq.GetLastInstr();
			}
		}

		public virtual int GetOldOffset(int index)
		{
			if (index < instrOldOffsets.Count)
			{
				return instrOldOffsets[index];
			}
			else
			{
				return -1;
			}
		}

		public virtual int Size()
		{
			return seq.Length();
		}

		public virtual void AddPredecessor(BasicBlock block)
		{
			preds.Add(block);
		}

		public virtual void RemovePredecessor(BasicBlock block)
		{
			while (preds.Remove(block))
			{
			}
		}

		/**/
		public virtual void AddSuccessor(BasicBlock block)
		{
			succs.Add(block);
			block.AddPredecessor(this);
		}

		public virtual void RemoveSuccessor(BasicBlock block)
		{
			while (succs.Remove(block))
			{
			}
			/**/
			block.RemovePredecessor(this);
		}

		// FIXME: unify block comparisons: id or direct equality
		public virtual void ReplaceSuccessor(BasicBlock oldBlock, BasicBlock newBlock)
		{
			for (int i = 0; i < succs.Count; i++)
			{
				if (succs[i].id == oldBlock.id)
				{
					succs[i] = newBlock;
					oldBlock.RemovePredecessor(this);
					newBlock.AddPredecessor(this);
				}
			}
			for (int i = 0; i < succExceptions.Count; i++)
			{
				if (succExceptions[i].id == oldBlock.id)
				{
					succExceptions[i] = newBlock;
					oldBlock.RemovePredecessorException(this);
					newBlock.AddPredecessorException(this);
				}
			}
		}

		public virtual void AddPredecessorException(BasicBlock block)
		{
			predExceptions.Add(block);
		}

		public virtual void RemovePredecessorException(BasicBlock block)
		{
			while (predExceptions.Remove(block))
			{
			}
		}

		/**/
		public virtual void AddSuccessorException(BasicBlock block)
		{
			if (!succExceptions.Contains(block))
			{
				succExceptions.Add(block);
				block.AddPredecessorException(this);
			}
		}

		public virtual void RemoveSuccessorException(BasicBlock block)
		{
			while (succExceptions.Remove(block))
			{
			}
			/**/
			block.RemovePredecessorException(this);
		}

		public override string ToString()
		{
			return ToString(0);
		}

		public virtual string ToString(int indent)
		{
			string new_line_separator = DecompilerContext.GetNewLineSeparator();
			return id + ":" + new_line_separator + seq.ToString(indent);
		}

		public virtual bool IsSuccessor(BasicBlock block)
		{
			foreach (BasicBlock succ in succs)
			{
				if (succ.id == block.id)
				{
					return true;
				}
			}
			return false;
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual List<int> GetInstrOldOffsets()
		{
			return instrOldOffsets;
		}

		public virtual List<IIGraphNode> GetPredecessors()
		{
			List<BasicBlock> lst = new List<BasicBlock>(preds);
			Sharpen.Collections.AddAll(lst, predExceptions);
			return lst;
		}

		public virtual List<BasicBlock> GetPreds()
		{
			return preds;
		}

		public virtual InstructionSequence GetSeq()
		{
			return seq;
		}

		public virtual void SetSeq(InstructionSequence seq)
		{
			this.seq = seq;
		}

		public virtual List<BasicBlock> GetSuccs()
		{
			return succs;
		}

		public virtual List<BasicBlock> GetSuccExceptions()
		{
			return succExceptions;
		}

		public virtual List<BasicBlock> GetPredExceptions()
		{
			return predExceptions;
		}
	}
}
