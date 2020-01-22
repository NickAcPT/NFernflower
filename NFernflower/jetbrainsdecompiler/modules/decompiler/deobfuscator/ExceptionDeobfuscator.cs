// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Code.Cfg;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Modules.Decompiler.Decompose;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Deobfuscator
{
	public class ExceptionDeobfuscator
	{
		private class Range
		{
			private readonly BasicBlock handler;

			private readonly string uniqueStr;

			private readonly HashSet<BasicBlock> protectedRange;

			private readonly ExceptionRangeCFG rangeCFG;

			private Range(BasicBlock handler, string uniqueStr, HashSet<BasicBlock> protectedRange
				, ExceptionRangeCFG rangeCFG)
			{
				this.handler = handler;
				this.uniqueStr = uniqueStr;
				this.protectedRange = protectedRange;
				this.rangeCFG = rangeCFG;
			}
		}

		public static void RestorePopRanges(ControlFlowGraph graph)
		{
			List<ExceptionDeobfuscator.Range> lstRanges = new List<ExceptionDeobfuscator.Range
				>();
			// aggregate ranges
			foreach (ExceptionRangeCFG range in graph.GetExceptions())
			{
				bool found = false;
				foreach (ExceptionDeobfuscator.Range arr in lstRanges)
				{
					if (arr.handler == range.GetHandler() && InterpreterUtil.EqualObjects(range.GetUniqueExceptionsString
						(), arr.uniqueStr))
					{
						Sharpen.Collections.AddAll(arr.protectedRange, range.GetProtectedRange());
						found = true;
						break;
					}
				}
				if (!found)
				{
					// doesn't matter, which range chosen
					lstRanges.Add(new ExceptionDeobfuscator.Range(range.GetHandler(), range.GetUniqueExceptionsString
						(), new HashSet<BasicBlock>(range.GetProtectedRange()), range));
				}
			}
			// process aggregated ranges
			foreach (ExceptionDeobfuscator.Range range in lstRanges)
			{
				if (range.uniqueStr != null)
				{
					BasicBlock handler = range.handler;
					InstructionSequence seq = handler.GetSeq();
					Instruction firstinstr;
					if (seq.Length() > 0)
					{
						firstinstr = seq.GetInstr(0);
						if (firstinstr.opcode == ICodeConstants.opc_pop || firstinstr.opcode == ICodeConstants
							.opc_astore)
						{
							HashSet<BasicBlock> setrange = new HashSet<BasicBlock>(range.protectedRange);
							foreach (ExceptionDeobfuscator.Range range_super in lstRanges)
							{
								// finally or strict superset
								if (range != range_super)
								{
									HashSet<BasicBlock> setrange_super = new HashSet<BasicBlock>(range_super.protectedRange
										);
									if (!setrange.Contains(range_super.handler) && !setrange_super.Contains(handler) 
										&& (range_super.uniqueStr == null || setrange_super.ContainsAll(setrange)))
									{
										if (range_super.uniqueStr == null)
										{
											setrange_super.RetainAll(setrange);
										}
										else
										{
											setrange_super.RemoveAll(setrange);
										}
										if (!(setrange_super.Count == 0))
										{
											BasicBlock newblock = handler;
											// split the handler
											if (seq.Length() > 1)
											{
												newblock = new BasicBlock(++graph.last_id);
												InstructionSequence newseq = new SimpleInstructionSequence();
												newseq.AddInstruction(firstinstr.Clone(), -1);
												newblock.SetSeq(newseq);
												graph.GetBlocks().AddWithKey(newblock, newblock.id);
												List<BasicBlock> lstTemp = new List<BasicBlock>();
												Sharpen.Collections.AddAll(lstTemp, handler.GetPreds());
												Sharpen.Collections.AddAll(lstTemp, handler.GetPredExceptions());
												// replace predecessors
												foreach (BasicBlock pred in lstTemp)
												{
													pred.ReplaceSuccessor(handler, newblock);
												}
												// replace handler
												foreach (ExceptionRangeCFG range_ext in graph.GetExceptions())
												{
													if (range_ext.GetHandler() == handler)
													{
														range_ext.SetHandler(newblock);
													}
													else if (range_ext.GetProtectedRange().Contains(handler))
													{
														newblock.AddSuccessorException(range_ext.GetHandler());
														range_ext.GetProtectedRange().Add(newblock);
													}
												}
												newblock.AddSuccessor(handler);
												if (graph.GetFirst() == handler)
												{
													graph.SetFirst(newblock);
												}
												// remove the first pop in the handler
												seq.RemoveInstruction(0);
											}
											newblock.AddSuccessorException(range_super.handler);
											range_super.rangeCFG.GetProtectedRange().Add(newblock);
											handler = range.rangeCFG.GetHandler();
											seq = handler.GetSeq();
										}
									}
								}
							}
						}
					}
				}
			}
		}

		public static void InsertEmptyExceptionHandlerBlocks(ControlFlowGraph graph)
		{
			HashSet<BasicBlock> setVisited = new HashSet<BasicBlock>();
			foreach (ExceptionRangeCFG range in graph.GetExceptions())
			{
				BasicBlock handler = range.GetHandler();
				if (setVisited.Contains(handler))
				{
					continue;
				}
				setVisited.Add(handler);
				BasicBlock emptyblock = new BasicBlock(++graph.last_id);
				graph.GetBlocks().AddWithKey(emptyblock, emptyblock.id);
				// only exception predecessors considered
				List<BasicBlock> lstTemp = new List<BasicBlock>(handler.GetPredExceptions());
				// replace predecessors
				foreach (BasicBlock pred in lstTemp)
				{
					pred.ReplaceSuccessor(handler, emptyblock);
				}
				// replace handler
				foreach (ExceptionRangeCFG range_ext in graph.GetExceptions())
				{
					if (range_ext.GetHandler() == handler)
					{
						range_ext.SetHandler(emptyblock);
					}
					else if (range_ext.GetProtectedRange().Contains(handler))
					{
						emptyblock.AddSuccessorException(range_ext.GetHandler());
						range_ext.GetProtectedRange().Add(emptyblock);
					}
				}
				emptyblock.AddSuccessor(handler);
				if (graph.GetFirst() == handler)
				{
					graph.SetFirst(emptyblock);
				}
			}
		}

		public static void RemoveEmptyRanges(ControlFlowGraph graph)
		{
			List<ExceptionRangeCFG> lstRanges = graph.GetExceptions();
			for (int i = lstRanges.Count - 1; i >= 0; i--)
			{
				ExceptionRangeCFG range = lstRanges[i];
				bool isEmpty = true;
				foreach (BasicBlock block in range.GetProtectedRange())
				{
					if (!block.GetSeq().IsEmpty())
					{
						isEmpty = false;
						break;
					}
				}
				if (isEmpty)
				{
					foreach (BasicBlock block in range.GetProtectedRange())
					{
						block.RemoveSuccessorException(range.GetHandler());
					}
					lstRanges.RemoveAtReturningValue(i);
				}
			}
		}

		public static void RemoveCircularRanges(ControlFlowGraph graph)
		{
			GenericDominatorEngine engine = new GenericDominatorEngine(new _IIGraph_215(graph
				));
			engine.Initialize();
			List<ExceptionRangeCFG> lstRanges = graph.GetExceptions();
			for (int i = lstRanges.Count - 1; i >= 0; i--)
			{
				ExceptionRangeCFG range = lstRanges[i];
				BasicBlock handler = range.GetHandler();
				List<BasicBlock> rangeList = range.GetProtectedRange();
				if (rangeList.Contains(handler))
				{
					// TODO: better removing strategy
					List<BasicBlock> lstRemBlocks = GetReachableBlocksRestricted(range.GetHandler(), 
						range, engine);
					if (lstRemBlocks.Count < rangeList.Count || rangeList.Count == 1)
					{
						foreach (BasicBlock block in lstRemBlocks)
						{
							block.RemoveSuccessorException(handler);
							rangeList.Remove(block);
						}
					}
					if ((rangeList.Count == 0))
					{
						lstRanges.RemoveAtReturningValue(i);
					}
				}
			}
		}

		private sealed class _IIGraph_215 : IIGraph
		{
			public _IIGraph_215(ControlFlowGraph graph)
			{
				this.graph = graph;
			}

			public List<IIGraphNode> GetReversePostOrderList()
			{
				return graph.GetReversePostOrder();
			}

			public HashSet<IIGraphNode> GetRoots()
			{
				return new HashSet<BasicBlock>(System.Linq.Enumerable.ToList(new [] {graph.GetFirst
					()}));
			}

			private readonly ControlFlowGraph graph;
		}

		private static List<BasicBlock> GetReachableBlocksRestricted(BasicBlock start, ExceptionRangeCFG
			 range, GenericDominatorEngine engine)
		{
			List<BasicBlock> lstRes = new List<BasicBlock>();
			LinkedList<BasicBlock> stack = new LinkedList<BasicBlock>();
			HashSet<BasicBlock> setVisited = new HashSet<BasicBlock>();
			stack.AddFirst(start);
			while (!(stack.Count == 0))
			{
				BasicBlock block = Sharpen.Collections.RemoveFirst(stack);
				setVisited.Add(block);
				if (range.GetProtectedRange().Contains(block) && engine.IsDominator(block, start))
				{
					lstRes.Add(block);
					List<BasicBlock> lstSuccs = new List<BasicBlock>(block.GetSuccs());
					Sharpen.Collections.AddAll(lstSuccs, block.GetSuccExceptions());
					foreach (BasicBlock succ in lstSuccs)
					{
						if (!setVisited.Contains(succ))
						{
							stack.Add(succ);
						}
					}
				}
			}
			return lstRes;
		}

		public static bool HasObfuscatedExceptions(ControlFlowGraph graph)
		{
			IDictionary<BasicBlock, HashSet<BasicBlock>> mapRanges = new Dictionary<BasicBlock
				, HashSet<BasicBlock>>();
			foreach (ExceptionRangeCFG range in graph.GetExceptions())
			{
				Sharpen.Collections.AddAll(mapRanges.ComputeIfAbsent(range.GetHandler(), (BasicBlock
					 k) => new HashSet<BasicBlock>()), range.GetProtectedRange());
			}
			foreach (KeyValuePair<BasicBlock, HashSet<BasicBlock>> ent in mapRanges)
			{
				HashSet<BasicBlock> setEntries = new HashSet<BasicBlock>();
				foreach (BasicBlock block in ent.Value)
				{
					HashSet<BasicBlock> setTemp = new HashSet<BasicBlock>(block.GetPreds());
					setTemp.RemoveAll(ent.Value);
					if (!(setTemp.Count == 0))
					{
						setEntries.Add(block);
					}
				}
				if (!(setEntries.Count == 0))
				{
					if (setEntries.Count > 1)
					{
						/*|| ent.getValue().contains(first)*/
						return true;
					}
				}
			}
			return false;
		}

		public static bool HandleMultipleEntryExceptionRanges(ControlFlowGraph graph)
		{
			GenericDominatorEngine engine = new GenericDominatorEngine(new _IIGraph_314(graph
				));
			engine.Initialize();
			bool found;
			while (true)
			{
				found = false;
				bool splitted = false;
				foreach (ExceptionRangeCFG range in graph.GetExceptions())
				{
					HashSet<BasicBlock> setEntries = GetRangeEntries(range);
					if (setEntries.Count > 1)
					{
						// multiple-entry protected range
						found = true;
						if (SplitExceptionRange(range, setEntries, graph, engine))
						{
							splitted = true;
							break;
						}
					}
				}
				if (!splitted)
				{
					break;
				}
			}
			return !found;
		}

		private sealed class _IIGraph_314 : IIGraph
		{
			public _IIGraph_314(ControlFlowGraph graph)
			{
				this.graph = graph;
			}

			public List<IIGraphNode> GetReversePostOrderList()
			{
				return graph.GetReversePostOrder();
			}

			public HashSet<IIGraphNode> GetRoots()
			{
				return new HashSet<BasicBlock>(System.Linq.Enumerable.ToList(new [] {graph.GetFirst
					()}));
			}

			private readonly ControlFlowGraph graph;
		}

		private static HashSet<BasicBlock> GetRangeEntries(ExceptionRangeCFG range)
		{
			HashSet<BasicBlock> setEntries = new HashSet<BasicBlock>();
			HashSet<BasicBlock> setRange = new HashSet<BasicBlock>(range.GetProtectedRange());
			foreach (BasicBlock block in range.GetProtectedRange())
			{
				HashSet<BasicBlock> setPreds = new HashSet<BasicBlock>(block.GetPreds());
				setPreds.RemoveAll(setRange);
				if (!(setPreds.Count == 0))
				{
					setEntries.Add(block);
				}
			}
			return setEntries;
		}

		private static bool SplitExceptionRange(ExceptionRangeCFG range, HashSet<BasicBlock
			> setEntries, ControlFlowGraph graph, GenericDominatorEngine engine)
		{
			foreach (BasicBlock entry in setEntries)
			{
				List<BasicBlock> lstSubrangeBlocks = GetReachableBlocksRestricted(entry, range, 
					engine);
				if (!(lstSubrangeBlocks.Count == 0) && lstSubrangeBlocks.Count < range.GetProtectedRange
					().Count)
				{
					// add new range
					ExceptionRangeCFG subRange = new ExceptionRangeCFG(lstSubrangeBlocks, range.GetHandler
						(), range.GetExceptionTypes());
					graph.GetExceptions().Add(subRange);
					// shrink the original range
					range.GetProtectedRange().RemoveAll(lstSubrangeBlocks);
					return true;
				}
				else
				{
					// should not happen
					DecompilerContext.GetLogger().WriteMessage("Inconsistency found while splitting protected range"
						, IFernflowerLogger.Severity.Warn);
				}
			}
			return false;
		}

		public static void InsertDummyExceptionHandlerBlocks(ControlFlowGraph graph, int 
			bytecode_version)
		{
			IDictionary<BasicBlock, HashSet<ExceptionRangeCFG>> mapRanges = new Dictionary<BasicBlock
				, HashSet<ExceptionRangeCFG>>();
			foreach (ExceptionRangeCFG range in graph.GetExceptions())
			{
				mapRanges.ComputeIfAbsent(range.GetHandler(), (BasicBlock k) => new HashSet<ExceptionRangeCFG
					>()).Add(range);
			}
			foreach (KeyValuePair<BasicBlock, HashSet<ExceptionRangeCFG>> ent in mapRanges)
			{
				BasicBlock handler = ent.Key;
				HashSet<ExceptionRangeCFG> ranges = ent.Value;
				if (ranges.Count == 1)
				{
					continue;
				}
				foreach (ExceptionRangeCFG range in ranges)
				{
					// add some dummy instructions to prevent optimizing away the empty block  
					SimpleInstructionSequence seq = new SimpleInstructionSequence();
					seq.AddInstruction(Instruction.Create(ICodeConstants.opc_bipush, false, ICodeConstants
						.Group_General, bytecode_version, new int[] { 0 }), -1);
					seq.AddInstruction(Instruction.Create(ICodeConstants.opc_pop, false, ICodeConstants
						.Group_General, bytecode_version, null), -1);
					BasicBlock dummyBlock = new BasicBlock(++graph.last_id);
					dummyBlock.SetSeq(seq);
					graph.GetBlocks().AddWithKey(dummyBlock, dummyBlock.id);
					// only exception predecessors from this range considered
					List<BasicBlock> lstPredExceptions = new List<BasicBlock>(handler.GetPredExceptions
						());
					lstPredExceptions.RetainAll(range.GetProtectedRange());
					// replace predecessors
					foreach (BasicBlock pred in lstPredExceptions)
					{
						pred.ReplaceSuccessor(handler, dummyBlock);
					}
					// replace handler
					range.SetHandler(dummyBlock);
					// add common exception edges
					HashSet<BasicBlock> commonHandlers = new HashSet<BasicBlock>(handler.GetSuccExceptions
						());
					foreach (BasicBlock pred in lstPredExceptions)
					{
						commonHandlers.RetainAll(pred.GetSuccExceptions());
					}
					// TODO: more sanity checks?
					foreach (BasicBlock commonHandler in commonHandlers)
					{
						ExceptionRangeCFG commonRange = graph.GetExceptionRange(commonHandler, handler);
						dummyBlock.AddSuccessorException(commonHandler);
						commonRange.GetProtectedRange().Add(dummyBlock);
					}
					dummyBlock.AddSuccessor(handler);
				}
			}
		}
	}
}
