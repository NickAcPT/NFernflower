// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.Text;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Code.Interpreter;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Modules.Code;
using JetBrainsDecompiler.Modules.Decompiler.Decompose;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Code.Cfg
{
	public class ControlFlowGraph : ICodeConstants
	{
		public int last_id = 0;

		private VBStyleCollection<BasicBlock, int> blocks;

		private BasicBlock first;

		private BasicBlock last;

		private List<ExceptionRangeCFG> exceptions;

		private Dictionary<BasicBlock, BasicBlock> subroutines;

		private readonly HashSet<BasicBlock> finallyExits = new HashSet<BasicBlock>();

		public ControlFlowGraph(InstructionSequence seq)
		{
			// *****************************************************************************
			// private fields
			// *****************************************************************************
			// *****************************************************************************
			// constructors
			// *****************************************************************************
			BuildBlocks(seq);
		}

		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public virtual void RemoveMarkers()
		{
			foreach (BasicBlock block in blocks)
			{
				block.mark = 0;
			}
		}

		public override string ToString()
		{
			if (blocks == null)
			{
				return "Empty";
			}
			string new_line_separator = DecompilerContext.GetNewLineSeparator();
			StringBuilder buf = new StringBuilder();
			foreach (BasicBlock block in blocks)
			{
				buf.Append("----- Block ").Append(block.id).Append(" -----").Append(new_line_separator
					);
				buf.Append(block.ToString());
				buf.Append("----- Edges -----").Append(new_line_separator);
				List<BasicBlock> suc = block.GetSuccs();
				foreach (BasicBlock aSuc in suc)
				{
					buf.Append(">>>>>>>>(regular) Block ").Append(aSuc.id).Append(new_line_separator);
				}
				suc = block.GetSuccExceptions();
				foreach (BasicBlock handler in suc)
				{
					ExceptionRangeCFG range = GetExceptionRange(handler, block);
					if (range == null)
					{
						buf.Append(">>>>>>>>(exception) Block ").Append(handler.id).Append("\t").Append("ERROR: range not found!"
							).Append(new_line_separator);
					}
					else
					{
						List<string> exceptionTypes = range.GetExceptionTypes();
						if (exceptionTypes == null)
						{
							buf.Append(">>>>>>>>(exception) Block ").Append(handler.id).Append("\t").Append("NULL"
								).Append(new_line_separator);
						}
						else
						{
							foreach (string exceptionType in exceptionTypes)
							{
								buf.Append(">>>>>>>>(exception) Block ").Append(handler.id).Append("\t").Append(exceptionType
									).Append(new_line_separator);
							}
						}
					}
				}
				buf.Append("----- ----- -----").Append(new_line_separator);
			}
			return buf.ToString();
		}

		public virtual void InlineJsr(StructMethod mt)
		{
			ProcessJsr();
			RemoveJsr(mt);
			RemoveMarkers();
			DeadCodeHelper.RemoveEmptyBlocks(this);
		}

		public virtual void RemoveBlock(BasicBlock block)
		{
			while (block.GetSuccs().Count > 0)
			{
				block.RemoveSuccessor(block.GetSuccs()[0]);
			}
			while (block.GetSuccExceptions().Count > 0)
			{
				block.RemoveSuccessorException(block.GetSuccExceptions()[0]);
			}
			while (block.GetPreds().Count > 0)
			{
				block.GetPreds()[0].RemoveSuccessor(block);
			}
			while (block.GetPredExceptions().Count > 0)
			{
				block.GetPredExceptions()[0].RemoveSuccessorException(block);
			}
			last.RemovePredecessor(block);
			blocks.RemoveWithKey(block.id);
			for (int i = exceptions.Count - 1; i >= 0; i--)
			{
				ExceptionRangeCFG range = exceptions[i];
				if (range.GetHandler() == block)
				{
					exceptions.RemoveAtReturningValue(i);
				}
				else
				{
					List<BasicBlock> lstRange = range.GetProtectedRange();
					lstRange.Remove(block);
					if ((lstRange.Count == 0))
					{
						exceptions.RemoveAtReturningValue(i);
					}
				}
			}
			subroutines.RemoveIf(ent => ent.Key == block
			                              || ent.Value == block);
		}

		public virtual ExceptionRangeCFG GetExceptionRange(BasicBlock handler, BasicBlock
			 block)
		{
			//List<ExceptionRangeCFG> ranges = new ArrayList<ExceptionRangeCFG>();
			for (int i = exceptions.Count - 1; i >= 0; i--)
			{
				ExceptionRangeCFG range = exceptions[i];
				if (range.GetHandler() == handler && range.GetProtectedRange().Contains(block))
				{
					return range;
				}
			}
			//ranges.add(range);
			return null;
		}

		//return ranges.isEmpty() ? null : ranges;
		//	public String getExceptionsUniqueString(BasicBlock handler, BasicBlock block) {
		//
		//		List<ExceptionRangeCFG> ranges = getExceptionRange(handler, block);
		//
		//		if(ranges == null) {
		//			return null;
		//		} else {
		//			Set<String> setExceptionStrings = new HashSet<String>();
		//			for(ExceptionRangeCFG range : ranges) {
		//				setExceptionStrings.add(range.getExceptionType());
		//			}
		//
		//			String ret = "";
		//			for(String exception : setExceptionStrings) {
		//				ret += exception;
		//			}
		//
		//			return ret;
		//		}
		//	}
		// *****************************************************************************
		// private methods
		// *****************************************************************************
		private void BuildBlocks(InstructionSequence instrseq)
		{
			short[] states = FindStartInstructions(instrseq);
			Dictionary<int, BasicBlock> mapInstrBlocks = new Dictionary<int, BasicBlock>();
			VBStyleCollection<BasicBlock, int> colBlocks = CreateBasicBlocks(states, instrseq
				, mapInstrBlocks);
			blocks = colBlocks;
			ConnectBlocks(colBlocks, mapInstrBlocks);
			SetExceptionEdges(instrseq, mapInstrBlocks);
			SetSubroutineEdges();
			SetFirstAndLastBlocks();
		}

		private static short[] FindStartInstructions(InstructionSequence seq)
		{
			int len = seq.Length();
			short[] inststates = new short[len];
			HashSet<int> excSet = new HashSet<int>();
			foreach (ExceptionHandler handler in seq.GetExceptionTable().GetHandlers())
			{
				excSet.Add(handler.from_instr);
				excSet.Add(handler.to_instr);
				excSet.Add(handler.handler_instr);
			}
			for (int i = 0; i < len; i++)
			{
				// exception blocks
				if (excSet.Contains(i))
				{
					inststates[i] = 1;
				}
				Instruction instr = seq.GetInstr(i);
				switch (instr.group)
				{
					case Group_Jump:
					{
						inststates[((JumpInstruction)instr).destination] = 1;
						goto case Group_Return;
					}

					case Group_Return:
					{
						if (i + 1 < len)
						{
							inststates[i + 1] = 1;
						}
						break;
					}

					case Group_Switch:
					{
						SwitchInstruction swinstr = (SwitchInstruction)instr;
						int[] dests = swinstr.GetDestinations();
						for (int j = dests.Length - 1; j >= 0; j--)
						{
							inststates[dests[j]] = 1;
						}
						inststates[swinstr.GetDefaultDestination()] = 1;
						if (i + 1 < len)
						{
							inststates[i + 1] = 1;
						}
						break;
					}
				}
			}
			// first instruction
			inststates[0] = 1;
			return inststates;
		}

		private VBStyleCollection<BasicBlock, int> CreateBasicBlocks(short[] startblock, 
			InstructionSequence instrseq, Dictionary<int, BasicBlock> mapInstrBlocks)
		{
			VBStyleCollection<BasicBlock, int> col = new VBStyleCollection<BasicBlock, int>();
			InstructionSequence currseq = null;
			List<int> lstOffs = null;
			int len = startblock.Length;
			short counter = 0;
			int blockoffset = 0;
			BasicBlock currentBlock = null;
			for (int i = 0; i < len; i++)
			{
				if (startblock[i] == 1)
				{
					currentBlock = new BasicBlock(++counter);
					currseq = currentBlock.GetSeq();
					lstOffs = currentBlock.GetInstrOldOffsets();
					col.AddWithKey(currentBlock, currentBlock.id);
					blockoffset = instrseq.GetOffset(i);
				}
				startblock[i] = counter;
				Sharpen.Collections.Put(mapInstrBlocks, i, currentBlock);
				currseq.AddInstruction(instrseq.GetInstr(i), instrseq.GetOffset(i) - blockoffset);
				lstOffs.Add(instrseq.GetOffset(i));
			}
			last_id = counter;
			return col;
		}

		private static void ConnectBlocks(List<BasicBlock> lstbb, Dictionary<int, BasicBlock
			> mapInstrBlocks)
		{
			for (int i = 0; i < lstbb.Count; i++)
			{
				BasicBlock block = lstbb[i];
				Instruction instr = block.GetLastInstruction();
				bool fallthrough = instr.CanFallThrough();
				BasicBlock bTemp;
				switch (instr.group)
				{
					case Group_Jump:
					{
						int dest = ((JumpInstruction)instr).destination;
						bTemp = mapInstrBlocks.GetOrNull(dest);
						block.AddSuccessor(bTemp);
						break;
					}

					case Group_Switch:
					{
						SwitchInstruction sinstr = (SwitchInstruction)instr;
						int[] dests = sinstr.GetDestinations();
						bTemp = mapInstrBlocks.GetOrNull(((SwitchInstruction)instr).GetDefaultDestination
							());
						block.AddSuccessor(bTemp);
						foreach (int dest1 in dests)
						{
							bTemp = mapInstrBlocks.GetOrNull(dest1);
							block.AddSuccessor(bTemp);
						}
						break;
					}
				}
				if (fallthrough && i < lstbb.Count - 1)
				{
					BasicBlock defaultBlock = lstbb[i + 1];
					block.AddSuccessor(defaultBlock);
				}
			}
		}

		private void SetExceptionEdges(InstructionSequence instrseq, Dictionary<int, BasicBlock
			> instrBlocks)
		{
			exceptions = new List<ExceptionRangeCFG>();
			Dictionary<string, ExceptionRangeCFG> mapRanges = new Dictionary<string, ExceptionRangeCFG
				>();
			foreach (ExceptionHandler handler in instrseq.GetExceptionTable().GetHandlers())
			{
				BasicBlock from = instrBlocks.GetOrNull(handler.from_instr);
				BasicBlock to = instrBlocks.GetOrNull(handler.to_instr);
				BasicBlock handle = instrBlocks.GetOrNull(handler.handler_instr);
				string key = from.id + ":" + to.id + ":" + handle.id;
				if (mapRanges.ContainsKey(key))
				{
					ExceptionRangeCFG range = mapRanges.GetOrNull(key);
					range.AddExceptionType(handler.exceptionClass);
				}
				else
				{
					List<BasicBlock> protectedRange = new List<BasicBlock>();
					for (int j = from.id; j < to.id; j++)
					{
						BasicBlock block = blocks.GetWithKey(j);
						protectedRange.Add(block);
						block.AddSuccessorException(handle);
					}
					ExceptionRangeCFG range = new ExceptionRangeCFG(protectedRange, handle, handler.exceptionClass
						 == null ? null : System.Linq.Enumerable.ToList(new [] {handler.exceptionClass})
						);
					Sharpen.Collections.Put(mapRanges, key, range);
					exceptions.Add(range);
				}
			}
		}

		private void SetSubroutineEdges()
		{
			var subroutines = new Dictionary<BasicBlock, BasicBlock>();
			foreach (BasicBlock block in blocks)
			{
				if (block.GetSeq().GetLastInstr().opcode == ICodeConstants.opc_jsr)
				{
					LinkedList<BasicBlock> stack = new LinkedList<BasicBlock>();
					LinkedList<LinkedList<BasicBlock>> stackJsrStacks = new LinkedList<LinkedList<BasicBlock
						>>();
					HashSet<BasicBlock> setVisited = new HashSet<BasicBlock>();
					stack.AddLast(block);
					stackJsrStacks.AddLast(new LinkedList<BasicBlock>());
					while (!(stack.Count == 0))
					{
						BasicBlock node = Sharpen.Collections.RemoveFirst(stack);
						LinkedList<BasicBlock> jsrstack = Sharpen.Collections.RemoveFirst(stackJsrStacks);
						setVisited.Add(node);
						switch (node.GetSeq().GetLastInstr().opcode)
						{
							case ICodeConstants.opc_jsr:
							{
								jsrstack.AddLast(node);
								break;
							}

							case ICodeConstants.opc_ret:
							{
								BasicBlock enter = jsrstack.Last.Value;
								BasicBlock exit = blocks.GetWithKey(enter.id + 1);
								// FIXME: find successor in a better way
								if (exit != null)
								{
									if (!node.IsSuccessor(exit))
									{
										node.AddSuccessor(exit);
									}
									Sharpen.Collections.RemoveLast(jsrstack);
									Sharpen.Collections.Put(subroutines, enter, exit);
								}
								else
								{
									throw new Exception("ERROR: last instruction jsr");
								}
								break;
							}
						}
						if (!(jsrstack.Count == 0))
						{
							foreach (BasicBlock succ in node.GetSuccs())
							{
								if (!setVisited.Contains(succ))
								{
									stack.AddLast(succ);
									stackJsrStacks.AddLast(new LinkedList<BasicBlock>(jsrstack));
								}
							}
						}
					}
				}
			}
			this.subroutines = subroutines;
		}

		private void ProcessJsr()
		{
			while (true)
			{
				if (ProcessJsrRanges() == 0)
				{
					break;
				}
			}
		}

		public class JsrRecord
		{
			public BasicBlock jsr { get; }

			public HashSet<BasicBlock> range { get; }

			public BasicBlock ret { get; }

			public JsrRecord(BasicBlock jsr, HashSet<BasicBlock> range, BasicBlock ret)
			{
				this.jsr = jsr;
				this.range = range;
				this.ret = ret;
			}
		}

		private int ProcessJsrRanges()
		{
			List<ControlFlowGraph.JsrRecord> lstJsrAll = new List<ControlFlowGraph.JsrRecord
				>();
			// get all jsr ranges
			foreach (KeyValuePair<BasicBlock, BasicBlock> ent in subroutines)
			{
				BasicBlock jsr = ent.Key;
				BasicBlock ret = ent.Value;
				lstJsrAll.Add(new ControlFlowGraph.JsrRecord(jsr, GetJsrRange(jsr, ret), ret));
			}
			// sort ranges
			// FIXME: better sort order
			List<ControlFlowGraph.JsrRecord> lstJsr = new List<ControlFlowGraph.JsrRecord>();
			foreach (ControlFlowGraph.JsrRecord arr in lstJsrAll)
			{
				int i = 0;
				for (; i < lstJsr.Count; i++)
				{
					ControlFlowGraph.JsrRecord arrJsr = lstJsr[i];
					if (arrJsr.range.Contains(arr.jsr))
					{
						break;
					}
				}
				lstJsr.Add(i, arr);
			}
			// find the first intersection
			for (int i = 0; i < lstJsr.Count; i++)
			{
				ControlFlowGraph.JsrRecord arr = lstJsr[i];
				HashSet<BasicBlock> set = arr.range;
				for (int j = i + 1; j < lstJsr.Count; j++)
				{
					ControlFlowGraph.JsrRecord arr1 = lstJsr[j];
					HashSet<BasicBlock> set1 = arr1.range;
					if (!set.Contains(arr1.jsr) && !set1.Contains(arr.jsr))
					{
						// rang 0 doesn't contain entry 1 and vice versa
						HashSet<BasicBlock> setc = new HashSet<BasicBlock>(set);
						setc.IntersectWith(set1);
						if (!(setc.Count == 0))
						{
							SplitJsrRange(arr.jsr, arr.ret, setc);
							return 1;
						}
					}
				}
			}
			return 0;
		}

		private HashSet<BasicBlock> GetJsrRange(BasicBlock jsr, BasicBlock ret)
		{
			HashSet<BasicBlock> blocks = new HashSet<BasicBlock>();
			List<BasicBlock> lstNodes = new List<BasicBlock>();
			lstNodes.Add(jsr);
			BasicBlock dom = jsr.GetSuccs()[0];
			while (!(lstNodes.Count == 0))
			{
				BasicBlock node = lstNodes.RemoveAtReturningValue(0);
				for (int j = 0; j < 2; j++)
				{
					List<BasicBlock> lst;
					if (j == 0)
					{
						if (node.GetLastInstruction().opcode == ICodeConstants.opc_ret)
						{
							if (node.GetSuccs().Contains(ret))
							{
								continue;
							}
						}
						lst = node.GetSuccs();
					}
					else
					{
						if (node == jsr)
						{
							continue;
						}
						lst = node.GetSuccExceptions();
					}
					for (int i = lst.Count - 1; i >= 0; i--)
					{
						BasicBlock child = lst[i];
						if (!blocks.Contains(child))
						{
							if (node != jsr)
							{
								for (int k = 0; k < child.GetPreds().Count; k++)
								{
									if (!DeadCodeHelper.IsDominator(this, child.GetPreds()[k], dom))
									{
										goto CHILD_continue;
									}
								}
								for (int k = 0; k < child.GetPredExceptions().Count; k++)
								{
									if (!DeadCodeHelper.IsDominator(this, child.GetPredExceptions()[k], dom))
									{
										goto CHILD_continue;
									}
								}
							}
							// last block is a dummy one
							if (child != last)
							{
								blocks.Add(child);
							}
							lstNodes.Add(child);
						}
CHILD_continue: ;
					}
CHILD_break: ;
				}
			}
			return blocks;
		}

		private void SplitJsrRange(BasicBlock jsr, BasicBlock ret, HashSet<BasicBlock> common_blocks
			)
		{
			List<BasicBlock> lstNodes = new List<BasicBlock>();
			Dictionary<int, BasicBlock> mapNewNodes = new Dictionary<int, BasicBlock>();
			lstNodes.Add(jsr);
			Sharpen.Collections.Put(mapNewNodes, jsr.id, jsr);
			while (!(lstNodes.Count == 0))
			{
				BasicBlock node = lstNodes.RemoveAtReturningValue(0);
				for (int j = 0; j < 2; j++)
				{
					List<BasicBlock> lst;
					if (j == 0)
					{
						if (node.GetLastInstruction().opcode == ICodeConstants.opc_ret)
						{
							if (node.GetSuccs().Contains(ret))
							{
								continue;
							}
						}
						lst = node.GetSuccs();
					}
					else
					{
						if (node == jsr)
						{
							continue;
						}
						lst = node.GetSuccExceptions();
					}
					for (int i = lst.Count - 1; i >= 0; i--)
					{
						BasicBlock child = lst[i];
						int childid = child.id;
						if (mapNewNodes.ContainsKey(childid))
						{
							node.ReplaceSuccessor(child, mapNewNodes.GetOrNull(childid));
						}
						else if (common_blocks.Contains(child))
						{
							// make a copy of the current block
							BasicBlock copy = child.Clone();
							copy.id = ++last_id;
							// copy all successors
							if (copy.GetLastInstruction().opcode == ICodeConstants.opc_ret && child.GetSuccs(
								).Contains(ret))
							{
								copy.AddSuccessor(ret);
								child.RemoveSuccessor(ret);
							}
							else
							{
								for (int k = 0; k < child.GetSuccs().Count; k++)
								{
									copy.AddSuccessor(child.GetSuccs()[k]);
								}
							}
							for (int k = 0; k < child.GetSuccExceptions().Count; k++)
							{
								copy.AddSuccessorException(child.GetSuccExceptions()[k]);
							}
							lstNodes.Add(copy);
							Sharpen.Collections.Put(mapNewNodes, childid, copy);
							if (last.GetPreds().Contains(child))
							{
								last.AddPredecessor(copy);
							}
							node.ReplaceSuccessor(child, copy);
							blocks.AddWithKey(copy, copy.id);
						}
						else
						{
							// stop at the first fixed node
							//lstNodes.add(child);
							Sharpen.Collections.Put(mapNewNodes, childid, child);
						}
					}
				}
			}
			// note: subroutines won't be copied!
			SplitJsrExceptionRanges(common_blocks, mapNewNodes);
		}

		private void SplitJsrExceptionRanges(HashSet<BasicBlock> common_blocks, IDictionary
			<int, BasicBlock> mapNewNodes)
		{
			for (int i = exceptions.Count - 1; i >= 0; i--)
			{
				ExceptionRangeCFG range = exceptions[i];
				List<BasicBlock> lstRange = range.GetProtectedRange();
				HashSet<BasicBlock> setBoth = new HashSet<BasicBlock>(common_blocks);
				setBoth.IntersectWith(lstRange);
				if (setBoth.Count > 0)
				{
					List<BasicBlock> lstNewRange;
					if (setBoth.Count == lstRange.Count)
					{
						lstNewRange = new List<BasicBlock>();
						ExceptionRangeCFG newRange = new ExceptionRangeCFG(lstNewRange, mapNewNodes.GetOrNull
							(range.GetHandler().id), range.GetExceptionTypes());
						exceptions.Add(newRange);
					}
					else
					{
						lstNewRange = lstRange;
					}
					foreach (BasicBlock block in setBoth)
					{
						lstNewRange.Add(mapNewNodes.GetOrNull(block.id));
					}
				}
			}
		}

		private void RemoveJsr(StructMethod mt)
		{
			RemoveJsrInstructions(mt.GetClassStruct().GetPool(), first, DataPoint.GetInitialDataPoint
				(mt));
		}

		private static void RemoveJsrInstructions(ConstantPool pool, BasicBlock block, DataPoint
			 data)
		{
			ListStack<VarType> stack = data.GetStack();
			InstructionSequence seq = block.GetSeq();
			for (int i = 0; i < seq.Length(); i++)
			{
				Instruction instr = seq.GetInstr(i);
				VarType var = null;
				if (instr.opcode == ICodeConstants.opc_astore || instr.opcode == ICodeConstants.opc_pop)
				{
					var = stack.GetByOffset(-1);
				}
				InstructionImpact.StepTypes(data, instr, pool);
				switch (instr.opcode)
				{
					case ICodeConstants.opc_jsr:
					case ICodeConstants.opc_ret:
					{
						seq.RemoveInstruction(i);
						i--;
						break;
					}

					case ICodeConstants.opc_astore:
					case ICodeConstants.opc_pop:
					{
						if (var.type == ICodeConstants.Type_Address)
						{
							seq.RemoveInstruction(i);
							i--;
						}
						break;
					}
				}
			}
			block.mark = 1;
			for (int i = 0; i < block.GetSuccs().Count; i++)
			{
				BasicBlock suc = block.GetSuccs()[i];
				if (suc.mark != 1)
				{
					RemoveJsrInstructions(pool, suc, data.Copy());
				}
			}
			for (int i = 0; i < block.GetSuccExceptions().Count; i++)
			{
				BasicBlock suc = block.GetSuccExceptions()[i];
				if (suc.mark != 1)
				{
					DataPoint point = new DataPoint();
					point.SetLocalVariables(new List<VarType>(data.GetLocalVariables()));
					point.GetStack().Push(new VarType(ICodeConstants.Type_Object, 0, null));
					RemoveJsrInstructions(pool, suc, point);
				}
			}
		}

		private void SetFirstAndLastBlocks()
		{
			first = blocks[0];
			last = new BasicBlock(++last_id);
			foreach (BasicBlock block in blocks)
			{
				if ((block.GetSuccs().Count == 0))
				{
					last.AddPredecessor(block);
				}
			}
		}

		public virtual LinkedList<IGraphNode> GetReversePostOrder()
		{
			LinkedList<IGraphNode> res = new LinkedList<IGraphNode>();
			AddToReversePostOrderListIterative(first, res);
			return res;
		}

		private static void AddToReversePostOrderListIterative(BasicBlock root, LinkedList<IGraphNode> lst)
		{
			LinkedList<BasicBlock> stackNode = new LinkedList<BasicBlock>();
			LinkedList<int> stackIndex = new LinkedList<int>();
			HashSet<BasicBlock> setVisited = new HashSet<BasicBlock>();
			stackNode.AddLast(root);
			stackIndex.AddLast(0);
			while (!(stackNode.Count == 0))
			{
				BasicBlock node = stackNode.Last.Value;
				int index = Sharpen.Collections.RemoveLast(stackIndex);
				setVisited.Add(node);
				List<BasicBlock> lstSuccs = new List<BasicBlock>(node.GetSuccs());
				Sharpen.Collections.AddAll(lstSuccs, node.GetSuccExceptions());
				for (; index < lstSuccs.Count; index++)
				{
					BasicBlock succ = lstSuccs[index];
					if (!setVisited.Contains(succ))
					{
						stackIndex.AddLast(index + 1);
						stackNode.AddLast(succ);
						stackIndex.AddLast(0);
						break;
					}
				}
				if (index == lstSuccs.Count)
				{
					lst.AddFirst(node);
					Sharpen.Collections.RemoveLast(stackNode);
				}
			}
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual VBStyleCollection<BasicBlock, int> GetBlocks()
		{
			return blocks;
		}

		public virtual BasicBlock GetFirst()
		{
			return first;
		}

		public virtual void SetFirst(BasicBlock first)
		{
			this.first = first;
		}

		public virtual List<ExceptionRangeCFG> GetExceptions()
		{
			return exceptions;
		}

		public virtual BasicBlock GetLast()
		{
			return last;
		}

		public virtual HashSet<BasicBlock> GetFinallyExits()
		{
			return finallyExits;
		}
	}
}
