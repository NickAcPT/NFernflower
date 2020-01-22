// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Code.Cfg;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Sforms;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class ExprProcessor : ICodeConstants
	{
		public const string Undefined_Type_String = "<undefinedtype>";

		public const string Unknown_Type_String = "<unknown>";

		public const string Null_Type_String = "<null>";

		private static readonly Dictionary<int, int> mapConsts = new Dictionary<int, int
			>();

		static ExprProcessor()
		{
			Sharpen.Collections.Put(mapConsts, opc_arraylength, FunctionExprent.Function_Array_Length
				);
			Sharpen.Collections.Put(mapConsts, opc_checkcast, FunctionExprent.Function_Cast);
			Sharpen.Collections.Put(mapConsts, opc_instanceof, FunctionExprent.Function_Instanceof
				);
		}

		private static readonly VarType[] consts = new VarType[] { VarType.Vartype_Int, VarType
			.Vartype_Float, VarType.Vartype_Long, VarType.Vartype_Double, VarType.Vartype_Class
			, VarType.Vartype_String };

		private static readonly VarType[] varTypes = new VarType[] { VarType.Vartype_Int, 
			VarType.Vartype_Long, VarType.Vartype_Float, VarType.Vartype_Double, VarType.Vartype_Object
			 };

		private static readonly VarType[] arrTypes = new VarType[] { VarType.Vartype_Int, 
			VarType.Vartype_Long, VarType.Vartype_Float, VarType.Vartype_Double, VarType.Vartype_Object
			, VarType.Vartype_Boolean, VarType.Vartype_Char, VarType.Vartype_Short };

		private static readonly int[] func1 = new int[] { FunctionExprent.Function_Add, FunctionExprent
			.Function_Sub, FunctionExprent.Function_Mul, FunctionExprent.Function_Div, FunctionExprent
			.Function_Rem };

		private static readonly int[] func2 = new int[] { FunctionExprent.Function_Shl, FunctionExprent
			.Function_Shr, FunctionExprent.Function_Ushr, FunctionExprent.Function_And, FunctionExprent
			.Function_Or, FunctionExprent.Function_Xor };

		private static readonly int[] func3 = new int[] { FunctionExprent.Function_I2l, FunctionExprent
			.Function_I2f, FunctionExprent.Function_I2d, FunctionExprent.Function_L2i, FunctionExprent
			.Function_L2f, FunctionExprent.Function_L2d, FunctionExprent.Function_F2i, FunctionExprent
			.Function_F2l, FunctionExprent.Function_F2d, FunctionExprent.Function_D2i, FunctionExprent
			.Function_D2l, FunctionExprent.Function_D2f, FunctionExprent.Function_I2b, FunctionExprent
			.Function_I2c, FunctionExprent.Function_I2s };

		private static readonly int[] func4 = new int[] { FunctionExprent.Function_Lcmp, 
			FunctionExprent.Function_Fcmpl, FunctionExprent.Function_Fcmpg, FunctionExprent.
			Function_Dcmpl, FunctionExprent.Function_Dcmpg };

		private static readonly int[] func5 = new int[] { IfExprent.If_Eq, IfExprent.If_Ne
			, IfExprent.If_Lt, IfExprent.If_Ge, IfExprent.If_Gt, IfExprent.If_Le };

		private static readonly int[] func6 = new int[] { IfExprent.If_Icmpeq, IfExprent.
			If_Icmpne, IfExprent.If_Icmplt, IfExprent.If_Icmpge, IfExprent.If_Icmpgt, IfExprent
			.If_Icmple, IfExprent.If_Acmpeq, IfExprent.If_Acmpne };

		private static readonly int[] func7 = new int[] { IfExprent.If_Null, IfExprent.If_Nonnull
			 };

		private static readonly int[] func8 = new int[] { MonitorExprent.Monitor_Enter, MonitorExprent
			.Monitor_Exit };

		private static readonly int[] arrTypeIds = new int[] { ICodeConstants.Type_Boolean
			, ICodeConstants.Type_Char, ICodeConstants.Type_Float, ICodeConstants.Type_Double
			, ICodeConstants.Type_Byte, ICodeConstants.Type_Short, ICodeConstants.Type_Int, 
			ICodeConstants.Type_Long };

		private static readonly int[] negIfs = new int[] { IfExprent.If_Ne, IfExprent.If_Eq
			, IfExprent.If_Ge, IfExprent.If_Lt, IfExprent.If_Le, IfExprent.If_Gt, IfExprent.
			If_Nonnull, IfExprent.If_Null, IfExprent.If_Icmpne, IfExprent.If_Icmpeq, IfExprent
			.If_Icmpge, IfExprent.If_Icmplt, IfExprent.If_Icmple, IfExprent.If_Icmpgt, IfExprent
			.If_Acmpne, IfExprent.If_Acmpeq };

		private static readonly string[] typeNames = new string[] { "byte", "char", "double"
			, "float", "int", "long", "short", "boolean" };

		private readonly MethodDescriptor methodDescriptor;

		private readonly VarProcessor varProcessor;

		public ExprProcessor(MethodDescriptor md, VarProcessor varProc)
		{
			methodDescriptor = md;
			varProcessor = varProc;
		}

		public virtual void ProcessStatement(RootStatement root, StructClass cl)
		{
			FlattenStatementsHelper flatthelper = new FlattenStatementsHelper();
			DirectGraph dgraph = flatthelper.BuildDirectGraph(root);
			// collect finally entry points
			HashSet<string> setFinallyShortRangeEntryPoints = new HashSet<string>();
			foreach (List<FlattenStatementsHelper.FinallyPathWrapper> lst in dgraph.mapShortRangeFinallyPaths
				.Values)
			{
				foreach (FlattenStatementsHelper.FinallyPathWrapper finwrap in lst)
				{
					setFinallyShortRangeEntryPoints.Add(finwrap.entry);
				}
			}
			HashSet<string> setFinallyLongRangeEntryPaths = new HashSet<string>();
			foreach (List<FlattenStatementsHelper.FinallyPathWrapper> lst in dgraph.mapLongRangeFinallyPaths
				.Values)
			{
				foreach (FlattenStatementsHelper.FinallyPathWrapper finwrap in lst)
				{
					setFinallyLongRangeEntryPaths.Add(finwrap.source + "##" + finwrap.entry);
				}
			}
			Dictionary<string, VarExprent> mapCatch = new Dictionary<string, VarExprent>();
			CollectCatchVars(root, flatthelper, mapCatch);
			Dictionary<DirectNode, Dictionary<string, PrimitiveExprsList>> mapData = new Dictionary
				<DirectNode, Dictionary<string, PrimitiveExprsList>>();
			LinkedList<DirectNode> stack = new LinkedList<DirectNode>();
			LinkedList<LinkedList<string>> stackEntryPoint = new LinkedList<LinkedList<string
				>>();
			stack.AddLast(dgraph.first);
			stackEntryPoint.AddLast(new LinkedList<string>());
			Dictionary<string, PrimitiveExprsList> map = new Dictionary<string, PrimitiveExprsList
				>();
			Sharpen.Collections.Put(map, null, new PrimitiveExprsList());
			Sharpen.Collections.Put(mapData, dgraph.first, map);
			while (!(stack.Count == 0))
			{
				DirectNode node = Sharpen.Collections.RemoveFirst(stack);
				LinkedList<string> entrypoints = Sharpen.Collections.RemoveFirst(stackEntryPoint);
				PrimitiveExprsList data;
				if (mapCatch.ContainsKey(node.id))
				{
					data = GetExpressionData(mapCatch.GetOrNull(node.id));
				}
				else
				{
					data = mapData.GetOrNull(node).GetOrNull(BuildEntryPointKey(entrypoints));
				}
				BasicBlockStatement block = node.block;
				if (block != null)
				{
					ProcessBlock(block, data, cl);
					block.SetExprents(data.GetLstExprents());
				}
				string currentEntrypoint = (entrypoints.Count == 0) ? null : entrypoints.Last
					.Value;
				foreach (DirectNode nd in node.succs)
				{
					bool isSuccessor = true;
					if (currentEntrypoint != null && dgraph.mapLongRangeFinallyPaths.ContainsKey(node
						.id))
					{
						isSuccessor = false;
						foreach (FlattenStatementsHelper.FinallyPathWrapper finwraplong in dgraph.mapLongRangeFinallyPaths
							.GetOrNull(node.id))
						{
							if (finwraplong.source.Equals(currentEntrypoint) && finwraplong.destination.Equals
								(nd.id))
							{
								isSuccessor = true;
								break;
							}
						}
					}
					if (isSuccessor)
					{
						Dictionary<string, PrimitiveExprsList> mapSucc = mapData.ComputeIfAbsent(nd, (DirectNode
							 k) => new Dictionary<string, PrimitiveExprsList>());
						LinkedList<string> ndentrypoints = new LinkedList<string>(entrypoints);
						if (setFinallyLongRangeEntryPaths.Contains(node.id + "##" + nd.id))
						{
							ndentrypoints.AddLast(node.id);
						}
						else if (!setFinallyShortRangeEntryPoints.Contains(nd.id) && dgraph.mapLongRangeFinallyPaths
							.ContainsKey(node.id))
						{
							Sharpen.Collections.RemoveLast(ndentrypoints);
						}
						// currentEntrypoint should
						// not be null at this point
						// handling of entry point loops
						int succ_entry_index = ndentrypoints.ToList().IndexOf(nd.id);
						if (succ_entry_index >= 0)
						{
							// we are in a loop (e.g. continue in a finally block), drop all entry points in the list beginning with succ_entry_index
							for (int elements_to_remove = ndentrypoints.Count - succ_entry_index; elements_to_remove
								 > 0; elements_to_remove--)
							{
								Sharpen.Collections.RemoveLast(ndentrypoints);
							}
						}
						string ndentrykey = BuildEntryPointKey(ndentrypoints);
						if (!mapSucc.ContainsKey(ndentrykey))
						{
							Sharpen.Collections.Put(mapSucc, ndentrykey, CopyVarExprents(data.CopyStack()));
							stack.AddLast(nd);
							stackEntryPoint.AddLast(ndentrypoints);
						}
					}
				}
			}
			InitStatementExprents(root);
		}

		// FIXME: Ugly code, to be rewritten. A tuple class is needed.
		private static string BuildEntryPointKey(LinkedList<string> entrypoints)
		{
			if ((entrypoints.Count == 0))
			{
				return null;
			}
			else
			{
				StringBuilder buffer = new StringBuilder();
				foreach (string point in entrypoints)
				{
					buffer.Append(point);
					buffer.Append(":");
				}
				return buffer.ToString();
			}
		}

		private static PrimitiveExprsList CopyVarExprents(PrimitiveExprsList data)
		{
			ExprentStack stack = data.GetStack();
			CopyEntries(stack);
			return data;
		}

		public static void CopyEntries(List<Exprent> stack)
		{
			for (int i = 0; i < stack.Count; i++)
			{
				stack[i] = stack[i].Copy();
			}
		}

		private static void CollectCatchVars(Statement stat, FlattenStatementsHelper flatthelper
			, Dictionary<string, VarExprent> map)
		{
			List<VarExprent> lst = null;
			if (stat.type == Statement.Type_Catchall)
			{
				CatchAllStatement catchall = (CatchAllStatement)stat;
				if (!catchall.IsFinally())
				{
					lst = catchall.GetVars();
				}
			}
			else if (stat.type == Statement.Type_Trycatch)
			{
				lst = ((CatchStatement)stat).GetVars();
			}
			if (lst != null)
			{
				for (int i = 1; i < stat.GetStats().Count; i++)
				{
					Sharpen.Collections.Put(map, flatthelper.GetMapDestinationNodes().GetOrNull(stat.
						GetStats()[i].id)[0], lst[i - 1]);
				}
			}
			foreach (Statement st in stat.GetStats())
			{
				CollectCatchVars(st, flatthelper, map);
			}
		}

		private static void InitStatementExprents(Statement stat)
		{
			stat.InitExprents();
			foreach (Statement st in stat.GetStats())
			{
				InitStatementExprents(st);
			}
		}

		public virtual void ProcessBlock(BasicBlockStatement stat, PrimitiveExprsList data
			, StructClass cl)
		{
			ConstantPool pool = cl.GetPool();
			StructBootstrapMethodsAttribute bootstrap = cl.GetAttribute(StructGeneralAttribute
				.Attribute_Bootstrap_Methods);
			BasicBlock block = stat.GetBlock();
			ExprentStack stack = data.GetStack();
			List<Exprent> exprlist = data.GetLstExprents();
			InstructionSequence seq = block.GetSeq();
			for (int i = 0; i < seq.Length(); i++)
			{
				Instruction instr = seq.GetInstr(i);
				int bytecode_offset = block.GetOldOffset(i);
				HashSet<int> bytecode_offsets = bytecode_offset >= 0 ? new System.Collections.Generic.HashSet<
					int>(){{bytecode_offset}} : null;
				switch (instr.opcode)
				{
					case opc_aconst_null:
					{
						PushEx(stack, exprlist, new ConstExprent(VarType.Vartype_Null, null, bytecode_offsets
							));
						break;
					}

					case opc_bipush:
					case opc_sipush:
					{
						PushEx(stack, exprlist, new ConstExprent(instr.Operand(0), true, bytecode_offsets
							));
						break;
					}

					case opc_lconst_0:
					case opc_lconst_1:
					{
						PushEx(stack, exprlist, new ConstExprent(VarType.Vartype_Long, (long)(instr.opcode
							 - opc_lconst_0), bytecode_offsets));
						break;
					}

					case opc_fconst_0:
					case opc_fconst_1:
					case opc_fconst_2:
					{
						PushEx(stack, exprlist, new ConstExprent(VarType.Vartype_Float, (float)(instr.opcode
							 - opc_fconst_0), bytecode_offsets));
						break;
					}

					case opc_dconst_0:
					case opc_dconst_1:
					{
						PushEx(stack, exprlist, new ConstExprent(VarType.Vartype_Double, (double)(instr.opcode
							 - opc_dconst_0), bytecode_offsets));
						break;
					}

					case opc_ldc:
					case opc_ldc_w:
					case opc_ldc2_w:
					{
						PooledConstant cn = pool.GetConstant(instr.Operand(0));
						if (cn is PrimitiveConstant)
						{
							PushEx(stack, exprlist, new ConstExprent(consts[cn.type - CONSTANT_Integer], ((PrimitiveConstant
								)cn).value, bytecode_offsets));
						}
						else if (cn is LinkConstant)
						{
							//TODO: for now treat Links as Strings
							PushEx(stack, exprlist, new ConstExprent(VarType.Vartype_String, ((LinkConstant)cn
								).elementname, bytecode_offsets));
						}
						break;
					}

					case opc_iload:
					case opc_lload:
					case opc_fload:
					case opc_dload:
					case opc_aload:
					{
						PushEx(stack, exprlist, new VarExprent(instr.Operand(0), varTypes[instr.opcode - 
							opc_iload], varProcessor, bytecode_offset));
						break;
					}

					case opc_iaload:
					case opc_laload:
					case opc_faload:
					case opc_daload:
					case opc_aaload:
					case opc_baload:
					case opc_caload:
					case opc_saload:
					{
						Exprent index = stack.Pop();
						Exprent arr = stack.Pop();
						VarType vartype = null;
						switch (instr.opcode)
						{
							case opc_laload:
							{
								vartype = VarType.Vartype_Long;
								break;
							}

							case opc_daload:
							{
								vartype = VarType.Vartype_Double;
								break;
							}
						}
						PushEx(stack, exprlist, new ArrayExprent(arr, index, arrTypes[instr.opcode - opc_iaload
							], bytecode_offsets), vartype);
						break;
					}

					case opc_istore:
					case opc_lstore:
					case opc_fstore:
					case opc_dstore:
					case opc_astore:
					{
						Exprent expr = stack.Pop();
						int varindex = instr.Operand(0);
						AssignmentExprent assign = new AssignmentExprent(new VarExprent(varindex, varTypes
							[instr.opcode - opc_istore], varProcessor, NextMeaningfulOffset(block, i)), expr
							, bytecode_offsets);
						exprlist.Add(assign);
						break;
					}

					case opc_iastore:
					case opc_lastore:
					case opc_fastore:
					case opc_dastore:
					case opc_aastore:
					case opc_bastore:
					case opc_castore:
					case opc_sastore:
					{
						Exprent value = stack.Pop();
						Exprent index_store = stack.Pop();
						Exprent arr_store = stack.Pop();
						AssignmentExprent arrassign = new AssignmentExprent(new ArrayExprent(arr_store, index_store
							, arrTypes[instr.opcode - opc_iastore], bytecode_offsets), value, bytecode_offsets
							);
						exprlist.Add(arrassign);
						break;
					}

					case opc_iadd:
					case opc_ladd:
					case opc_fadd:
					case opc_dadd:
					case opc_isub:
					case opc_lsub:
					case opc_fsub:
					case opc_dsub:
					case opc_imul:
					case opc_lmul:
					case opc_fmul:
					case opc_dmul:
					case opc_idiv:
					case opc_ldiv:
					case opc_fdiv:
					case opc_ddiv:
					case opc_irem:
					case opc_lrem:
					case opc_frem:
					case opc_drem:
					{
						PushEx(stack, exprlist, new FunctionExprent(func1[(instr.opcode - opc_iadd) / 4], 
							stack, bytecode_offsets));
						break;
					}

					case opc_ishl:
					case opc_lshl:
					case opc_ishr:
					case opc_lshr:
					case opc_iushr:
					case opc_lushr:
					case opc_iand:
					case opc_land:
					case opc_ior:
					case opc_lor:
					case opc_ixor:
					case opc_lxor:
					{
						PushEx(stack, exprlist, new FunctionExprent(func2[(instr.opcode - opc_ishl) / 2], 
							stack, bytecode_offsets));
						break;
					}

					case opc_ineg:
					case opc_lneg:
					case opc_fneg:
					case opc_dneg:
					{
						PushEx(stack, exprlist, new FunctionExprent(FunctionExprent.Function_Neg, stack, 
							bytecode_offsets));
						break;
					}

					case opc_iinc:
					{
						VarExprent vevar = new VarExprent(instr.Operand(0), VarType.Vartype_Int, varProcessor
							);
						exprlist.Add(new AssignmentExprent(vevar, new FunctionExprent(instr.Operand(1) < 
							0 ? FunctionExprent.Function_Sub : FunctionExprent.Function_Add, Sharpen.Arrays.AsList
							(vevar.Copy(), new ConstExprent(VarType.Vartype_Int, System.Math.Abs(instr.Operand
							(1)), null)), bytecode_offsets), bytecode_offsets));
						break;
					}

					case opc_i2l:
					case opc_i2f:
					case opc_i2d:
					case opc_l2i:
					case opc_l2f:
					case opc_l2d:
					case opc_f2i:
					case opc_f2l:
					case opc_f2d:
					case opc_d2i:
					case opc_d2l:
					case opc_d2f:
					case opc_i2b:
					case opc_i2c:
					case opc_i2s:
					{
						PushEx(stack, exprlist, new FunctionExprent(func3[instr.opcode - opc_i2l], stack, 
							bytecode_offsets));
						break;
					}

					case opc_lcmp:
					case opc_fcmpl:
					case opc_fcmpg:
					case opc_dcmpl:
					case opc_dcmpg:
					{
						PushEx(stack, exprlist, new FunctionExprent(func4[instr.opcode - opc_lcmp], stack
							, bytecode_offsets));
						break;
					}

					case opc_ifeq:
					case opc_ifne:
					case opc_iflt:
					case opc_ifge:
					case opc_ifgt:
					case opc_ifle:
					{
						exprlist.Add(new IfExprent(negIfs[func5[instr.opcode - opc_ifeq]], stack, bytecode_offsets
							));
						break;
					}

					case opc_if_icmpeq:
					case opc_if_icmpne:
					case opc_if_icmplt:
					case opc_if_icmpge:
					case opc_if_icmpgt:
					case opc_if_icmple:
					case opc_if_acmpeq:
					case opc_if_acmpne:
					{
						exprlist.Add(new IfExprent(negIfs[func6[instr.opcode - opc_if_icmpeq]], stack, bytecode_offsets
							));
						break;
					}

					case opc_ifnull:
					case opc_ifnonnull:
					{
						exprlist.Add(new IfExprent(negIfs[func7[instr.opcode - opc_ifnull]], stack, bytecode_offsets
							));
						break;
					}

					case opc_tableswitch:
					case opc_lookupswitch:
					{
						exprlist.Add(new SwitchExprent(stack.Pop(), bytecode_offsets));
						break;
					}

					case opc_ireturn:
					case opc_lreturn:
					case opc_freturn:
					case opc_dreturn:
					case opc_areturn:
					case opc_return:
					case opc_athrow:
					{
						exprlist.Add(new ExitExprent(instr.opcode == opc_athrow ? ExitExprent.Exit_Throw : 
							ExitExprent.Exit_Return, instr.opcode == opc_return ? null : stack.Pop(), instr.
							opcode == opc_athrow ? null : methodDescriptor.ret, bytecode_offsets));
						break;
					}

					case opc_monitorenter:
					case opc_monitorexit:
					{
						exprlist.Add(new MonitorExprent(func8[instr.opcode - opc_monitorenter], stack.Pop
							(), bytecode_offsets));
						break;
					}

					case opc_checkcast:
					case opc_instanceof:
					{
						stack.Push(new ConstExprent(new VarType(pool.GetPrimitiveConstant(instr.Operand(0
							)).GetString(), true), null, null));
						goto case opc_arraylength;
					}

					case opc_arraylength:
					{
						PushEx(stack, exprlist, new FunctionExprent((int) mapConsts.GetOrNullable(instr.opcode)
							, stack, bytecode_offsets));
						break;
					}

					case opc_getstatic:
					case opc_getfield:
					{
						PushEx(stack, exprlist, new FieldExprent(pool.GetLinkConstant(instr.Operand(0)), 
							instr.opcode == opc_getstatic ? null : stack.Pop(), bytecode_offsets));
						break;
					}

					case opc_putstatic:
					case opc_putfield:
					{
						Exprent valfield = stack.Pop();
						Exprent exprfield = new FieldExprent(pool.GetLinkConstant(instr.Operand(0)), instr
							.opcode == opc_putstatic ? null : stack.Pop(), bytecode_offsets);
						exprlist.Add(new AssignmentExprent(exprfield, valfield, bytecode_offsets));
						break;
					}

					case opc_invokevirtual:
					case opc_invokespecial:
					case opc_invokestatic:
					case opc_invokeinterface:
					case opc_invokedynamic:
					{
						if (instr.opcode != opc_invokedynamic || instr.bytecodeVersion >= ICodeConstants.
							Bytecode_Java_7)
						{
							LinkConstant invoke_constant = pool.GetLinkConstant(instr.Operand(0));
							List<PooledConstant> bootstrap_arguments = null;
							if (instr.opcode == opc_invokedynamic && bootstrap != null)
							{
								bootstrap_arguments = bootstrap.GetMethodArguments(invoke_constant.index1);
							}
							InvocationExprent exprinv = new InvocationExprent(instr.opcode, invoke_constant, 
								bootstrap_arguments, stack, bytecode_offsets);
							if (exprinv.GetDescriptor().ret.type == ICodeConstants.Type_Void)
							{
								exprlist.Add(exprinv);
							}
							else
							{
								PushEx(stack, exprlist, exprinv);
							}
						}
						break;
					}

					case opc_new:
					case opc_anewarray:
					case opc_multianewarray:
					{
						int dimensions = (instr.opcode == opc_new) ? 0 : (instr.opcode == opc_anewarray) ? 
							1 : instr.Operand(1);
						VarType arrType = new VarType(pool.GetPrimitiveConstant(instr.Operand(0)).GetString
							(), true);
						if (instr.opcode != opc_multianewarray)
						{
							arrType = arrType.ResizeArrayDim(arrType.arrayDim + dimensions);
						}
						PushEx(stack, exprlist, new NewExprent(arrType, stack, dimensions, bytecode_offsets
							));
						break;
					}

					case opc_newarray:
					{
						PushEx(stack, exprlist, new NewExprent(new VarType(arrTypeIds[instr.Operand(0) - 
							4], 1), stack, 1, bytecode_offsets));
						break;
					}

					case opc_dup:
					{
						PushEx(stack, exprlist, stack.GetByOffset(-1).Copy());
						break;
					}

					case opc_dup_x1:
					{
						InsertByOffsetEx(-2, stack, exprlist, -1);
						break;
					}

					case opc_dup_x2:
					{
						if (stack.GetByOffset(-2).GetExprType().stackSize == 2)
						{
							InsertByOffsetEx(-2, stack, exprlist, -1);
						}
						else
						{
							InsertByOffsetEx(-3, stack, exprlist, -1);
						}
						break;
					}

					case opc_dup2:
					{
						if (stack.GetByOffset(-1).GetExprType().stackSize == 2)
						{
							PushEx(stack, exprlist, stack.GetByOffset(-1).Copy());
						}
						else
						{
							PushEx(stack, exprlist, stack.GetByOffset(-2).Copy());
							PushEx(stack, exprlist, stack.GetByOffset(-2).Copy());
						}
						break;
					}

					case opc_dup2_x1:
					{
						if (stack.GetByOffset(-1).GetExprType().stackSize == 2)
						{
							InsertByOffsetEx(-2, stack, exprlist, -1);
						}
						else
						{
							InsertByOffsetEx(-3, stack, exprlist, -2);
							InsertByOffsetEx(-3, stack, exprlist, -1);
						}
						break;
					}

					case opc_dup2_x2:
					{
						if (stack.GetByOffset(-1).GetExprType().stackSize == 2)
						{
							if (stack.GetByOffset(-2).GetExprType().stackSize == 2)
							{
								InsertByOffsetEx(-2, stack, exprlist, -1);
							}
							else
							{
								InsertByOffsetEx(-3, stack, exprlist, -1);
							}
						}
						else if (stack.GetByOffset(-3).GetExprType().stackSize == 2)
						{
							InsertByOffsetEx(-3, stack, exprlist, -2);
							InsertByOffsetEx(-3, stack, exprlist, -1);
						}
						else
						{
							InsertByOffsetEx(-4, stack, exprlist, -2);
							InsertByOffsetEx(-4, stack, exprlist, -1);
						}
						break;
					}

					case opc_swap:
					{
						InsertByOffsetEx(-2, stack, exprlist, -1);
						stack.Pop();
						break;
					}

					case opc_pop:
					{
						stack.Pop();
						break;
					}

					case opc_pop2:
					{
						if (stack.GetByOffset(-1).GetExprType().stackSize == 1)
						{
							// Since value at the top of the stack is a value of category 1 (JVMS9 2.11.1)
							// we should remove one more item from the stack.
							// See JVMS9 pop2 chapter.
							stack.Pop();
						}
						stack.Pop();
						break;
					}
				}
			}
		}

		private static int NextMeaningfulOffset(BasicBlock block, int index)
		{
			InstructionSequence seq = block.GetSeq();
			while (++index < seq.Length())
			{
				switch (seq.GetInstr(index).opcode)
				{
					case opc_nop:
					case opc_istore:
					case opc_lstore:
					case opc_fstore:
					case opc_dstore:
					case opc_astore:
					{
						continue;
					}
				}
				return block.GetOldOffset(index);
			}
			return -1;
		}

		private void PushEx(ExprentStack stack, List<Exprent> exprlist, Exprent exprent)
		{
			PushEx(stack, exprlist, exprent, null);
		}

		private void PushEx(ExprentStack stack, List<Exprent> exprlist, Exprent exprent, 
			VarType vartype)
		{
			int varindex = VarExprent.Stack_Base + stack.Count;
			VarExprent var = new VarExprent(varindex, vartype == null ? exprent.GetExprType()
				 : vartype, varProcessor);
			var.SetStack(true);
			exprlist.Add(new AssignmentExprent(var, exprent, null));
			stack.Push(var.Copy());
		}

		private void InsertByOffsetEx(int offset, ExprentStack stack, List<Exprent> exprlist
			, int copyoffset)
		{
			int @base = VarExprent.Stack_Base + stack.Count;
			LinkedList<VarExprent> lst = new LinkedList<VarExprent>();
			for (int i = -1; i >= offset; i--)
			{
				Exprent varex = stack.Pop();
				VarExprent varnew = new VarExprent(@base + i + 1, varex.GetExprType(), varProcessor
					);
				varnew.SetStack(true);
				exprlist.Add(new AssignmentExprent(varnew, varex, null));
				lst.AddFirst((VarExprent)varnew.Copy());
			}
			Exprent exprent = lst.ToList()[lst.Count + copyoffset].Copy();
			VarExprent var = new VarExprent(@base + offset, exprent.GetExprType(), varProcessor
				);
			var.SetStack(true);
			exprlist.Add(new AssignmentExprent(var, exprent, null));
			lst.AddFirst((VarExprent)var.Copy());
			foreach (VarExprent expr in lst)
			{
				stack.Push(expr);
			}
		}

		public static string GetTypeName(VarType type)
		{
			return GetTypeName(type, true);
		}

		public static string GetTypeName(VarType type, bool getShort)
		{
			int tp = type.type;
			if (tp <= ICodeConstants.Type_Boolean)
			{
				return typeNames[tp];
			}
			else if (tp == ICodeConstants.Type_Unknown)
			{
				return Unknown_Type_String;
			}
			else if (tp == ICodeConstants.Type_Null)
			{
				// INFO: should not occur
				return Null_Type_String;
			}
			else if (tp == ICodeConstants.Type_Void)
			{
				// INFO: should not occur
				return "void";
			}
			else if (tp == ICodeConstants.Type_Object)
			{
				string ret = BuildJavaClassName(type.value);
				if (getShort)
				{
					ret = DecompilerContext.GetImportCollector().GetShortName(ret);
				}
				if (ret == null)
				{
					// FIXME: a warning should be logged
					ret = Undefined_Type_String;
				}
				return ret;
			}
			throw new Exception("invalid type");
		}

		public static string GetCastTypeName(VarType type)
		{
			return GetCastTypeName(type, true);
		}

		public static string GetCastTypeName(VarType type, bool getShort)
		{
			StringBuilder s = new StringBuilder(GetTypeName(type, getShort));
			TextUtil.Append(s, "[]", type.arrayDim);
			return s.ToString();
		}

		public static PrimitiveExprsList GetExpressionData(VarExprent var)
		{
			PrimitiveExprsList prlst = new PrimitiveExprsList();
			VarExprent vartmp = new VarExprent(VarExprent.Stack_Base, var.GetExprType(), var.
				GetProcessor());
			vartmp.SetStack(true);
			prlst.GetLstExprents().Add(new AssignmentExprent(vartmp, var.Copy(), null));
			prlst.GetStack().Push(vartmp.Copy());
			return prlst;
		}

		public static bool EndsWithSemicolon(Exprent expr)
		{
			int type = expr.type;
			return !(type == Exprent.Exprent_Switch || type == Exprent.Exprent_Monitor || type
				 == Exprent.Exprent_If || (type == Exprent.Exprent_Var && ((VarExprent)expr).IsClassDef
				()));
		}

		private static void AddDeletedGotoInstructionMapping(Statement stat, BytecodeMappingTracer
			 tracer)
		{
			if (stat is BasicBlockStatement)
			{
				BasicBlock block = ((BasicBlockStatement)stat).GetBlock();
				List<int> offsets = block.GetInstrOldOffsets();
				if (!(offsets.Count == 0) && offsets.Count > block.GetSeq().Length())
				{
					// some instructions have been deleted, but we still have offsets
					tracer.AddMapping(offsets[offsets.Count - 1]);
				}
			}
		}

		// add the last offset
		public static TextBuffer JmpWrapper(Statement stat, int indent, bool semicolon, BytecodeMappingTracer
			 tracer)
		{
			TextBuffer buf = stat.ToJava(indent, tracer);
			List<StatEdge> lstSuccs = stat.GetSuccessorEdges(Statement.Statedge_Direct_All);
			if (lstSuccs.Count == 1)
			{
				StatEdge edge = lstSuccs[0];
				if (edge.GetType() != StatEdge.Type_Regular && edge.@explicit && edge.GetDestination
					().type != Statement.Type_Dummyexit)
				{
					buf.AppendIndent(indent);
					switch (edge.GetType())
					{
						case StatEdge.Type_Break:
						{
							AddDeletedGotoInstructionMapping(stat, tracer);
							buf.Append("break");
							break;
						}

						case StatEdge.Type_Continue:
						{
							AddDeletedGotoInstructionMapping(stat, tracer);
							buf.Append("continue");
							break;
						}
					}
					if (edge.labeled)
					{
						buf.Append(" label").Append(edge.closure.id.ToString());
					}
					buf.Append(";").AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
				}
			}
			if (buf.Length() == 0 && semicolon)
			{
				buf.AppendIndent(indent).Append(";").AppendLineSeparator();
				tracer.IncrementCurrentSourceLine();
			}
			return buf;
		}

		public static string BuildJavaClassName(string name)
		{
			string res = name.Replace('/', '.');
			if (res.Contains("$"))
			{
				// attempt to invoke foreign member
				// classes correctly
				StructClass cl = DecompilerContext.GetStructContext().GetClass(name);
				if (cl == null || !cl.IsOwn())
				{
					res = res.Replace('$', '.');
				}
			}
			return res;
		}

		public static TextBuffer ListToJava(List<Exprent> lst, int indent, BytecodeMappingTracer
			 tracer)
		{
			if (lst == null || (lst.Count == 0))
			{
				return new TextBuffer();
			}
			TextBuffer buf = new TextBuffer();
			foreach (var expr in lst)
			{
				if (buf.Length() > 0 && expr.type == Exprent.Exprent_Var && ((VarExprent)expr).IsClassDef
					())
				{
					// separates local class definition from previous statements
					buf.AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
				}
				TextBuffer content = expr.ToJava(indent, tracer);
				if (content.Length() > 0)
				{
					if (expr.type != Exprent.Exprent_Var || !((VarExprent)expr).IsClassDef())
					{
						buf.AppendIndent(indent);
					}
					buf.Append(content);
					if (expr.type == Exprent.Exprent_Monitor && ((MonitorExprent)expr).GetMonType() ==
						 MonitorExprent.Monitor_Enter)
					{
						buf.Append("{}");
					}
					// empty synchronized block
					if (EndsWithSemicolon(expr))
					{
						buf.Append(";");
					}
					buf.AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
				}
			}
			return buf;
		}

		public static ConstExprent GetDefaultArrayValue(VarType arrType)
		{
			ConstExprent defaultVal;
			if (arrType.type == ICodeConstants.Type_Object || arrType.arrayDim > 0)
			{
				defaultVal = new ConstExprent(VarType.Vartype_Null, null, null);
			}
			else if (arrType.type == ICodeConstants.Type_Float)
			{
				defaultVal = new ConstExprent(VarType.Vartype_Float, 0f, null);
			}
			else if (arrType.type == ICodeConstants.Type_Long)
			{
				defaultVal = new ConstExprent(VarType.Vartype_Long, 0L, null);
			}
			else if (arrType.type == ICodeConstants.Type_Double)
			{
				defaultVal = new ConstExprent(VarType.Vartype_Double, 0d, null);
			}
			else
			{
				// integer types
				defaultVal = new ConstExprent(0, true, null);
			}
			return defaultVal;
		}

		public static bool GetCastedExprent(Exprent exprent, VarType leftType, TextBuffer
			 buffer, int indent, bool castNull, BytecodeMappingTracer tracer)
		{
			return GetCastedExprent(exprent, leftType, buffer, indent, castNull, false, false
				, false, tracer);
		}

		public static bool GetCastedExprent(Exprent exprent, VarType leftType, TextBuffer
			 buffer, int indent, bool castNull, bool castAlways, bool castNarrowing, bool unbox
			, BytecodeMappingTracer tracer)
		{
			if (unbox)
			{
				// "unbox" invocation parameters, e.g. 'byteSet.add((byte)123)' or 'new ShortContainer((short)813)'
				if (exprent.type == Exprent.Exprent_Invocation && ((InvocationExprent)exprent).IsBoxingCall
					())
				{
					InvocationExprent invocationExprent = (InvocationExprent)exprent;
					exprent = invocationExprent.GetLstParameters()[0];
					int paramType = invocationExprent.GetDescriptor().@params[0].type;
					if (exprent.type == Exprent.Exprent_Const && ((ConstExprent)exprent).GetConstType
						().type != paramType)
					{
						leftType = new VarType(paramType);
					}
				}
			}
			VarType rightType = exprent.GetExprType();
			bool cast = castAlways || (!leftType.IsSuperset(rightType) && (rightType.Equals(VarType
				.Vartype_Object) || leftType.type != ICodeConstants.Type_Object)) || (castNull &&
				 rightType.type == ICodeConstants.Type_Null && !Undefined_Type_String.Equals(GetTypeName
				(leftType))) || (castNarrowing && IsIntConstant(exprent) && IsNarrowedIntType(leftType
				));
			bool quote = cast && exprent.GetPrecedence() >= FunctionExprent.GetPrecedence(FunctionExprent
				.Function_Cast);
			// cast instead to 'byte' / 'short' when int constant is used as a value for 'Byte' / 'Short'
			if (castNarrowing && exprent.type == Exprent.Exprent_Const && !((ConstExprent)exprent
				).IsNull())
			{
				if (leftType.Equals(VarType.Vartype_Byte_Obj))
				{
					leftType = VarType.Vartype_Byte;
				}
				else if (leftType.Equals(VarType.Vartype_Short_Obj))
				{
					leftType = VarType.Vartype_Short;
				}
			}
			if (cast)
			{
				buffer.Append('(').Append(GetCastTypeName(leftType)).Append(')');
			}
			if (quote)
			{
				buffer.Append('(');
			}
			if (exprent.type == Exprent.Exprent_Const)
			{
				((ConstExprent)exprent).AdjustConstType(leftType);
			}
			buffer.Append(exprent.ToJava(indent, tracer));
			if (quote)
			{
				buffer.Append(')');
			}
			return cast;
		}

		private static bool IsIntConstant(Exprent exprent)
		{
			if (exprent.type == Exprent.Exprent_Const)
			{
				switch (((ConstExprent)exprent).GetConstType().type)
				{
					case ICodeConstants.Type_Byte:
					case ICodeConstants.Type_Bytechar:
					case ICodeConstants.Type_Short:
					case ICodeConstants.Type_Shortchar:
					case ICodeConstants.Type_Int:
					{
						return true;
					}
				}
			}
			return false;
		}

		private static bool IsNarrowedIntType(VarType type)
		{
			return VarType.Vartype_Int.IsStrictSuperset(type) || type.Equals(VarType.Vartype_Byte_Obj
				) || type.Equals(VarType.Vartype_Short_Obj);
		}
	}
}
