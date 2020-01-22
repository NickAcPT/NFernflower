// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Code.Cfg;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Modules.Code;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Sforms;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class FinallyProcessor
	{
		private readonly Dictionary<int, int> finallyBlockIDs = new Dictionary<int, int>
			();

		private readonly Dictionary<int, int> catchallBlockIDs = new Dictionary<int, int
			>();

		private readonly MethodDescriptor methodDescriptor;

		private readonly VarProcessor varProcessor;

		public FinallyProcessor(MethodDescriptor md, VarProcessor varProc)
		{
			methodDescriptor = md;
			varProcessor = varProc;
		}

		public virtual bool IterateGraph(StructMethod mt, RootStatement root, ControlFlowGraph
			 graph)
		{
			return ProcessStatementEx(mt, root, graph);
		}

		private bool ProcessStatementEx(StructMethod mt, RootStatement root, ControlFlowGraph
			 graph)
		{
			int bytecode_version = mt.GetClassStruct().GetBytecodeVersion();
			LinkedList<Statement> stack = new LinkedList<Statement>();
			stack.AddLast(root);
			while (!(stack.Count == 0))
			{
				Statement stat = Sharpen.Collections.RemoveLast(stack);
				Statement parent = stat.GetParent();
				if (parent != null && parent.type == Statement.Type_Catchall && stat == parent.GetFirst
					() && !parent.IsCopied())
				{
					CatchAllStatement fin = (CatchAllStatement)parent;
					BasicBlock head = fin.GetBasichead().GetBlock();
					BasicBlock handler = fin.GetHandler().GetBasichead().GetBlock();
					if (catchallBlockIDs.ContainsKey(handler.id))
					{
					}
					else if (finallyBlockIDs.ContainsKey(handler.id))
					{
						// do nothing
						fin.SetFinally(true);
						int? var = finallyBlockIDs.GetOrNullable(handler.id);
						fin.SetMonitor(var == null ? null : new VarExprent(var.Value, VarType.Vartype_Int
							, varProcessor));
					}
					else
					{
						FinallyProcessor.Record inf = GetFinallyInformation(mt, root, fin);
						if (inf == null)
						{
							// inconsistent finally
							Sharpen.Collections.Put(catchallBlockIDs, handler.id, null);
						}
						else
						{
							if (DecompilerContext.GetOption(IFernflowerPreferences.Finally_Deinline) && VerifyFinallyEx
								(graph, fin, inf))
							{
								Sharpen.Collections.Put(finallyBlockIDs, handler.id, null);
							}
							else
							{
								int varindex = DecompilerContext.GetCounterContainer().GetCounterAndIncrement(CounterContainer
									.Var_Counter);
								InsertSemaphore(graph, GetAllBasicBlocks(fin.GetFirst()), head, handler, varindex
									, inf, bytecode_version);
								Sharpen.Collections.Put(finallyBlockIDs, handler.id, varindex);
							}
							DeadCodeHelper.RemoveDeadBlocks(graph);
							// e.g. multiple return blocks after a nested finally
							DeadCodeHelper.RemoveEmptyBlocks(graph);
							DeadCodeHelper.MergeBasicBlocks(graph);
						}
						return true;
					}
				}
				Sharpen.Collections.AddAll(stack, stat.GetStats());
			}
			return false;
		}

		private class Record
		{
			internal readonly int firstCode;

			internal readonly Dictionary<BasicBlock, bool> mapLast;

			internal Record(int firstCode, Dictionary<BasicBlock, bool> mapLast)
			{
				this.firstCode = firstCode;
				this.mapLast = mapLast;
			}
		}

		private FinallyProcessor.Record GetFinallyInformation(StructMethod mt, RootStatement
			 root, CatchAllStatement fstat)
		{
			Dictionary<BasicBlock, bool> mapLast = new Dictionary<BasicBlock, bool>();
			BasicBlockStatement firstBlockStatement = fstat.GetHandler().GetBasichead();
			BasicBlock firstBasicBlock = firstBlockStatement.GetBlock();
			Instruction instrFirst = firstBasicBlock.GetInstruction(0);
			int firstcode = 0;
			switch (instrFirst.opcode)
			{
				case ICodeConstants.opc_pop:
				{
					firstcode = 1;
					break;
				}

				case ICodeConstants.opc_astore:
				{
					firstcode = 2;
					break;
				}
			}
			ExprProcessor proc = new ExprProcessor(methodDescriptor, varProcessor);
			proc.ProcessStatement(root, mt.GetClassStruct());
			SSAConstructorSparseEx ssa = new SSAConstructorSparseEx();
			ssa.SplitVariables(root, mt);
			List<Exprent> lstExprents = firstBlockStatement.GetExprents();
			VarVersionPair varpaar = new VarVersionPair((VarExprent)((AssignmentExprent)lstExprents
				[firstcode == 2 ? 1 : 0]).GetLeft());
			FlattenStatementsHelper flatthelper = new FlattenStatementsHelper();
			DirectGraph dgraph = flatthelper.BuildDirectGraph(root);
			LinkedList<DirectNode> stack = new LinkedList<DirectNode>();
			stack.AddLast(dgraph.first);
			HashSet<DirectNode> setVisited = new HashSet<DirectNode>();
			while (!(stack.Count == 0))
			{
				DirectNode node = Sharpen.Collections.RemoveFirst(stack);
				if (setVisited.Contains(node))
				{
					continue;
				}
				setVisited.Add(node);
				BasicBlockStatement blockStatement = null;
				if (node.block != null)
				{
					blockStatement = node.block;
				}
				else if (node.preds.Count == 1)
				{
					blockStatement = node.preds[0].block;
				}
				bool isTrueExit = true;
				if (firstcode != 1)
				{
					isTrueExit = false;
					for (int i = 0; i < node.exprents.Count; i++)
					{
						Exprent exprent = node.exprents[i];
						if (firstcode == 0)
						{
							List<Exprent> lst = exprent.GetAllExprents();
							lst.Add(exprent);
							bool found = false;
							foreach (Exprent expr in lst)
							{
								if (expr.type == Exprent.Exprent_Var && new VarVersionPair((VarExprent)expr).Equals
									(varpaar))
								{
									found = true;
									break;
								}
							}
							if (found)
							{
								found = false;
								if (exprent.type == Exprent.Exprent_Exit)
								{
									ExitExprent exexpr = (ExitExprent)exprent;
									if (exexpr.GetExitType() == ExitExprent.Exit_Throw && exexpr.GetValue().type == Exprent
										.Exprent_Var)
									{
										found = true;
									}
								}
								if (!found)
								{
									return null;
								}
								else
								{
									isTrueExit = true;
								}
							}
						}
						else if (firstcode == 2)
						{
							// search for a load instruction
							if (exprent.type == Exprent.Exprent_Assignment)
							{
								AssignmentExprent assexpr = (AssignmentExprent)exprent;
								if (assexpr.GetRight().type == Exprent.Exprent_Var && new VarVersionPair((VarExprent
									)assexpr.GetRight()).Equals(varpaar))
								{
									Exprent next = null;
									if (i == node.exprents.Count - 1)
									{
										if (node.succs.Count == 1)
										{
											DirectNode nd = node.succs[0];
											if (!(nd.exprents.Count == 0))
											{
												next = nd.exprents[0];
											}
										}
									}
									else
									{
										next = node.exprents[i + 1];
									}
									bool found = false;
									if (next != null && next.type == Exprent.Exprent_Exit)
									{
										ExitExprent exexpr = (ExitExprent)next;
										if (exexpr.GetExitType() == ExitExprent.Exit_Throw && exexpr.GetValue().type == Exprent
											.Exprent_Var && assexpr.GetLeft().Equals(exexpr.GetValue()))
										{
											found = true;
										}
									}
									if (!found)
									{
										return null;
									}
									else
									{
										isTrueExit = true;
									}
								}
							}
						}
					}
				}
				// find finally exits
				if (blockStatement != null && blockStatement.GetBlock() != null)
				{
					Statement handler = fstat.GetHandler();
					foreach (StatEdge edge in blockStatement.GetSuccessorEdges(Statement.Statedge_Direct_All
						))
					{
						if (edge.GetType() != StatEdge.Type_Regular && handler.ContainsStatement(blockStatement
							) && !handler.ContainsStatement(edge.GetDestination()))
						{
							bool? existingFlag = mapLast.GetOrNullable(blockStatement.GetBlock());
							// note: the dummy node is also processed!
							if (existingFlag == null || !existingFlag.Value)
							{
								Sharpen.Collections.Put(mapLast, blockStatement.GetBlock(), isTrueExit);
								break;
							}
						}
					}
				}
				Sharpen.Collections.AddAll(stack, node.succs);
			}
			// empty finally block?
			if (fstat.GetHandler().type == Statement.Type_Basicblock)
			{
				bool isEmpty = false;
				bool isFirstLast = mapLast.ContainsKey(firstBasicBlock);
				InstructionSequence seq = firstBasicBlock.GetSeq();
				switch (firstcode)
				{
					case 0:
					{
						isEmpty = isFirstLast && seq.Length() == 1;
						break;
					}

					case 1:
					{
						isEmpty = seq.Length() == 1;
						break;
					}

					case 2:
					{
						isEmpty = isFirstLast ? seq.Length() == 3 : seq.Length() == 1;
						break;
					}
				}
				if (isEmpty)
				{
					firstcode = 3;
				}
			}
			return new FinallyProcessor.Record(firstcode, mapLast);
		}

		private static void InsertSemaphore(ControlFlowGraph graph, HashSet<BasicBlock> setTry
			, BasicBlock head, BasicBlock handler, int var, FinallyProcessor.Record information
			, int bytecode_version)
		{
			HashSet<BasicBlock> setCopy = new HashSet<BasicBlock>(setTry);
			int finallytype = information.firstCode;
			Dictionary<BasicBlock, bool> mapLast = information.mapLast;
			// first and last statements
			RemoveExceptionInstructionsEx(handler, 1, finallytype);
			foreach (KeyValuePair<BasicBlock, bool> entry in mapLast)
			{
				BasicBlock last = entry.Key;
				if (entry.Value)
				{
					RemoveExceptionInstructionsEx(last, 2, finallytype);
					graph.GetFinallyExits().Add(last);
				}
			}
			// disable semaphore at statement exit points
			foreach (BasicBlock block in setTry)
			{
				List<BasicBlock> lstSucc = block.GetSuccs();
				foreach (BasicBlock dest in lstSucc)
				{
					// break out
					if (dest != graph.GetLast() && !setCopy.Contains(dest))
					{
						// disable semaphore
						SimpleInstructionSequence seq = new SimpleInstructionSequence();
						seq.AddInstruction(Instruction.Create(ICodeConstants.opc_bipush, false, ICodeConstants
							.Group_General, bytecode_version, new int[] { 0 }), -1);
						seq.AddInstruction(Instruction.Create(ICodeConstants.opc_istore, false, ICodeConstants
							.Group_General, bytecode_version, new int[] { var }), -1);
						// build a separate block
						BasicBlock newblock = new BasicBlock(++graph.last_id);
						newblock.SetSeq(seq);
						// insert between block and dest
						block.ReplaceSuccessor(dest, newblock);
						newblock.AddSuccessor(dest);
						setCopy.Add(newblock);
						graph.GetBlocks().AddWithKey(newblock, newblock.id);
						// exception ranges
						// FIXME: special case synchronized
						// copy exception edges and extend protected ranges
						for (int j = 0; j < block.GetSuccExceptions().Count; j++)
						{
							BasicBlock hd = block.GetSuccExceptions()[j];
							newblock.AddSuccessorException(hd);
							ExceptionRangeCFG range = graph.GetExceptionRange(hd, block);
							range.GetProtectedRange().Add(newblock);
						}
					}
				}
			}
			// enable semaphore at the statement entrance
			SimpleInstructionSequence seq_1 = new SimpleInstructionSequence();
			seq_1.AddInstruction(Instruction.Create(ICodeConstants.opc_bipush, false, ICodeConstants
				.Group_General, bytecode_version, new int[] { 1 }), -1);
			seq_1.AddInstruction(Instruction.Create(ICodeConstants.opc_istore, false, ICodeConstants
				.Group_General, bytecode_version, new int[] { var }), -1);
			BasicBlock newhead = new BasicBlock(++graph.last_id);
			newhead.SetSeq(seq_1);
			InsertBlockBefore(graph, head, newhead);
			// initialize semaphor with false
			seq_1 = new SimpleInstructionSequence();
			seq_1.AddInstruction(Instruction.Create(ICodeConstants.opc_bipush, false, ICodeConstants
				.Group_General, bytecode_version, new int[] { 0 }), -1);
			seq_1.AddInstruction(Instruction.Create(ICodeConstants.opc_istore, false, ICodeConstants
				.Group_General, bytecode_version, new int[] { var }), -1);
			BasicBlock newheadinit = new BasicBlock(++graph.last_id);
			newheadinit.SetSeq(seq_1);
			InsertBlockBefore(graph, newhead, newheadinit);
			setCopy.Add(newhead);
			setCopy.Add(newheadinit);
			foreach (BasicBlock hd in new HashSet<BasicBlock>(newheadinit.GetSuccExceptions()
				))
			{
				ExceptionRangeCFG range = graph.GetExceptionRange(hd, newheadinit);
				if (setCopy.ContainsAll(range.GetProtectedRange()))
				{
					newheadinit.RemoveSuccessorException(hd);
					range.GetProtectedRange().Remove(newheadinit);
				}
			}
		}

		private static void InsertBlockBefore(ControlFlowGraph graph, BasicBlock oldblock
			, BasicBlock newblock)
		{
			List<BasicBlock> lstTemp = new List<BasicBlock>();
			Sharpen.Collections.AddAll(lstTemp, oldblock.GetPreds());
			Sharpen.Collections.AddAll(lstTemp, oldblock.GetPredExceptions());
			// replace predecessors
			foreach (BasicBlock pred in lstTemp)
			{
				pred.ReplaceSuccessor(oldblock, newblock);
			}
			// copy exception edges and extend protected ranges
			foreach (BasicBlock hd in oldblock.GetSuccExceptions())
			{
				newblock.AddSuccessorException(hd);
				ExceptionRangeCFG range = graph.GetExceptionRange(hd, oldblock);
				range.GetProtectedRange().Add(newblock);
			}
			// replace handler
			foreach (ExceptionRangeCFG range in graph.GetExceptions())
			{
				if (range.GetHandler() == oldblock)
				{
					range.SetHandler(newblock);
				}
			}
			newblock.AddSuccessor(oldblock);
			graph.GetBlocks().AddWithKey(newblock, newblock.id);
			if (graph.GetFirst() == oldblock)
			{
				graph.SetFirst(newblock);
			}
		}

		private static HashSet<BasicBlock> GetAllBasicBlocks(Statement stat)
		{
			List<Statement> lst = new List<Statement>(new LinkedList<Statement>());
			lst.Add(stat);
			int index = 0;
			do
			{
				Statement st = lst[index];
				if (st.type == Statement.Type_Basicblock)
				{
					index++;
				}
				else
				{
					Sharpen.Collections.AddAll(lst, st.GetStats());
					lst.RemoveAtReturningValue(index);
				}
			}
			while (index < lst.Count);
			HashSet<BasicBlock> res = new HashSet<BasicBlock>();
			foreach (Statement st in lst)
			{
				res.Add(((BasicBlockStatement)st).GetBlock());
			}
			return res;
		}

		private bool VerifyFinallyEx(ControlFlowGraph graph, CatchAllStatement fstat, FinallyProcessor.Record
			 information)
		{
			HashSet<BasicBlock> tryBlocks = GetAllBasicBlocks(fstat.GetFirst());
			HashSet<BasicBlock> catchBlocks = GetAllBasicBlocks(fstat.GetHandler());
			int finallytype = information.firstCode;
			Dictionary<BasicBlock, bool> mapLast = information.mapLast;
			BasicBlock first = fstat.GetHandler().GetBasichead().GetBlock();
			bool skippedFirst = false;
			if (finallytype == 3)
			{
				// empty finally
				RemoveExceptionInstructionsEx(first, 3, finallytype);
				if (mapLast.ContainsKey(first))
				{
					graph.GetFinallyExits().Add(first);
				}
				return true;
			}
			else if (first.GetSeq().Length() == 1 && finallytype > 0)
			{
				BasicBlock firstsuc = first.GetSuccs()[0];
				if (catchBlocks.Contains(firstsuc))
				{
					first = firstsuc;
					skippedFirst = true;
				}
			}
			// identify start blocks
			HashSet<BasicBlock> startBlocks = new HashSet<BasicBlock>();
			foreach (BasicBlock block in tryBlocks)
			{
				Sharpen.Collections.AddAll(startBlocks, block.GetSuccs());
			}
			// throw in the try body will point directly to the dummy exit
			// so remove dummy exit
			startBlocks.Remove(graph.GetLast());
			startBlocks.RemoveAll(tryBlocks);
			List<FinallyProcessor.Area> lstAreas = new List<FinallyProcessor.Area>();
			foreach (BasicBlock start in startBlocks)
			{
				FinallyProcessor.Area arr = CompareSubgraphsEx(graph, start, catchBlocks, first, 
					finallytype, mapLast, skippedFirst);
				if (arr == null)
				{
					return false;
				}
				lstAreas.Add(arr);
			}
			//		try {
			//			DotExporter.toDotFile(graph, new File("c:\\Temp\\fern5.dot"), true);
			//		} catch(Exception ex){ex.printStackTrace();}
			// delete areas
			foreach (FinallyProcessor.Area area in lstAreas)
			{
				DeleteArea(graph, area);
			}
			//		try {
			//			DotExporter.toDotFile(graph, new File("c:\\Temp\\fern5.dot"), true);
			//		} catch(Exception ex){ex.printStackTrace();}
			// INFO: empty basic blocks may remain in the graph!
			foreach (KeyValuePair<BasicBlock, bool> entry in mapLast)
			{
				BasicBlock last = entry.Key;
				if (entry.Value)
				{
					RemoveExceptionInstructionsEx(last, 2, finallytype);
					graph.GetFinallyExits().Add(last);
				}
			}
			RemoveExceptionInstructionsEx(fstat.GetHandler().GetBasichead().GetBlock(), 1, finallytype
				);
			return true;
		}

		private class Area
		{
			internal readonly BasicBlock start;

			internal readonly HashSet<BasicBlock> sample;

			internal readonly BasicBlock next;

			internal Area(BasicBlock start, HashSet<BasicBlock> sample, BasicBlock next)
			{
				this.start = start;
				this.sample = sample;
				this.next = next;
			}
		}

		private FinallyProcessor.Area CompareSubgraphsEx(ControlFlowGraph graph, BasicBlock
			 startSample, HashSet<BasicBlock> catchBlocks, BasicBlock startCatch, int finallytype
			, Dictionary<BasicBlock, bool> mapLast, bool skippedFirst)
		{
			// TODO: correct handling (merging) of multiple paths
			List<_T1926163957> stack = new List<_T1926163957>(new LinkedList<_T1926163957>());
			HashSet<BasicBlock> setSample = new HashSet<BasicBlock>();
			Dictionary<string, BasicBlock[]> mapNext = new Dictionary<string, BasicBlock[]>(
				);
			stack.Add(new _T1926163957(this, startCatch, startSample, new List<int[]>()));
			while (!(stack.Count == 0))
			{
				_T1926163957 entry = stack.RemoveAtReturningValue(0);
				BasicBlock blockCatch = entry.blockCatch;
				BasicBlock blockSample = entry.blockSample;
				bool isFirstBlock = !skippedFirst && blockCatch == startCatch;
				bool isLastBlock = mapLast.ContainsKey(blockCatch);
				bool isTrueLastBlock = isLastBlock && (mapLast.GetOrNullable(blockCatch) ?? false);
				if (!CompareBasicBlocksEx(graph, blockCatch, blockSample, (isFirstBlock ? 1 : 0) 
					| (isTrueLastBlock ? 2 : 0), finallytype, entry.lstStoreVars))
				{
					return null;
				}
				if (blockSample.GetSuccs().Count != blockCatch.GetSuccs().Count)
				{
					return null;
				}
				setSample.Add(blockSample);
				// direct successors
				for (int i = 0; i < blockCatch.GetSuccs().Count; i++)
				{
					BasicBlock sucCatch = blockCatch.GetSuccs()[i];
					BasicBlock sucSample = blockSample.GetSuccs()[i];
					if (catchBlocks.Contains(sucCatch) && !setSample.Contains(sucSample))
					{
						stack.Add(new _T1926163957(this, sucCatch, sucSample, entry.lstStoreVars));
					}
				}
				// exception successors
				if (isLastBlock && blockSample.GetSeq().IsEmpty())
				{
				}
				else if (blockCatch.GetSuccExceptions().Count == blockSample.GetSuccExceptions().
					Count)
				{
					// do nothing, blockSample will be removed anyway
					for (int i = 0; i < blockCatch.GetSuccExceptions().Count; i++)
					{
						BasicBlock sucCatch = blockCatch.GetSuccExceptions()[i];
						BasicBlock sucSample = blockSample.GetSuccExceptions()[i];
						string excCatch = graph.GetExceptionRange(sucCatch, blockCatch).GetUniqueExceptionsString
							();
						string excSample = graph.GetExceptionRange(sucSample, blockSample).GetUniqueExceptionsString
							();
						// FIXME: compare handlers if possible
						bool equalexc = excCatch == null ? excSample == null : excCatch.Equals(excSample);
						if (equalexc)
						{
							if (catchBlocks.Contains(sucCatch) && !setSample.Contains(sucSample))
							{
								List<int[]> lst = entry.lstStoreVars;
								if (sucCatch.GetSeq().Length() > 0 && sucSample.GetSeq().Length() > 0)
								{
									Instruction instrCatch = sucCatch.GetSeq().GetInstr(0);
									Instruction instrSample = sucSample.GetSeq().GetInstr(0);
									if (instrCatch.opcode == ICodeConstants.opc_astore && instrSample.opcode == ICodeConstants
										.opc_astore)
									{
										lst = new List<int[]>(lst);
										lst.Add(new int[] { instrCatch.Operand(0), instrSample.Operand(0) });
									}
								}
								stack.Add(new _T1926163957(this, sucCatch, sucSample, lst));
							}
						}
						else
						{
							return null;
						}
					}
				}
				else
				{
					return null;
				}
				if (isLastBlock)
				{
					HashSet<BasicBlock> setSuccs = new HashSet<BasicBlock>(blockSample.GetSuccs());
					setSuccs.RemoveAll(setSample);
					foreach (_T1926163957 stackent in stack)
					{
						setSuccs.Remove(stackent.blockSample);
					}
					foreach (BasicBlock succ in setSuccs)
					{
						if (graph.GetLast() != succ)
						{
							// FIXME: why?
							Sharpen.Collections.Put(mapNext, blockSample.id + "#" + succ.id, new BasicBlock[]
								 { blockSample, succ, isTrueLastBlock ? succ : null });
						}
					}
				}
			}
			return new FinallyProcessor.Area(startSample, setSample, GetUniqueNext(graph, new 
				HashSet<BasicBlock[]>(mapNext.Values)));
		}

		internal class _T1926163957
		{
			public readonly BasicBlock blockCatch;

			public readonly BasicBlock blockSample;

			public readonly List<int[]> lstStoreVars;

			internal _T1926163957(FinallyProcessor _enclosing, BasicBlock blockCatch, BasicBlock
				 blockSample, List<int[]> lstStoreVars)
			{
				this._enclosing = _enclosing;
				this.blockCatch = blockCatch;
				this.blockSample = blockSample;
				this.lstStoreVars = new List<int[]>(lstStoreVars);
			}

			private readonly FinallyProcessor _enclosing;
		}

		private static BasicBlock GetUniqueNext(ControlFlowGraph graph, HashSet<BasicBlock
			[]> setNext)
		{
			// precondition: there is at most one true exit path in a finally statement
			BasicBlock next = null;
			bool multiple = false;
			foreach (BasicBlock[] arr in setNext)
			{
				if (arr[2] != null)
				{
					next = arr[1];
					multiple = false;
					break;
				}
				else
				{
					if (next == null)
					{
						next = arr[1];
					}
					else if (next != arr[1])
					{
						multiple = true;
					}
					if (arr[1].GetPreds().Count == 1)
					{
						next = arr[1];
					}
				}
			}
			if (multiple)
			{
				// TODO: generic solution
				foreach (BasicBlock[] arr in setNext)
				{
					BasicBlock block = arr[1];
					if (block != next)
					{
						if (InterpreterUtil.EqualSets(next.GetSuccs(), block.GetSuccs()))
						{
							InstructionSequence seqNext = next.GetSeq();
							InstructionSequence seqBlock = block.GetSeq();
							if (seqNext.Length() == seqBlock.Length())
							{
								for (int i = 0; i < seqNext.Length(); i++)
								{
									Instruction instrNext = seqNext.GetInstr(i);
									Instruction instrBlock = seqBlock.GetInstr(i);
									if (!Instruction.Equals(instrNext, instrBlock))
									{
										return null;
									}
									for (int j = 0; j < instrNext.OperandsCount(); j++)
									{
										if (instrNext.Operand(j) != instrBlock.Operand(j))
										{
											return null;
										}
									}
								}
							}
							else
							{
								return null;
							}
						}
						else
						{
							return null;
						}
					}
				}
				//			try {
				//				DotExporter.toDotFile(graph, new File("c:\\Temp\\fern5.dot"), true);
				//			} catch(IOException ex) {
				//				ex.printStackTrace();
				//			}
				foreach (BasicBlock[] arr in setNext)
				{
					if (arr[1] != next)
					{
						// FIXME: exception edge possible?
						arr[0].RemoveSuccessor(arr[1]);
						arr[0].AddSuccessor(next);
					}
				}
				DeadCodeHelper.RemoveDeadBlocks(graph);
			}
			return next;
		}

		private bool CompareBasicBlocksEx(ControlFlowGraph graph, BasicBlock pattern, BasicBlock
			 sample, int type, int finallytype, List<int[]> lstStoreVars)
		{
			InstructionSequence seqPattern = pattern.GetSeq();
			InstructionSequence seqSample = sample.GetSeq();
			if (type != 0)
			{
				seqPattern = seqPattern.Clone();
				if ((type & 1) > 0)
				{
					// first
					if (finallytype > 0)
					{
						seqPattern.RemoveInstruction(0);
					}
				}
				if ((type & 2) > 0)
				{
					// last
					if (finallytype == 0 || finallytype == 2)
					{
						seqPattern.RemoveLast();
					}
					if (finallytype == 2)
					{
						seqPattern.RemoveLast();
					}
				}
			}
			if (seqPattern.Length() > seqSample.Length())
			{
				return false;
			}
			for (int i = 0; i < seqPattern.Length(); i++)
			{
				Instruction instrPattern = seqPattern.GetInstr(i);
				Instruction instrSample = seqSample.GetInstr(i);
				// compare instructions with respect to jumps
				if (!EqualInstructions(instrPattern, instrSample, lstStoreVars))
				{
					return false;
				}
			}
			if (seqPattern.Length() < seqSample.Length())
			{
				// split in two blocks
				SimpleInstructionSequence seq = new SimpleInstructionSequence();
				LinkedList<int> oldOffsets = new LinkedList<int>();
				for (int i = seqSample.Length() - 1; i >= seqPattern.Length(); i--)
				{
					seq.AddInstruction(0, seqSample.GetInstr(i), -1);
					oldOffsets.AddFirst(sample.GetOldOffset(i));
					seqSample.RemoveInstruction(i);
				}
				BasicBlock newblock = new BasicBlock(++graph.last_id);
				newblock.SetSeq(seq);
				Sharpen.Collections.AddAll(newblock.GetInstrOldOffsets(), oldOffsets);
				List<BasicBlock> lstTemp = new List<BasicBlock>(sample.GetSuccs());
				// move successors
				foreach (BasicBlock suc in lstTemp)
				{
					sample.RemoveSuccessor(suc);
					newblock.AddSuccessor(suc);
				}
				sample.AddSuccessor(newblock);
				graph.GetBlocks().AddWithKey(newblock, newblock.id);
				HashSet<BasicBlock> setFinallyExits = graph.GetFinallyExits();
				if (setFinallyExits.Contains(sample))
				{
					setFinallyExits.Remove(sample);
					setFinallyExits.Add(newblock);
				}
				// copy exception edges and extend protected ranges
				for (int j = 0; j < sample.GetSuccExceptions().Count; j++)
				{
					BasicBlock hd = sample.GetSuccExceptions()[j];
					newblock.AddSuccessorException(hd);
					ExceptionRangeCFG range = graph.GetExceptionRange(hd, sample);
					range.GetProtectedRange().Add(newblock);
				}
			}
			return true;
		}

		public virtual bool EqualInstructions(Instruction first, Instruction second, IList
			<int[]> lstStoreVars)
		{
			if (!Instruction.Equals(first, second))
			{
				return false;
			}
			if (first.group != ICodeConstants.Group_Jump)
			{
				// FIXME: switch comparison
				for (int i = 0; i < first.OperandsCount(); i++)
				{
					int firstOp = first.Operand(i);
					int secondOp = second.Operand(i);
					if (firstOp != secondOp)
					{
						// a-load/store instructions
						if (first.opcode == ICodeConstants.opc_aload || first.opcode == ICodeConstants.opc_astore)
						{
							foreach (int[] arr in lstStoreVars)
							{
								if (arr[0] == firstOp && arr[1] == secondOp)
								{
									return true;
								}
							}
						}
						return false;
					}
				}
			}
			return true;
		}

		private static void DeleteArea(ControlFlowGraph graph, FinallyProcessor.Area area
			)
		{
			BasicBlock start = area.start;
			BasicBlock next = area.next;
			if (start == next)
			{
				return;
			}
			if (next == null)
			{
				// dummy exit block
				next = graph.GetLast();
			}
			// collect common exception ranges of predecessors and successors
			HashSet<BasicBlock> setCommonExceptionHandlers = new HashSet<BasicBlock>(next.GetSuccExceptions
				());
			foreach (BasicBlock pred in start.GetPreds())
			{
				setCommonExceptionHandlers.RetainAll(pred.GetSuccExceptions());
			}
			bool is_outside_range = false;
			HashSet<BasicBlock> setPredecessors = new HashSet<BasicBlock>(start.GetPreds());
			// replace start with next
			foreach (BasicBlock pred in setPredecessors)
			{
				pred.ReplaceSuccessor(start, next);
			}
			HashSet<BasicBlock> setBlocks = area.sample;
			HashSet<ExceptionRangeCFG> setCommonRemovedExceptionRanges = null;
			// remove all the blocks inbetween
			foreach (BasicBlock block in setBlocks)
			{
				// artificial basic blocks (those resulted from splitting)
				// can belong to more than one area
				if (graph.GetBlocks().ContainsKey(block.id))
				{
					if (!block.GetSuccExceptions().ContainsAll(setCommonExceptionHandlers))
					{
						is_outside_range = true;
					}
					HashSet<ExceptionRangeCFG> setRemovedExceptionRanges = new HashSet<ExceptionRangeCFG
						>();
					foreach (BasicBlock handler in block.GetSuccExceptions())
					{
						setRemovedExceptionRanges.Add(graph.GetExceptionRange(handler, block));
					}
					if (setCommonRemovedExceptionRanges == null)
					{
						setCommonRemovedExceptionRanges = setRemovedExceptionRanges;
					}
					else
					{
						setCommonRemovedExceptionRanges.RetainAll(setRemovedExceptionRanges);
					}
					// shift extern edges on splitted blocks
					if (block.GetSeq().IsEmpty() && block.GetSuccs().Count == 1)
					{
						BasicBlock succs = block.GetSuccs()[0];
						foreach (BasicBlock pred in new List<BasicBlock>(block.GetPreds()))
						{
							if (!setBlocks.Contains(pred))
							{
								pred.ReplaceSuccessor(block, succs);
							}
						}
						if (graph.GetFirst() == block)
						{
							graph.SetFirst(succs);
						}
					}
					graph.RemoveBlock(block);
				}
			}
			if (is_outside_range)
			{
				// new empty block
				BasicBlock emptyblock = new BasicBlock(++graph.last_id);
				graph.GetBlocks().AddWithKey(emptyblock, emptyblock.id);
				// add to ranges if necessary
				foreach (ExceptionRangeCFG range in setCommonRemovedExceptionRanges)
				{
					emptyblock.AddSuccessorException(range.GetHandler());
					range.GetProtectedRange().Add(emptyblock);
				}
				// insert between predecessors and next
				emptyblock.AddSuccessor(next);
				foreach (BasicBlock pred in setPredecessors)
				{
					pred.ReplaceSuccessor(next, emptyblock);
				}
			}
		}

		private static void RemoveExceptionInstructionsEx(BasicBlock block, int blocktype
			, int finallytype)
		{
			InstructionSequence seq = block.GetSeq();
			if (finallytype == 3)
			{
				// empty finally handler
				for (int i = seq.Length() - 1; i >= 0; i--)
				{
					seq.RemoveInstruction(i);
				}
			}
			else
			{
				if ((blocktype & 1) > 0)
				{
					// first
					if (finallytype == 2 || finallytype == 1)
					{
						// astore or pop
						seq.RemoveInstruction(0);
					}
				}
				if ((blocktype & 2) > 0)
				{
					// last
					if (finallytype == 2 || finallytype == 0)
					{
						seq.RemoveLast();
					}
					if (finallytype == 2)
					{
						// astore
						seq.RemoveLast();
					}
				}
			}
		}
	}
}
