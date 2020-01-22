// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Code.Cfg;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Code
{
	public class DeadCodeHelper
	{
		public static void RemoveDeadBlocks(ControlFlowGraph graph)
		{
			LinkedList<BasicBlock> stack = new LinkedList<BasicBlock>();
			HashSet<BasicBlock> setStacked = new HashSet<BasicBlock>();
			stack.AddLast(graph.GetFirst());
			setStacked.Add(graph.GetFirst());
			while (!(stack.Count == 0))
			{
				BasicBlock block = Sharpen.Collections.RemoveFirst(stack);
				List<BasicBlock> lstSuccs = new List<BasicBlock>(block.GetSuccs());
				Sharpen.Collections.AddAll(lstSuccs, block.GetSuccExceptions());
				foreach (BasicBlock succ in lstSuccs)
				{
					if (!setStacked.Contains(succ))
					{
						stack.AddLast(succ);
						setStacked.Add(succ);
					}
				}
			}
			HashSet<BasicBlock> setAllBlocks = new HashSet<BasicBlock>(graph.GetBlocks());
			setAllBlocks.ExceptWith(setStacked);
			foreach (BasicBlock block in setAllBlocks)
			{
				graph.RemoveBlock(block);
			}
		}

		public static void RemoveEmptyBlocks(ControlFlowGraph graph)
		{
			List<BasicBlock> blocks = graph.GetBlocks();
			bool cont;
			do
			{
				cont = false;
				for (int i = blocks.Count - 1; i >= 0; i--)
				{
					BasicBlock block = blocks[i];
					if (RemoveEmptyBlock(graph, block, false))
					{
						cont = true;
						break;
					}
				}
			}
			while (cont);
		}

		private static bool RemoveEmptyBlock(ControlFlowGraph graph, BasicBlock block, bool
			 merging)
		{
			bool deletedRanges = false;
			if (block.GetSeq().IsEmpty())
			{
				if (block.GetSuccs().Count > 1)
				{
					if (block.GetPreds().Count > 1)
					{
						// ambiguous block
						throw new Exception("ERROR: empty block with multiple predecessors and successors found"
							);
					}
					else if (!merging)
					{
						throw new Exception("ERROR: empty block with multiple successors found");
					}
				}
				HashSet<BasicBlock> setExits = new HashSet<BasicBlock>(graph.GetLast().GetPreds()
					);
				if ((block.GetPredExceptions().Count == 0) && (!setExits.Contains(block) || block
					.GetPreds().Count == 1))
				{
					if (setExits.Contains(block))
					{
						BasicBlock pred = block.GetPreds()[0];
						// FIXME: flag in the basic block
						if (pred.GetSuccs().Count != 1 || (!pred.GetSeq().IsEmpty() && pred.GetSeq().GetLastInstr
							().group == ICodeConstants.Group_Switch))
						{
							return false;
						}
					}
					HashSet<BasicBlock> setPreds = new HashSet<BasicBlock>(block.GetPreds());
					HashSet<BasicBlock> setSuccs = new HashSet<BasicBlock>(block.GetSuccs());
					// collect common exception ranges of predecessors and successors
					HashSet<BasicBlock> setCommonExceptionHandlers = null;
					for (int i = 0; i < 2; ++i)
					{
						foreach (BasicBlock pred in i == 0 ? setPreds : setSuccs)
						{
							if (setCommonExceptionHandlers == null)
							{
								setCommonExceptionHandlers = new HashSet<BasicBlock>(pred.GetSuccExceptions());
							}
							else
							{
								setCommonExceptionHandlers.IntersectWith(pred.GetSuccExceptions());
							}
						}
					}
					// check the block to be in each of the common ranges
					if (setCommonExceptionHandlers != null && !(setCommonExceptionHandlers.Count == 0))
					{
						foreach (BasicBlock handler in setCommonExceptionHandlers)
						{
							if (!block.GetSuccExceptions().Contains(handler))
							{
								return false;
							}
						}
					}
					// remove ranges consisting of this one block
					List<ExceptionRangeCFG> lstRanges = graph.GetExceptions();
					for (int i = lstRanges.Count - 1; i >= 0; i--)
					{
						ExceptionRangeCFG range = lstRanges[i];
						List<BasicBlock> lst = range.GetProtectedRange();
						if (lst.Count == 1 && lst[0] == block)
						{
							if (DecompilerContext.GetOption(IFernflowerPreferences.Remove_Empty_Ranges))
							{
								block.RemoveSuccessorException(range.GetHandler());
								lstRanges.RemoveAtReturningValue(i);
								deletedRanges = true;
							}
							else
							{
								return false;
							}
						}
					}
					// connect remaining nodes
					if (merging)
					{
						BasicBlock pred = block.GetPreds()[0];
						pred.RemoveSuccessor(block);
						List<BasicBlock> lstSuccs = new List<BasicBlock>(block.GetSuccs());
						foreach (BasicBlock succ in lstSuccs)
						{
							block.RemoveSuccessor(succ);
							pred.AddSuccessor(succ);
						}
					}
					else
					{
						foreach (BasicBlock pred in setPreds)
						{
							foreach (BasicBlock succ in setSuccs)
							{
								pred.ReplaceSuccessor(block, succ);
							}
						}
					}
					// finally exit edges
					HashSet<BasicBlock> setFinallyExits = graph.GetFinallyExits();
					if (setFinallyExits.Contains(block))
					{
						setFinallyExits.Remove(block);
						setFinallyExits.Add(setPreds.GetEnumerator().Current);
					}
					// replace first if necessary
					if (graph.GetFirst() == block)
					{
						if (setSuccs.Count != 1)
						{
							throw new Exception("multiple or no entry blocks!");
						}
						else
						{
							graph.SetFirst(setSuccs.GetEnumerator().Current);
						}
					}
					// remove this block
					graph.RemoveBlock(block);
					if (deletedRanges)
					{
						RemoveDeadBlocks(graph);
					}
				}
			}
			return deletedRanges;
		}

		public static bool IsDominator(ControlFlowGraph graph, BasicBlock block, BasicBlock
			 dom)
		{
			HashSet<BasicBlock> marked = new HashSet<BasicBlock>();
			if (block == dom)
			{
				return true;
			}
			LinkedList<BasicBlock> lstNodes = new LinkedList<BasicBlock>();
			lstNodes.AddLast(block);
			while (!(lstNodes.Count == 0))
			{
				BasicBlock node = lstNodes.First.Value;
				lstNodes.RemoveFirst();
				if (marked.Contains(node))
				{
					continue;
				}
				else
				{
					marked.Add(node);
				}
				if (node == graph.GetFirst())
				{
					return false;
				}
				for (int i = 0; i < node.GetPreds().Count; i++)
				{
					BasicBlock pred = node.GetPreds()[i];
					if (pred != dom && !marked.Contains(pred))
					{
						lstNodes.AddLast(pred);
					}
				}
				for (int i = 0; i < node.GetPredExceptions().Count; i++)
				{
					BasicBlock pred = node.GetPredExceptions()[i];
					if (pred != dom && !marked.Contains(pred))
					{
						lstNodes.AddLast(pred);
					}
				}
			}
			return true;
		}

		public static void RemoveGotos(ControlFlowGraph graph)
		{
			foreach (BasicBlock block in graph.GetBlocks())
			{
				Instruction instr = block.GetLastInstruction();
				if (instr != null && instr.opcode == ICodeConstants.opc_goto)
				{
					block.GetSeq().RemoveLast();
				}
			}
			RemoveEmptyBlocks(graph);
		}

		public static void ConnectDummyExitBlock(ControlFlowGraph graph)
		{
			BasicBlock exit = graph.GetLast();
			foreach (BasicBlock block in new HashSet<BasicBlock>(exit.GetPreds()))
			{
				exit.RemovePredecessor(block);
				block.AddSuccessor(exit);
			}
		}

		public static void ExtendSynchronizedRangeToMonitorexit(ControlFlowGraph graph)
		{
			while (true)
			{
				bool range_extended = false;
				foreach (ExceptionRangeCFG range in graph.GetExceptions())
				{
					HashSet<BasicBlock> setPreds = new HashSet<BasicBlock>();
					foreach (BasicBlock block in range.GetProtectedRange())
					{
						Sharpen.Collections.AddAll(setPreds, block.GetPreds());
					}
					setPreds.ExceptWith(range.GetProtectedRange());
					if (setPreds.Count != 1)
					{
						continue;
					}
					// multiple predecessors, obfuscated range
					BasicBlock predBlock = setPreds.GetEnumerator().Current;
					InstructionSequence predSeq = predBlock.GetSeq();
					if (predSeq.IsEmpty() || predSeq.GetLastInstr().opcode != ICodeConstants.opc_monitorenter)
					{
						continue;
					}
					// not a synchronized range
					bool monitorexit_in_range = false;
					HashSet<BasicBlock> setProtectedBlocks = new HashSet<BasicBlock>();
					Sharpen.Collections.AddAll(setProtectedBlocks, range.GetProtectedRange());
					setProtectedBlocks.Add(range.GetHandler());
					foreach (BasicBlock block in setProtectedBlocks)
					{
						InstructionSequence blockSeq = block.GetSeq();
						for (int i = 0; i < blockSeq.Length(); i++)
						{
							if (blockSeq.GetInstr(i).opcode == ICodeConstants.opc_monitorexit)
							{
								monitorexit_in_range = true;
								break;
							}
						}
						if (monitorexit_in_range)
						{
							break;
						}
					}
					if (monitorexit_in_range)
					{
						continue;
					}
					// protected range already contains monitorexit
					HashSet<BasicBlock> setSuccs = new HashSet<BasicBlock>();
					foreach (BasicBlock block in range.GetProtectedRange())
					{
						Sharpen.Collections.AddAll(setSuccs, block.GetSuccs());
					}
					setSuccs.ExceptWith(range.GetProtectedRange());
					if (setSuccs.Count != 1)
					{
						continue;
					}
					// non-unique successor
					BasicBlock succBlock = setSuccs.GetEnumerator().Current;
					InstructionSequence succSeq = succBlock.GetSeq();
					int succ_monitorexit_index = -1;
					for (int i = 0; i < succSeq.Length(); i++)
					{
						if (succSeq.GetInstr(i).opcode == ICodeConstants.opc_monitorexit)
						{
							succ_monitorexit_index = i;
							break;
						}
					}
					if (succ_monitorexit_index < 0)
					{
						continue;
					}
					// monitorexit not found in the single successor block
					BasicBlock handlerBlock = range.GetHandler();
					if (handlerBlock.GetSuccs().Count != 1)
					{
						continue;
					}
					// non-unique handler successor
					BasicBlock succHandler = handlerBlock.GetSuccs()[0];
					InstructionSequence succHandlerSeq = succHandler.GetSeq();
					if (succHandlerSeq.IsEmpty() || succHandlerSeq.GetLastInstr().opcode != ICodeConstants
						.opc_athrow)
					{
						continue;
					}
					// not a standard synchronized range
					int handler_monitorexit_index = -1;
					for (int i = 0; i < succHandlerSeq.Length(); i++)
					{
						if (succHandlerSeq.GetInstr(i).opcode == ICodeConstants.opc_monitorexit)
						{
							handler_monitorexit_index = i;
							break;
						}
					}
					if (handler_monitorexit_index < 0)
					{
						continue;
					}
					// monitorexit not found in the handler successor block
					// checks successful, prerequisites satisfied, now extend the range
					if (succ_monitorexit_index < succSeq.Length() - 1)
					{
						// split block
						SimpleInstructionSequence seq = new SimpleInstructionSequence();
						for (int counter = 0; counter < succ_monitorexit_index; counter++)
						{
							seq.AddInstruction(succSeq.GetInstr(0), -1);
							succSeq.RemoveInstruction(0);
						}
						// build a separate block
						BasicBlock newblock = new BasicBlock(++graph.last_id);
						newblock.SetSeq(seq);
						// insert new block
						foreach (BasicBlock block in succBlock.GetPreds())
						{
							block.ReplaceSuccessor(succBlock, newblock);
						}
						newblock.AddSuccessor(succBlock);
						graph.GetBlocks().AddWithKey(newblock, newblock.id);
						succBlock = newblock;
					}
					// copy exception edges and extend protected ranges (successor block)
					BasicBlock rangeExitBlock = succBlock.GetPreds()[0];
					for (int j = 0; j < rangeExitBlock.GetSuccExceptions().Count; j++)
					{
						BasicBlock hd = rangeExitBlock.GetSuccExceptions()[j];
						succBlock.AddSuccessorException(hd);
						ExceptionRangeCFG rng = graph.GetExceptionRange(hd, rangeExitBlock);
						rng.GetProtectedRange().Add(succBlock);
					}
					// copy instructions (handler successor block)
					InstructionSequence handlerSeq = handlerBlock.GetSeq();
					for (int counter = 0; counter < handler_monitorexit_index; counter++)
					{
						handlerSeq.AddInstruction(succHandlerSeq.GetInstr(0), -1);
						succHandlerSeq.RemoveInstruction(0);
					}
					range_extended = true;
					break;
				}
				if (!range_extended)
				{
					break;
				}
			}
		}

		public static void IncorporateValueReturns(ControlFlowGraph graph)
		{
			foreach (BasicBlock block in graph.GetBlocks())
			{
				InstructionSequence seq = block.GetSeq();
				int len = seq.Length();
				if (len > 0 && len < 3)
				{
					bool ok = false;
					if (seq.GetLastInstr().opcode >= ICodeConstants.opc_ireturn && seq.GetLastInstr()
						.opcode <= ICodeConstants.opc_return)
					{
						if (len == 1)
						{
							ok = true;
						}
						else if (seq.GetLastInstr().opcode != ICodeConstants.opc_return)
						{
							switch (seq.GetInstr(0).opcode)
							{
								case ICodeConstants.opc_iload:
								case ICodeConstants.opc_lload:
								case ICodeConstants.opc_fload:
								case ICodeConstants.opc_dload:
								case ICodeConstants.opc_aload:
								case ICodeConstants.opc_aconst_null:
								case ICodeConstants.opc_bipush:
								case ICodeConstants.opc_sipush:
								case ICodeConstants.opc_lconst_0:
								case ICodeConstants.opc_lconst_1:
								case ICodeConstants.opc_fconst_0:
								case ICodeConstants.opc_fconst_1:
								case ICodeConstants.opc_fconst_2:
								case ICodeConstants.opc_dconst_0:
								case ICodeConstants.opc_dconst_1:
								case ICodeConstants.opc_ldc:
								case ICodeConstants.opc_ldc_w:
								case ICodeConstants.opc_ldc2_w:
								{
									ok = true;
									break;
								}
							}
						}
					}
					if (ok)
					{
						if (!(block.GetPreds().Count == 0))
						{
							HashSet<BasicBlock> setPredHandlersUnion = new HashSet<BasicBlock>();
							HashSet<BasicBlock> setPredHandlersIntersection = new HashSet<BasicBlock>();
							bool firstpred = true;
							foreach (BasicBlock pred in block.GetPreds())
							{
								if (firstpred)
								{
									Sharpen.Collections.AddAll(setPredHandlersIntersection, pred.GetSuccExceptions());
									firstpred = false;
								}
								else
								{
									setPredHandlersIntersection.IntersectWith(pred.GetSuccExceptions());
								}
								Sharpen.Collections.AddAll(setPredHandlersUnion, pred.GetSuccExceptions());
							}
							// add exception ranges from predecessors
							setPredHandlersIntersection.ExceptWith(block.GetSuccExceptions());
							BasicBlock predecessor = block.GetPreds()[0];
							foreach (BasicBlock handler in setPredHandlersIntersection)
							{
								ExceptionRangeCFG range = graph.GetExceptionRange(handler, predecessor);
								range.GetProtectedRange().Add(block);
								block.AddSuccessorException(handler);
							}
							// remove redundant ranges
							HashSet<BasicBlock> setRangesToBeRemoved = new HashSet<BasicBlock>(block.GetSuccExceptions
								());
							setRangesToBeRemoved.ExceptWith(setPredHandlersUnion);
							foreach (BasicBlock handler in setRangesToBeRemoved)
							{
								ExceptionRangeCFG range = graph.GetExceptionRange(handler, block);
								if (range.GetProtectedRange().Count > 1)
								{
									range.GetProtectedRange().Remove(block);
									block.RemoveSuccessorException(handler);
								}
							}
						}
						if (block.GetPreds().Count == 1 && (block.GetPredExceptions().Count == 0))
						{
							BasicBlock bpred = block.GetPreds()[0];
							if (bpred.GetSuccs().Count == 1)
							{
								// add exception ranges of predecessor
								foreach (BasicBlock succ in bpred.GetSuccExceptions())
								{
									if (!block.GetSuccExceptions().Contains(succ))
									{
										ExceptionRangeCFG range = graph.GetExceptionRange(succ, bpred);
										range.GetProtectedRange().Add(block);
										block.AddSuccessorException(succ);
									}
								}
								// remove superfluous ranges from successors
								foreach (BasicBlock succ in new HashSet<BasicBlock>(block.GetSuccExceptions()))
								{
									if (!bpred.GetSuccExceptions().Contains(succ))
									{
										ExceptionRangeCFG range = graph.GetExceptionRange(succ, block);
										if (range.GetProtectedRange().Count > 1)
										{
											range.GetProtectedRange().Remove(block);
											block.RemoveSuccessorException(succ);
										}
									}
								}
							}
						}
					}
				}
			}
		}

		public static void MergeBasicBlocks(ControlFlowGraph graph)
		{
			while (true)
			{
				bool merged = false;
				foreach (BasicBlock block in graph.GetBlocks())
				{
					InstructionSequence seq = block.GetSeq();
					if (block.GetSuccs().Count == 1)
					{
						BasicBlock next = block.GetSuccs()[0];
						if (next != graph.GetLast() && (seq.IsEmpty() || seq.GetLastInstr().group != ICodeConstants
							.Group_Switch))
						{
							if (next.GetPreds().Count == 1 && (next.GetPredExceptions().Count == 0) && next !=
								 graph.GetFirst())
							{
								// TODO: implement a dummy start block
								bool sameRanges = true;
								foreach (ExceptionRangeCFG range in graph.GetExceptions())
								{
									if (range.GetProtectedRange().Contains(block) ^ range.GetProtectedRange().Contains
										(next))
									{
										sameRanges = false;
										break;
									}
								}
								if (sameRanges)
								{
									seq.AddSequence(next.GetSeq());
									Sharpen.Collections.AddAll(block.GetInstrOldOffsets(), next.GetInstrOldOffsets());
									next.GetSeq().Clear();
									RemoveEmptyBlock(graph, next, true);
									merged = true;
									break;
								}
							}
						}
					}
				}
				if (!merged)
				{
					break;
				}
			}
		}
	}
}
