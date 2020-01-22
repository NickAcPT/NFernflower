// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct
{
	public class StructMethod : StructMember
	{
		private static readonly int[] opr_iconst = new int[] { -1, 0, 1, 2, 3, 4, 5 };

		private static readonly int[] opr_loadstore = new int[] { 0, 1, 2, 3, 0, 1, 2, 3, 
			0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3 };

		private static readonly int[] opcs_load = new int[] { opc_iload, opc_lload, opc_fload
			, opc_dload, opc_aload };

		private static readonly int[] opcs_store = new int[] { opc_istore, opc_lstore, opc_fstore
			, opc_dstore, opc_astore };

		private readonly StructClass classStruct;

		private readonly string name;

		private readonly string descriptor;

		private bool containsCode__ = false;

		private int localVariables = 0;

		private int codeLength = 0;

		private int codeFullLength = 0;

		private InstructionSequence seq;

		private bool expanded = false;

		private IDictionary<string, StructGeneralAttribute> codeAttributes;

		/// <exception cref="System.IO.IOException"/>
		public StructMethod(DataInputFullStream @in, StructClass clStruct)
		{
			/*
			method_info {
			u2 access_flags;
			u2 name_index;
			u2 descriptor_index;
			u2 attributes_count;
			attribute_info attributes[attributes_count];
			}
			*/
			classStruct = clStruct;
			accessFlags = @in.ReadUnsignedShort();
			int nameIndex = @in.ReadUnsignedShort();
			int descriptorIndex = @in.ReadUnsignedShort();
			ConstantPool pool = clStruct.GetPool();
			string[] values = pool.GetClassElement(ConstantPool.Method, clStruct.qualifiedName
				, nameIndex, descriptorIndex);
			name = values[0];
			descriptor = values[1];
			attributes = ReadAttributes(@in, pool);
			if (codeAttributes != null)
			{
				Sharpen.Collections.PutAll(attributes, codeAttributes);
				codeAttributes = null;
			}
		}

		/// <exception cref="System.IO.IOException"/>
		protected internal override StructGeneralAttribute ReadAttribute(DataInputFullStream
			 @in, ConstantPool pool, string name)
		{
			if (StructGeneralAttribute.Attribute_Code.GetName().Equals(name))
			{
				if (!classStruct.IsOwn())
				{
					// skip code in foreign classes
					@in.Discard(8);
					@in.Discard(@in.ReadInt());
					@in.Discard(8 * @in.ReadUnsignedShort());
				}
				else
				{
					containsCode__ = true;
					@in.Discard(6);
					localVariables = @in.ReadUnsignedShort();
					codeLength = @in.ReadInt();
					@in.Discard(codeLength);
					int excLength = @in.ReadUnsignedShort();
					@in.Discard(excLength * 8);
					codeFullLength = codeLength + excLength * 8 + 2;
				}
				codeAttributes = ReadAttributes(@in, pool);
				return null;
			}
			return base.ReadAttribute(@in, pool, name);
		}

		/// <exception cref="System.IO.IOException"/>
		public virtual void ExpandData()
		{
			if (containsCode__ && !expanded)
			{
				byte[] code = classStruct.GetLoader().LoadBytecode(this, codeFullLength);
				seq = ParseBytecode(new DataInputFullStream(code), codeLength, classStruct.GetPool
					());
				expanded = true;
			}
		}

		public virtual void ReleaseResources()
		{
			if (containsCode__ && expanded)
			{
				seq = null;
				expanded = false;
			}
		}

		/// <exception cref="System.IO.IOException"/>
		private InstructionSequence ParseBytecode(DataInputFullStream @in, int length, ConstantPool
			 pool)
		{
			VBStyleCollection<Instruction, int> instructions = new VBStyleCollection<Instruction
				, int>();
			int bytecode_version = classStruct.GetBytecodeVersion();
			for (int i = 0; i < length; )
			{
				int offset = i;
				int opcode = @in.ReadUnsignedByte();
				int group = Group_General;
				bool wide = (opcode == opc_wide);
				if (wide)
				{
					i++;
					opcode = @in.ReadUnsignedByte();
				}
				List<int> operands = new List<int>();
				if (opcode >= opc_iconst_m1 && opcode <= opc_iconst_5)
				{
					operands.Add(opr_iconst[opcode - opc_iconst_m1]);
					opcode = opc_bipush;
				}
				else if (opcode >= opc_iload_0 && opcode <= opc_aload_3)
				{
					operands.Add(opr_loadstore[opcode - opc_iload_0]);
					opcode = opcs_load[(opcode - opc_iload_0) / 4];
				}
				else if (opcode >= opc_istore_0 && opcode <= opc_astore_3)
				{
					operands.Add(opr_loadstore[opcode - opc_istore_0]);
					opcode = opcs_store[(opcode - opc_istore_0) / 4];
				}
				else
				{
					switch (opcode)
					{
						case opc_bipush:
						{
							operands.Add((int)@in.ReadByte());
							i++;
							break;
						}

						case opc_ldc:
						case opc_newarray:
						{
							operands.Add(@in.ReadUnsignedByte());
							i++;
							break;
						}

						case opc_sipush:
						case opc_ifeq:
						case opc_ifne:
						case opc_iflt:
						case opc_ifge:
						case opc_ifgt:
						case opc_ifle:
						case opc_if_icmpeq:
						case opc_if_icmpne:
						case opc_if_icmplt:
						case opc_if_icmpge:
						case opc_if_icmpgt:
						case opc_if_icmple:
						case opc_if_acmpeq:
						case opc_if_acmpne:
						case opc_goto:
						case opc_jsr:
						case opc_ifnull:
						case opc_ifnonnull:
						{
							if (opcode != opc_sipush)
							{
								group = Group_Jump;
							}
							operands.Add((int)@in.ReadShort());
							i += 2;
							break;
						}

						case opc_ldc_w:
						case opc_ldc2_w:
						case opc_getstatic:
						case opc_putstatic:
						case opc_getfield:
						case opc_putfield:
						case opc_invokevirtual:
						case opc_invokespecial:
						case opc_invokestatic:
						case opc_new:
						case opc_anewarray:
						case opc_checkcast:
						case opc_instanceof:
						{
							operands.Add(@in.ReadUnsignedShort());
							i += 2;
							if (opcode >= opc_getstatic && opcode <= opc_putfield)
							{
								group = Group_Fieldaccess;
							}
							else if (opcode >= opc_invokevirtual && opcode <= opc_invokestatic)
							{
								group = Group_Invocation;
							}
							break;
						}

						case opc_invokedynamic:
						{
							if (classStruct.IsVersionGE_1_7())
							{
								// instruction unused in Java 6 and before
								operands.Add(@in.ReadUnsignedShort());
								@in.Discard(2);
								group = Group_Invocation;
								i += 4;
							}
							break;
						}

						case opc_iload:
						case opc_lload:
						case opc_fload:
						case opc_dload:
						case opc_aload:
						case opc_istore:
						case opc_lstore:
						case opc_fstore:
						case opc_dstore:
						case opc_astore:
						case opc_ret:
						{
							if (wide)
							{
								operands.Add(@in.ReadUnsignedShort());
								i += 2;
							}
							else
							{
								operands.Add(@in.ReadUnsignedByte());
								i++;
							}
							if (opcode == opc_ret)
							{
								group = Group_Return;
							}
							break;
						}

						case opc_iinc:
						{
							if (wide)
							{
								operands.Add(@in.ReadUnsignedShort());
								operands.Add((int)@in.ReadShort());
								i += 4;
							}
							else
							{
								operands.Add(@in.ReadUnsignedByte());
								operands.Add((int)@in.ReadByte());
								i += 2;
							}
							break;
						}

						case opc_goto_w:
						case opc_jsr_w:
						{
							opcode = opcode == opc_jsr_w ? opc_jsr : opc_goto;
							operands.Add(@in.ReadInt());
							group = Group_Jump;
							i += 4;
							break;
						}

						case opc_invokeinterface:
						{
							operands.Add(@in.ReadUnsignedShort());
							operands.Add(@in.ReadUnsignedByte());
							@in.Discard(1);
							group = Group_Invocation;
							i += 4;
							break;
						}

						case opc_multianewarray:
						{
							operands.Add(@in.ReadUnsignedShort());
							operands.Add(@in.ReadUnsignedByte());
							i += 3;
							break;
						}

						case opc_tableswitch:
						{
							@in.Discard((4 - (i + 1) % 4) % 4);
							i += ((4 - (i + 1) % 4) % 4);
							// padding
							operands.Add(@in.ReadInt());
							i += 4;
							int low = @in.ReadInt();
							operands.Add(low);
							i += 4;
							int high = @in.ReadInt();
							operands.Add(high);
							i += 4;
							for (int j = 0; j < high - low + 1; j++)
							{
								operands.Add(@in.ReadInt());
								i += 4;
							}
							group = Group_Switch;
							break;
						}

						case opc_lookupswitch:
						{
							@in.Discard((4 - (i + 1) % 4) % 4);
							i += ((4 - (i + 1) % 4) % 4);
							// padding
							operands.Add(@in.ReadInt());
							i += 4;
							int npairs = @in.ReadInt();
							operands.Add(npairs);
							i += 4;
							for (int j = 0; j < npairs; j++)
							{
								operands.Add(@in.ReadInt());
								i += 4;
								operands.Add(@in.ReadInt());
								i += 4;
							}
							group = Group_Switch;
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
							group = Group_Return;
							break;
						}
					}
				}
				int[] ops = null;
				if (!(operands.Count == 0))
				{
					ops = new int[operands.Count];
					for (int j = 0; j < operands.Count; j++)
					{
						ops[j] = operands[j];
					}
				}
				Instruction instr = Instruction.Create(opcode, wide, group, bytecode_version, ops
					);
				instructions.AddWithKey(instr, offset);
				i++;
			}
			// initialize exception table
			List<ExceptionHandler> lstHandlers = new List<ExceptionHandler>();
			int exception_count = @in.ReadUnsignedShort();
			for (int i = 0; i < exception_count; i++)
			{
				ExceptionHandler handler = new ExceptionHandler();
				handler.from = @in.ReadUnsignedShort();
				handler.to = @in.ReadUnsignedShort();
				handler.handler = @in.ReadUnsignedShort();
				int excclass = @in.ReadUnsignedShort();
				if (excclass != 0)
				{
					handler.exceptionClass = pool.GetPrimitiveConstant(excclass).GetString();
				}
				lstHandlers.Add(handler);
			}
			InstructionSequence seq = new FullInstructionSequence(instructions, new ExceptionTable
				(lstHandlers));
			// initialize instructions
			int i_1 = seq.Length() - 1;
			seq.SetPointer(i_1);
			while (i_1 >= 0)
			{
				Instruction instr = seq.GetInstr(i_1--);
				if (instr.group != Group_General)
				{
					instr.InitInstruction(seq);
				}
				seq.AddToPointer(-1);
			}
			return seq;
		}

		public virtual StructClass GetClassStruct()
		{
			return classStruct;
		}

		public virtual string GetName()
		{
			return name;
		}

		public virtual string GetDescriptor()
		{
			return descriptor;
		}

		public virtual bool ContainsCode()
		{
			return containsCode__;
		}

		public virtual int GetLocalVariables()
		{
			return localVariables;
		}

		public virtual InstructionSequence GetInstructionSequence()
		{
			return seq;
		}

		public virtual StructLocalVariableTableAttribute GetLocalVariableAttr()
		{
			return GetAttribute(StructGeneralAttribute.Attribute_Local_Variable_Table);
		}

		public override string ToString()
		{
			return name;
		}
	}
}
