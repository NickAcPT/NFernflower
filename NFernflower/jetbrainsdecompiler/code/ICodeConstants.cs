// Copyright 2000-2019 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Code
{
	public abstract class ICodeConstants
	{
		public const int Bytecode_Java_Le_4 = 48;

		public const int Bytecode_Java_5 = 49;

		public const int Bytecode_Java_6 = 50;

		public const int Bytecode_Java_7 = 51;

		public const int Bytecode_Java_8 = 52;

		public const int Bytecode_Java_9 = 53;

		public const int Bytecode_Java_10 = 54;

		public const int Bytecode_Java_11 = 55;

		public const int Bytecode_Java_12 = 56;

		public const int Bytecode_Java_13 = 57;

		public const int Type_Byte = 0;

		public const int Type_Char = 1;

		public const int Type_Double = 2;

		public const int Type_Float = 3;

		public const int Type_Int = 4;

		public const int Type_Long = 5;

		public const int Type_Short = 6;

		public const int Type_Boolean = 7;

		public const int Type_Object = 8;

		public const int Type_Address = 9;

		public const int Type_Void = 10;

		public const int Type_Any = 11;

		public const int Type_Group2empty = 12;

		public const int Type_Null = 13;

		public const int Type_Notinitialized = 14;

		public const int Type_Bytechar = 15;

		public const int Type_Shortchar = 16;

		public const int Type_Unknown = 17;

		public const int Type_Genvar = 18;

		public const int Type_Family_Unknown = 0;

		public const int Type_Family_Boolean = 1;

		public const int Type_Family_Integer = 2;

		public const int Type_Family_Float = 3;

		public const int Type_Family_Long = 4;

		public const int Type_Family_Double = 5;

		public const int Type_Family_Object = 6;

		public const int Acc_Public = unchecked((int)(0x0001));

		public const int Acc_Private = unchecked((int)(0x0002));

		public const int Acc_Protected = unchecked((int)(0x0004));

		public const int Acc_Static = unchecked((int)(0x0008));

		public const int Acc_Final = unchecked((int)(0x0010));

		public const int Acc_Synchronized = unchecked((int)(0x0020));

		public const int Acc_Native = unchecked((int)(0x0100));

		public const int Acc_Abstract = unchecked((int)(0x0400));

		public const int Acc_Strict = unchecked((int)(0x0800));

		public const int Acc_Volatile = unchecked((int)(0x0040));

		public const int Acc_Bridge = unchecked((int)(0x0040));

		public const int Acc_Transient = unchecked((int)(0x0080));

		public const int Acc_Varargs = unchecked((int)(0x0080));

		public const int Acc_Synthetic = unchecked((int)(0x1000));

		public const int Acc_Annotation = unchecked((int)(0x2000));

		public const int Acc_Enum = unchecked((int)(0x4000));

		public const int Acc_Mandated = unchecked((int)(0x8000));

		public const int Acc_Super = unchecked((int)(0x0020));

		public const int Acc_Interface = unchecked((int)(0x0200));

		public const int Group_General = 1;

		public const int Group_Jump = 2;

		public const int Group_Switch = 3;

		public const int Group_Invocation = 4;

		public const int Group_Fieldaccess = 5;

		public const int Group_Return = 6;

		public const int CONSTANT_Utf8 = 1;

		public const int CONSTANT_Integer = 3;

		public const int CONSTANT_Float = 4;

		public const int CONSTANT_Long = 5;

		public const int CONSTANT_Double = 6;

		public const int CONSTANT_Class = 7;

		public const int CONSTANT_String = 8;

		public const int CONSTANT_Fieldref = 9;

		public const int CONSTANT_Methodref = 10;

		public const int CONSTANT_InterfaceMethodref = 11;

		public const int CONSTANT_NameAndType = 12;

		public const int CONSTANT_MethodHandle = 15;

		public const int CONSTANT_MethodType = 16;

		public const int CONSTANT_InvokeDynamic = 18;

		public const int CONSTANT_MethodHandle_REF_getField = 1;

		public const int CONSTANT_MethodHandle_REF_getStatic = 2;

		public const int CONSTANT_MethodHandle_REF_putField = 3;

		public const int CONSTANT_MethodHandle_REF_putStatic = 4;

		public const int CONSTANT_MethodHandle_REF_invokeVirtual = 5;

		public const int CONSTANT_MethodHandle_REF_invokeStatic = 6;

		public const int CONSTANT_MethodHandle_REF_invokeSpecial = 7;

		public const int CONSTANT_MethodHandle_REF_newInvokeSpecial = 8;

		public const int CONSTANT_MethodHandle_REF_invokeInterface = 9;

		public const int opc_nop = 0;

		public const int opc_aconst_null = 1;

		public const int opc_iconst_m1 = 2;

		public const int opc_iconst_0 = 3;

		public const int opc_iconst_1 = 4;

		public const int opc_iconst_2 = 5;

		public const int opc_iconst_3 = 6;

		public const int opc_iconst_4 = 7;

		public const int opc_iconst_5 = 8;

		public const int opc_lconst_0 = 9;

		public const int opc_lconst_1 = 10;

		public const int opc_fconst_0 = 11;

		public const int opc_fconst_1 = 12;

		public const int opc_fconst_2 = 13;

		public const int opc_dconst_0 = 14;

		public const int opc_dconst_1 = 15;

		public const int opc_bipush = 16;

		public const int opc_sipush = 17;

		public const int opc_ldc = 18;

		public const int opc_ldc_w = 19;

		public const int opc_ldc2_w = 20;

		public const int opc_iload = 21;

		public const int opc_lload = 22;

		public const int opc_fload = 23;

		public const int opc_dload = 24;

		public const int opc_aload = 25;

		public const int opc_iload_0 = 26;

		public const int opc_iload_1 = 27;

		public const int opc_iload_2 = 28;

		public const int opc_iload_3 = 29;

		public const int opc_lload_0 = 30;

		public const int opc_lload_1 = 31;

		public const int opc_lload_2 = 32;

		public const int opc_lload_3 = 33;

		public const int opc_fload_0 = 34;

		public const int opc_fload_1 = 35;

		public const int opc_fload_2 = 36;

		public const int opc_fload_3 = 37;

		public const int opc_dload_0 = 38;

		public const int opc_dload_1 = 39;

		public const int opc_dload_2 = 40;

		public const int opc_dload_3 = 41;

		public const int opc_aload_0 = 42;

		public const int opc_aload_1 = 43;

		public const int opc_aload_2 = 44;

		public const int opc_aload_3 = 45;

		public const int opc_iaload = 46;

		public const int opc_laload = 47;

		public const int opc_faload = 48;

		public const int opc_daload = 49;

		public const int opc_aaload = 50;

		public const int opc_baload = 51;

		public const int opc_caload = 52;

		public const int opc_saload = 53;

		public const int opc_istore = 54;

		public const int opc_lstore = 55;

		public const int opc_fstore = 56;

		public const int opc_dstore = 57;

		public const int opc_astore = 58;

		public const int opc_istore_0 = 59;

		public const int opc_istore_1 = 60;

		public const int opc_istore_2 = 61;

		public const int opc_istore_3 = 62;

		public const int opc_lstore_0 = 63;

		public const int opc_lstore_1 = 64;

		public const int opc_lstore_2 = 65;

		public const int opc_lstore_3 = 66;

		public const int opc_fstore_0 = 67;

		public const int opc_fstore_1 = 68;

		public const int opc_fstore_2 = 69;

		public const int opc_fstore_3 = 70;

		public const int opc_dstore_0 = 71;

		public const int opc_dstore_1 = 72;

		public const int opc_dstore_2 = 73;

		public const int opc_dstore_3 = 74;

		public const int opc_astore_0 = 75;

		public const int opc_astore_1 = 76;

		public const int opc_astore_2 = 77;

		public const int opc_astore_3 = 78;

		public const int opc_iastore = 79;

		public const int opc_lastore = 80;

		public const int opc_fastore = 81;

		public const int opc_dastore = 82;

		public const int opc_aastore = 83;

		public const int opc_bastore = 84;

		public const int opc_castore = 85;

		public const int opc_sastore = 86;

		public const int opc_pop = 87;

		public const int opc_pop2 = 88;

		public const int opc_dup = 89;

		public const int opc_dup_x1 = 90;

		public const int opc_dup_x2 = 91;

		public const int opc_dup2 = 92;

		public const int opc_dup2_x1 = 93;

		public const int opc_dup2_x2 = 94;

		public const int opc_swap = 95;

		public const int opc_iadd = 96;

		public const int opc_ladd = 97;

		public const int opc_fadd = 98;

		public const int opc_dadd = 99;

		public const int opc_isub = 100;

		public const int opc_lsub = 101;

		public const int opc_fsub = 102;

		public const int opc_dsub = 103;

		public const int opc_imul = 104;

		public const int opc_lmul = 105;

		public const int opc_fmul = 106;

		public const int opc_dmul = 107;

		public const int opc_idiv = 108;

		public const int opc_ldiv = 109;

		public const int opc_fdiv = 110;

		public const int opc_ddiv = 111;

		public const int opc_irem = 112;

		public const int opc_lrem = 113;

		public const int opc_frem = 114;

		public const int opc_drem = 115;

		public const int opc_ineg = 116;

		public const int opc_lneg = 117;

		public const int opc_fneg = 118;

		public const int opc_dneg = 119;

		public const int opc_ishl = 120;

		public const int opc_lshl = 121;

		public const int opc_ishr = 122;

		public const int opc_lshr = 123;

		public const int opc_iushr = 124;

		public const int opc_lushr = 125;

		public const int opc_iand = 126;

		public const int opc_land = 127;

		public const int opc_ior = 128;

		public const int opc_lor = 129;

		public const int opc_ixor = 130;

		public const int opc_lxor = 131;

		public const int opc_iinc = 132;

		public const int opc_i2l = 133;

		public const int opc_i2f = 134;

		public const int opc_i2d = 135;

		public const int opc_l2i = 136;

		public const int opc_l2f = 137;

		public const int opc_l2d = 138;

		public const int opc_f2i = 139;

		public const int opc_f2l = 140;

		public const int opc_f2d = 141;

		public const int opc_d2i = 142;

		public const int opc_d2l = 143;

		public const int opc_d2f = 144;

		public const int opc_i2b = 145;

		public const int opc_i2c = 146;

		public const int opc_i2s = 147;

		public const int opc_lcmp = 148;

		public const int opc_fcmpl = 149;

		public const int opc_fcmpg = 150;

		public const int opc_dcmpl = 151;

		public const int opc_dcmpg = 152;

		public const int opc_ifeq = 153;

		public const int opc_ifne = 154;

		public const int opc_iflt = 155;

		public const int opc_ifge = 156;

		public const int opc_ifgt = 157;

		public const int opc_ifle = 158;

		public const int opc_if_icmpeq = 159;

		public const int opc_if_icmpne = 160;

		public const int opc_if_icmplt = 161;

		public const int opc_if_icmpge = 162;

		public const int opc_if_icmpgt = 163;

		public const int opc_if_icmple = 164;

		public const int opc_if_acmpeq = 165;

		public const int opc_if_acmpne = 166;

		public const int opc_goto = 167;

		public const int opc_jsr = 168;

		public const int opc_ret = 169;

		public const int opc_tableswitch = 170;

		public const int opc_lookupswitch = 171;

		public const int opc_ireturn = 172;

		public const int opc_lreturn = 173;

		public const int opc_freturn = 174;

		public const int opc_dreturn = 175;

		public const int opc_areturn = 176;

		public const int opc_return = 177;

		public const int opc_getstatic = 178;

		public const int opc_putstatic = 179;

		public const int opc_getfield = 180;

		public const int opc_putfield = 181;

		public const int opc_invokevirtual = 182;

		public const int opc_invokespecial = 183;

		public const int opc_invokestatic = 184;

		public const int opc_invokeinterface = 185;

		public const int opc_invokedynamic = 186;

		public const int opc_new = 187;

		public const int opc_newarray = 188;

		public const int opc_anewarray = 189;

		public const int opc_arraylength = 190;

		public const int opc_athrow = 191;

		public const int opc_checkcast = 192;

		public const int opc_instanceof = 193;

		public const int opc_monitorenter = 194;

		public const int opc_monitorexit = 195;

		public const int opc_wide = 196;

		public const int opc_multianewarray = 197;

		public const int opc_ifnull = 198;

		public const int opc_ifnonnull = 199;

		public const int opc_goto_w = 200;

		public const int opc_jsr_w = 201;

		public const string Clinit_Name = "<clinit>";

		public const string Init_Name = "<init>";
		// ----------------------------------------------------------------------
		// BYTECODE VERSIONS
		// ----------------------------------------------------------------------
		// ----------------------------------------------------------------------
		// VARIABLE TYPES
		// ----------------------------------------------------------------------
		// ----------------------------------------------------------------------
		// VARIABLE TYPE FAMILIES
		// ----------------------------------------------------------------------
		// ----------------------------------------------------------------------
		// ACCESS FLAGS
		// ----------------------------------------------------------------------
		// ----------------------------------------------------------------------
		// CLASS FLAGS
		// ----------------------------------------------------------------------
		// ----------------------------------------------------------------------
		// INSTRUCTION GROUPS
		// ----------------------------------------------------------------------
		// ----------------------------------------------------------------------
		// POOL CONSTANTS
		// ----------------------------------------------------------------------
		// ----------------------------------------------------------------------
		// MethodHandle reference_kind values
		// ----------------------------------------------------------------------
		// ----------------------------------------------------------------------
		// VM OPCODES
		// ----------------------------------------------------------------------
	}

	public static class CodeConstantsConstants
	{
	}
}
