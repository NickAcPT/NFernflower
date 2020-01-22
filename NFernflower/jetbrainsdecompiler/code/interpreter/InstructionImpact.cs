// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Code.Interpreter
{
	public class InstructionImpact
	{
		private static readonly int[][][] stack_impact = new int[][][] { new int[][] { null
			, null }, null, null, null, null, null, null, null, null, new int[][] { null, new 
			int[] { ICodeConstants.Type_Long } }, new int[][] { null, new int[] { ICodeConstants
			.Type_Long } }, new int[][] { null, new int[] { ICodeConstants.Type_Float } }, new 
			int[][] { null, new int[] { ICodeConstants.Type_Float } }, new int[][] { null, new 
			int[] { ICodeConstants.Type_Float } }, new int[][] { null, new int[] { ICodeConstants
			.Type_Double } }, new int[][] { null, new int[] { ICodeConstants.Type_Double } }
			, new int[][] { null, new int[] { ICodeConstants.Type_Int } }, new int[][] { null
			, new int[] { ICodeConstants.Type_Int } }, null, null, null, new int[][] { null, 
			new int[] { ICodeConstants.Type_Int } }, new int[][] { null, new int[] { ICodeConstants
			.Type_Long } }, new int[][] { null, new int[] { ICodeConstants.Type_Float } }, new 
			int[][] { null, new int[] { ICodeConstants.Type_Double } }, null, null, null, null
			, null, null, null, null, null, null, null, null, null, null, null, null, null, 
			null, null, null, null, new int[][] { new int[] { ICodeConstants.Type_Object, ICodeConstants
			.Type_Int }, new int[] { ICodeConstants.Type_Int } }, new int[][] { new int[] { 
			ICodeConstants.Type_Object, ICodeConstants.Type_Int }, new int[] { ICodeConstants
			.Type_Long } }, new int[][] { new int[] { ICodeConstants.Type_Object, ICodeConstants
			.Type_Int }, new int[] { ICodeConstants.Type_Float } }, new int[][] { new int[] 
			{ ICodeConstants.Type_Object, ICodeConstants.Type_Int }, new int[] { ICodeConstants
			.Type_Double } }, null, new int[][] { new int[] { ICodeConstants.Type_Object, ICodeConstants
			.Type_Int }, new int[] { ICodeConstants.Type_Int } }, new int[][] { new int[] { 
			ICodeConstants.Type_Object, ICodeConstants.Type_Int }, new int[] { ICodeConstants
			.Type_Int } }, new int[][] { new int[] { ICodeConstants.Type_Object, ICodeConstants
			.Type_Int }, new int[] { ICodeConstants.Type_Int } }, new int[][] { new int[] { 
			ICodeConstants.Type_Int }, null }, new int[][] { new int[] { ICodeConstants.Type_Long
			 }, null }, new int[][] { new int[] { ICodeConstants.Type_Float }, null }, new int
			[][] { new int[] { ICodeConstants.Type_Double }, null }, null, null, null, null, 
			null, null, null, null, null, null, null, null, null, null, null, null, null, null
			, null, null, null, new int[][] { new int[] { ICodeConstants.Type_Object, ICodeConstants
			.Type_Int, ICodeConstants.Type_Int }, null }, new int[][] { new int[] { ICodeConstants
			.Type_Object, ICodeConstants.Type_Int, ICodeConstants.Type_Long }, null }, new int
			[][] { new int[] { ICodeConstants.Type_Object, ICodeConstants.Type_Int, ICodeConstants
			.Type_Float }, null }, new int[][] { new int[] { ICodeConstants.Type_Object, ICodeConstants
			.Type_Int, ICodeConstants.Type_Double }, null }, new int[][] { new int[] { ICodeConstants
			.Type_Object, ICodeConstants.Type_Int, ICodeConstants.Type_Object }, null }, new 
			int[][] { new int[] { ICodeConstants.Type_Object, ICodeConstants.Type_Int, ICodeConstants
			.Type_Int }, null }, new int[][] { new int[] { ICodeConstants.Type_Object, ICodeConstants
			.Type_Int, ICodeConstants.Type_Int }, null }, new int[][] { new int[] { ICodeConstants
			.Type_Object, ICodeConstants.Type_Int, ICodeConstants.Type_Int }, null }, new int
			[][] { new int[] { ICodeConstants.Type_Any }, null }, new int[][] { new int[] { 
			ICodeConstants.Type_Any, ICodeConstants.Type_Any }, null }, null, null, null, null
			, null, null, null, new int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants
			.Type_Int }, new int[] { ICodeConstants.Type_Int } }, new int[][] { new int[] { 
			ICodeConstants.Type_Long, ICodeConstants.Type_Long }, new int[] { ICodeConstants
			.Type_Long } }, new int[][] { new int[] { ICodeConstants.Type_Float, ICodeConstants
			.Type_Float }, new int[] { ICodeConstants.Type_Float } }, new int[][] { new int[
			] { ICodeConstants.Type_Double, ICodeConstants.Type_Double }, new int[] { ICodeConstants
			.Type_Double } }, new int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants
			.Type_Int }, new int[] { ICodeConstants.Type_Int } }, new int[][] { new int[] { 
			ICodeConstants.Type_Long, ICodeConstants.Type_Long }, new int[] { ICodeConstants
			.Type_Long } }, new int[][] { new int[] { ICodeConstants.Type_Float, ICodeConstants
			.Type_Float }, new int[] { ICodeConstants.Type_Float } }, new int[][] { new int[
			] { ICodeConstants.Type_Double, ICodeConstants.Type_Double }, new int[] { ICodeConstants
			.Type_Double } }, new int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants
			.Type_Int }, new int[] { ICodeConstants.Type_Int } }, new int[][] { new int[] { 
			ICodeConstants.Type_Long, ICodeConstants.Type_Long }, new int[] { ICodeConstants
			.Type_Long } }, new int[][] { new int[] { ICodeConstants.Type_Float, ICodeConstants
			.Type_Float }, new int[] { ICodeConstants.Type_Float } }, new int[][] { new int[
			] { ICodeConstants.Type_Double, ICodeConstants.Type_Double }, new int[] { ICodeConstants
			.Type_Double } }, new int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants
			.Type_Int }, new int[] { ICodeConstants.Type_Int } }, new int[][] { new int[] { 
			ICodeConstants.Type_Long, ICodeConstants.Type_Long }, new int[] { ICodeConstants
			.Type_Long } }, new int[][] { new int[] { ICodeConstants.Type_Float, ICodeConstants
			.Type_Float }, new int[] { ICodeConstants.Type_Float } }, new int[][] { new int[
			] { ICodeConstants.Type_Double, ICodeConstants.Type_Double }, new int[] { ICodeConstants
			.Type_Double } }, new int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants
			.Type_Int }, new int[] { ICodeConstants.Type_Int } }, new int[][] { new int[] { 
			ICodeConstants.Type_Long, ICodeConstants.Type_Long }, new int[] { ICodeConstants
			.Type_Long } }, new int[][] { new int[] { ICodeConstants.Type_Float, ICodeConstants
			.Type_Float }, new int[] { ICodeConstants.Type_Float } }, new int[][] { new int[
			] { ICodeConstants.Type_Double, ICodeConstants.Type_Double }, new int[] { ICodeConstants
			.Type_Double } }, new int[][] { new int[] { ICodeConstants.Type_Int }, new int[]
			 { ICodeConstants.Type_Int } }, new int[][] { new int[] { ICodeConstants.Type_Long
			 }, new int[] { ICodeConstants.Type_Long } }, new int[][] { new int[] { ICodeConstants
			.Type_Float }, new int[] { ICodeConstants.Type_Float } }, new int[][] { new int[
			] { ICodeConstants.Type_Double }, new int[] { ICodeConstants.Type_Double } }, new 
			int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants.Type_Int }, new int
			[] { ICodeConstants.Type_Int } }, new int[][] { new int[] { ICodeConstants.Type_Long
			, ICodeConstants.Type_Int }, new int[] { ICodeConstants.Type_Long } }, new int[]
			[] { new int[] { ICodeConstants.Type_Int, ICodeConstants.Type_Int }, new int[] { 
			ICodeConstants.Type_Int } }, new int[][] { new int[] { ICodeConstants.Type_Long, 
			ICodeConstants.Type_Int }, new int[] { ICodeConstants.Type_Long } }, new int[][]
			 { new int[] { ICodeConstants.Type_Int, ICodeConstants.Type_Int }, new int[] { ICodeConstants
			.Type_Int } }, new int[][] { new int[] { ICodeConstants.Type_Long, ICodeConstants
			.Type_Int }, new int[] { ICodeConstants.Type_Long } }, new int[][] { new int[] { 
			ICodeConstants.Type_Int, ICodeConstants.Type_Int }, new int[] { ICodeConstants.Type_Int
			 } }, new int[][] { new int[] { ICodeConstants.Type_Long, ICodeConstants.Type_Long
			 }, new int[] { ICodeConstants.Type_Long } }, new int[][] { new int[] { ICodeConstants
			.Type_Int, ICodeConstants.Type_Int }, new int[] { ICodeConstants.Type_Int } }, new 
			int[][] { new int[] { ICodeConstants.Type_Long, ICodeConstants.Type_Long }, new 
			int[] { ICodeConstants.Type_Long } }, new int[][] { new int[] { ICodeConstants.Type_Int
			, ICodeConstants.Type_Int }, new int[] { ICodeConstants.Type_Int } }, new int[][]
			 { new int[] { ICodeConstants.Type_Long, ICodeConstants.Type_Long }, new int[] { 
			ICodeConstants.Type_Long } }, new int[][] { null, null }, new int[][] { new int[
			] { ICodeConstants.Type_Int }, new int[] { ICodeConstants.Type_Long } }, new int
			[][] { new int[] { ICodeConstants.Type_Int }, new int[] { ICodeConstants.Type_Float
			 } }, new int[][] { new int[] { ICodeConstants.Type_Int }, new int[] { ICodeConstants
			.Type_Double } }, new int[][] { new int[] { ICodeConstants.Type_Long }, new int[
			] { ICodeConstants.Type_Int } }, new int[][] { new int[] { ICodeConstants.Type_Long
			 }, new int[] { ICodeConstants.Type_Float } }, new int[][] { new int[] { ICodeConstants
			.Type_Long }, new int[] { ICodeConstants.Type_Double } }, new int[][] { new int[
			] { ICodeConstants.Type_Float }, new int[] { ICodeConstants.Type_Int } }, new int
			[][] { new int[] { ICodeConstants.Type_Float }, new int[] { ICodeConstants.Type_Long
			 } }, new int[][] { new int[] { ICodeConstants.Type_Float }, new int[] { ICodeConstants
			.Type_Double } }, new int[][] { new int[] { ICodeConstants.Type_Double }, new int
			[] { ICodeConstants.Type_Int } }, new int[][] { new int[] { ICodeConstants.Type_Double
			 }, new int[] { ICodeConstants.Type_Long } }, new int[][] { new int[] { ICodeConstants
			.Type_Double }, new int[] { ICodeConstants.Type_Float } }, new int[][] { new int
			[] { ICodeConstants.Type_Int }, new int[] { ICodeConstants.Type_Int } }, new int
			[][] { new int[] { ICodeConstants.Type_Int }, new int[] { ICodeConstants.Type_Int
			 } }, new int[][] { new int[] { ICodeConstants.Type_Int }, new int[] { ICodeConstants
			.Type_Int } }, new int[][] { new int[] { ICodeConstants.Type_Long, ICodeConstants
			.Type_Long }, new int[] { ICodeConstants.Type_Int } }, new int[][] { new int[] { 
			ICodeConstants.Type_Float, ICodeConstants.Type_Float }, new int[] { ICodeConstants
			.Type_Int } }, new int[][] { new int[] { ICodeConstants.Type_Float, ICodeConstants
			.Type_Float }, new int[] { ICodeConstants.Type_Int } }, new int[][] { new int[] 
			{ ICodeConstants.Type_Double, ICodeConstants.Type_Double }, new int[] { ICodeConstants
			.Type_Int } }, new int[][] { new int[] { ICodeConstants.Type_Double, ICodeConstants
			.Type_Double }, new int[] { ICodeConstants.Type_Int } }, new int[][] { new int[]
			 { ICodeConstants.Type_Int }, null }, new int[][] { new int[] { ICodeConstants.Type_Int
			 }, null }, new int[][] { new int[] { ICodeConstants.Type_Int }, null }, new int
			[][] { new int[] { ICodeConstants.Type_Int }, null }, new int[][] { new int[] { 
			ICodeConstants.Type_Int }, null }, new int[][] { new int[] { ICodeConstants.Type_Int
			 }, null }, new int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants.Type_Int
			 }, null }, new int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants.Type_Int
			 }, null }, new int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants.Type_Int
			 }, null }, new int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants.Type_Int
			 }, null }, new int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants.Type_Int
			 }, null }, new int[][] { new int[] { ICodeConstants.Type_Int, ICodeConstants.Type_Int
			 }, null }, new int[][] { new int[] { ICodeConstants.Type_Object, ICodeConstants
			.Type_Object }, null }, new int[][] { new int[] { ICodeConstants.Type_Object, ICodeConstants
			.Type_Object }, null }, new int[][] { null, null }, new int[][] { null, new int[
			] { ICodeConstants.Type_Address } }, new int[][] { null, null }, new int[][] { new 
			int[] { ICodeConstants.Type_Int }, null }, new int[][] { new int[] { ICodeConstants
			.Type_Int }, null }, new int[][] { new int[] { ICodeConstants.Type_Int }, null }
			, new int[][] { new int[] { ICodeConstants.Type_Long }, null }, new int[][] { new 
			int[] { ICodeConstants.Type_Float }, null }, new int[][] { new int[] { ICodeConstants
			.Type_Double }, null }, new int[][] { new int[] { ICodeConstants.Type_Object }, 
			null }, new int[][] { null, null }, null, null, null, null, null, null, null, null
			, null, null, null, null, new int[][] { new int[] { ICodeConstants.Type_Object }
			, new int[] { ICodeConstants.Type_Int } }, null, null, null, new int[][] { new int
			[] { ICodeConstants.Type_Object }, null }, new int[][] { new int[] { ICodeConstants
			.Type_Object }, null }, null, null, new int[][] { new int[] { ICodeConstants.Type_Object
			 }, null }, new int[][] { new int[] { ICodeConstants.Type_Object }, null }, new 
			int[][] { null, null }, new int[][] { null, new int[] { ICodeConstants.Type_Address
			 } } };

		private static readonly int[] arr_type = new int[] { ICodeConstants.Type_Boolean, 
			ICodeConstants.Type_Char, ICodeConstants.Type_Float, ICodeConstants.Type_Double, 
			ICodeConstants.Type_Byte, ICodeConstants.Type_Short, ICodeConstants.Type_Int, ICodeConstants
			.Type_Long };

		// {read, write}
		//		public final static int		opc_nop = 0;
		//		public final static int		opc_aconst_null = 1;
		//		public final static int		opc_iconst_m1 = 2;
		//		public final static int		opc_iconst_0 = 3;
		//		public final static int		opc_iconst_1 = 4;
		//		public final static int		opc_iconst_2 = 5;
		//		public final static int		opc_iconst_3 = 6;
		//		public final static int		opc_iconst_4 = 7;
		//		public final static int		opc_iconst_5 = 8;
		//		public final static int		opc_lconst_0 = 9;
		//		public final static int		opc_lconst_1 = 10;
		//		public final static int		opc_fconst_0 = 11;
		//		public final static int		opc_fconst_1 = 12;
		//		public final static int		opc_fconst_2 = 13;
		//		public final static int		opc_dconst_0 = 14;
		//		public final static int		opc_dconst_1 = 15;
		//		public final static int		opc_bipush = 16;
		//		public final static int		opc_sipush = 17;
		//		public final static int		opc_ldc = 18;
		//		public final static int		opc_ldc_w = 19;
		//		public final static int		opc_ldc2_w = 20;
		//		public final static int		opc_iload = 21;
		//		public final static int		opc_lload = 22;
		//		public final static int		opc_fload = 23;
		//		public final static int		opc_dload = 24;
		//		public final static int		opc_aload = 25;
		//		public final static int		opc_iload_0 = 26;
		//		public final static int		opc_iload_1 = 27;
		//		public final static int		opc_iload_2 = 28;
		//		public final static int		opc_iload_3 = 29;
		//		public final static int		opc_lload_0 = 30;
		//		public final static int		opc_lload_1 = 31;
		//		public final static int		opc_lload_2 = 32;
		//		public final static int		opc_lload_3 = 33;
		//		public final static int		opc_fload_0 = 34;
		//		public final static int		opc_fload_1 = 35;
		//		public final static int		opc_fload_2 = 36;
		//		public final static int		opc_fload_3 = 37;
		//		public final static int		opc_dload_0 = 38;
		//		public final static int		opc_dload_1 = 39;
		//		public final static int		opc_dload_2 = 40;
		//		public final static int		opc_dload_3 = 41;
		//		public final static int		opc_aload_0 = 42;
		//		public final static int		opc_aload_1 = 43;
		//		public final static int		opc_aload_2 = 44;
		//		public final static int		opc_aload_3 = 45;
		//		public final static int		opc_iaload = 46;
		//		public final static int		opc_laload = 47;
		//		public final static int		opc_faload = 48;
		//		public final static int		opc_daload = 49;
		//		public final static int		opc_aaload = 50;
		//		public final static int		opc_baload = 51;
		//		public final static int		opc_caload = 52;
		//		public final static int		opc_saload = 53;
		//		public final static int		opc_istore = 54;
		//		public final static int		opc_lstore = 55;
		//		public final static int		opc_fstore = 56;
		//		public final static int		opc_dstore = 57;
		//		public final static int		opc_astore = 58;
		//		public final static int		opc_istore_0 = 59;
		//		public final static int		opc_istore_1 = 60;
		//		public final static int		opc_istore_2 = 61;
		//		public final static int		opc_istore_3 = 62;
		//		public final static int		opc_lstore_0 = 63;
		//		public final static int		opc_lstore_1 = 64;
		//		public final static int		opc_lstore_2 = 65;
		//		public final static int		opc_lstore_3 = 66;
		//		public final static int		opc_fstore_0 = 67;
		//		public final static int		opc_fstore_1 = 68;
		//		public final static int		opc_fstore_2 = 69;
		//		public final static int		opc_fstore_3 = 70;
		//		public final static int		opc_dstore_0 = 71;
		//		public final static int		opc_dstore_1 = 72;
		//		public final static int		opc_dstore_2 = 73;
		//		public final static int		opc_dstore_3 = 74;
		//		public final static int		opc_astore_0 = 75;
		//		public final static int		opc_astore_1 = 76;
		//		public final static int		opc_astore_2 = 77;
		//		public final static int		opc_astore_3 = 78;
		//		public final static int		opc_iastore = 79;
		//		public final static int		opc_lastore = 80;
		//		public final static int		opc_fastore = 81;
		//		public final static int		opc_dastore = 82;
		//		public final static int		opc_aastore = 83;
		//		public final static int		opc_bastore = 84;
		//		public final static int		opc_castore = 85;
		//		public final static int		opc_sastore = 86;
		//		public final static int		opc_pop = 87;
		//		public final static int		opc_pop2 = 88;
		//		public final static int		opc_dup = 89;
		//		public final static int		opc_dup_x1 = 90;
		//		public final static int		opc_dup_x2 = 91;
		//		public final static int		opc_dup2 = 92;
		//		public final static int		opc_dup2_x1 = 93;
		//		public final static int		opc_dup2_x2 = 94;
		//		public final static int		opc_swap = 95;
		//		public final static int		opc_iadd = 96;
		//		public final static int		opc_ladd = 97;
		//		public final static int		opc_fadd = 98;
		//		public final static int		opc_dadd = 99;
		//		public final static int		opc_isub = 100;
		//		public final static int		opc_lsub = 101;
		//		public final static int		opc_fsub = 102;
		//		public final static int		opc_dsub = 103;
		//		public final static int		opc_imul = 104;
		//		public final static int		opc_lmul = 105;
		//		public final static int		opc_fmul = 106;
		//		public final static int		opc_dmul = 107;
		//		public final static int		opc_idiv = 108;
		//		public final static int		opc_ldiv = 109;
		//		public final static int		opc_fdiv = 110;
		//		public final static int		opc_ddiv = 111;
		//		public final static int		opc_irem = 112;
		//		public final static int		opc_lrem = 113;
		//		public final static int		opc_frem = 114;
		//		public final static int		opc_drem = 115;
		//		public final static int		opc_ineg = 116;
		//		public final static int		opc_lneg = 117;
		//		public final static int		opc_fneg = 118;
		//		public final static int		opc_dneg = 119;
		//		public final static int		opc_ishl = 120;
		//		public final static int		opc_lshl = 121;
		//		public final static int		opc_ishr = 122;
		//		public final static int		opc_lshr = 123;
		//		public final static int		opc_iushr = 124;
		//		public final static int		opc_lushr = 125;
		//		public final static int		opc_iand = 126;
		//		public final static int		opc_land = 127;
		//		public final static int		opc_ior = 128;
		//		public final static int		opc_lor = 129;
		//		public final static int		opc_ixor = 130;
		//		public final static int		opc_lxor = 131;
		//		public final static int		opc_iinc = 132;
		//		public final static int		opc_i2l = 133;
		//		public final static int		opc_i2f = 134;
		//		public final static int		opc_i2d = 135;
		//		public final static int		opc_l2i = 136;
		//		public final static int		opc_l2f = 137;
		//		public final static int		opc_l2d = 138;
		//		public final static int		opc_f2i = 139;
		//		public final static int		opc_f2l = 140;
		//		public final static int		opc_f2d = 141;
		//		public final static int		opc_d2i = 142;
		//		public final static int		opc_d2l = 143;
		//		public final static int		opc_d2f = 144;
		//		public final static int		opc_i2b = 145;
		//		public final static int		opc_i2c = 146;
		//		public final static int		opc_i2s = 147;
		//		public final static int		opc_lcmp = 148;
		//		public final static int		opc_fcmpl = 149;
		//		public final static int		opc_fcmpg = 150;
		//		public final static int		opc_dcmpl = 151;
		//		public final static int		opc_dcmpg = 152;
		//		public final static int		opc_ifeq = 153;
		//		public final static int		opc_ifne = 154;
		//		public final static int		opc_iflt = 155;
		//		public final static int		opc_ifge = 156;
		//		public final static int		opc_ifgt = 157;
		//		public final static int		opc_ifle = 158;
		//		public final static int		opc_if_icmpeq = 159;
		//		public final static int		opc_if_icmpne = 160;
		//		public final static int		opc_if_icmplt = 161;
		//		public final static int		opc_if_icmpge = 162;
		//		public final static int		opc_if_icmpgt = 163;
		//		public final static int		opc_if_icmple = 164;
		//		public final static int		opc_if_acmpeq = 165;
		//		public final static int		opc_if_acmpne = 166;
		//		public final static int		opc_goto = 167;
		//		public final static int		opc_jsr = 168;
		//		public final static int		opc_ret = 169;
		//		public final static int		opc_tableswitch = 170;
		//		public final static int		opc_lookupswitch = 171;
		//		public final static int		opc_ireturn = 172;
		//		public final static int		opc_lreturn = 173;
		//		public final static int		opc_freturn = 174;
		//		public final static int		opc_dreturn = 175;
		//		public final static int		opc_areturn = 176;
		//		public final static int		opc_return = 177;
		//		public final static int		opc_getstatic = 178;
		//		public final static int		opc_putstatic = 179;
		//		public final static int		opc_getfield = 180;
		//		public final static int		opc_putfield = 181;
		//		public final static int		opc_invokevirtual = 182;
		//		public final static int		opc_invokespecial = 183;
		//		public final static int		opc_invokestatic = 184;
		//		public final static int		opc_invokeinterface = 185;
		//		public final static int		opc_xxxunusedxxx = 186;
		//		public final static int		opc_new = 187;
		//		public final static int		opc_newarray = 188;
		//		public final static int		opc_anewarray = 189;
		//		public final static int		opc_arraylength = 190;
		//		public final static int		opc_athrow = 191;
		//		public final static int		opc_checkcast = 192;
		//		public final static int		opc_instanceof = 193;
		//		public final static int		opc_monitorenter = 194;
		//		public final static int		opc_monitorexit = 195;
		//		public final static int		opc_wide = 196;
		//		public final static int		opc_multianewarray = 197;
		//		public final static int		opc_ifnull = 198;
		//		public final static int		opc_ifnonnull = 199;
		//		public final static int		opc_goto_w = 200;
		//		public final static int		opc_jsr_w = 201;
		// Sonderbehandlung
		//	null,			//		public final static int		opc_aconst_null = 1;
		//	null, 			//		public final static int		opc_ldc = 18;
		//	null, 			//		public final static int		opc_ldc_w = 19;
		//	null, 			//		public final static int		opc_ldc2_w = 20;
		//	null,			//		public final static int		opc_aload = 25;
		//	null,			//		public final static int		opc_aaload = 50;
		//	null,			//		public final static int		opc_astore = 58;
		//	null, 			//		public final static int		opc_dup = 89;
		//	null, 			//		public final static int		opc_dup_x1 = 90;
		//	null, 			//		public final static int		opc_dup_x2 = 91;
		//	null, 			//		public final static int		opc_dup2 = 92;
		//	null, 			//		public final static int		opc_dup2_x1 = 93;
		//	null, 			//		public final static int		opc_dup2_x2 = 94;
		//	null, 			//		public final static int		opc_swap = 95;
		//	null, 			//		public final static int		opc_getstatic = 178;
		//	null, 			//		public final static int		opc_putstatic = 179;
		//	null, 			//		public final static int		opc_getfield = 180;
		//	null, 			//		public final static int		opc_putfield = 181;
		//	null, 			//		public final static int		opc_invokevirtual = 182;
		//	null, 			//		public final static int		opc_invokespecial = 183;
		//	null, 			//		public final static int		opc_invokestatic = 184;
		//	null, 			//		public final static int		opc_invokeinterface = 185;
		//	null,			//		public final static int		opc_new = 187;
		//	null,			//		public final static int		opc_newarray = 188;
		//	null,			//		public final static int		opc_anewarray = 189;
		//	null, 			//		public final static int		opc_athrow = 191;
		//	null,			//		public final static int		opc_checkcast = 192;
		//	null,			//		public final static int		opc_instanceof = 193;
		//	null, 			//		public final static int		opc_multianewarray = 197;
		public static void StepTypes(DataPoint data, Instruction instr, ConstantPool pool
			)
		{
			ListStack<VarType> stack = data.GetStack();
			int[][] arr = stack_impact[instr.opcode];
			if (arr != null)
			{
				// simple types only
				int[] read = arr[0];
				int[] write = arr[1];
				if (read != null)
				{
					int depth = 0;
					foreach (int type in read)
					{
						depth++;
						if (type == ICodeConstants.Type_Long || type == ICodeConstants.Type_Double)
						{
							depth++;
						}
					}
					stack.RemoveMultiple(depth);
				}
				if (write != null)
				{
					foreach (int type in write)
					{
						stack.Push(new VarType(type));
						if (type == ICodeConstants.Type_Long || type == ICodeConstants.Type_Double)
						{
							stack.Push(new VarType(ICodeConstants.Type_Group2empty));
						}
					}
				}
			}
			else
			{
				// Sonderbehandlung
				ProcessSpecialInstructions(data, instr, pool);
			}
		}

		private static void ProcessSpecialInstructions(DataPoint data, Instruction instr, 
			ConstantPool pool)
		{
			VarType var1;
			PrimitiveConstant cn;
			LinkConstant ck;
			ListStack<VarType> stack = data.GetStack();
			switch (instr.opcode)
			{
				case ICodeConstants.opc_aconst_null:
				{
					stack.Push(new VarType(ICodeConstants.Type_Null, 0, null));
					break;
				}

				case ICodeConstants.opc_ldc:
				case ICodeConstants.opc_ldc_w:
				case ICodeConstants.opc_ldc2_w:
				{
					PooledConstant constant = pool.GetConstant(instr.Operand(0));
					switch (constant.type)
					{
						case ICodeConstants.CONSTANT_Integer:
						{
							stack.Push(new VarType(ICodeConstants.Type_Int));
							break;
						}

						case ICodeConstants.CONSTANT_Float:
						{
							stack.Push(new VarType(ICodeConstants.Type_Float));
							break;
						}

						case ICodeConstants.CONSTANT_Long:
						{
							stack.Push(new VarType(ICodeConstants.Type_Long));
							stack.Push(new VarType(ICodeConstants.Type_Group2empty));
							break;
						}

						case ICodeConstants.CONSTANT_Double:
						{
							stack.Push(new VarType(ICodeConstants.Type_Double));
							stack.Push(new VarType(ICodeConstants.Type_Group2empty));
							break;
						}

						case ICodeConstants.CONSTANT_String:
						{
							stack.Push(new VarType(ICodeConstants.Type_Object, 0, "java/lang/String"));
							break;
						}

						case ICodeConstants.CONSTANT_Class:
						{
							stack.Push(new VarType(ICodeConstants.Type_Object, 0, "java/lang/Class"));
							break;
						}

						case ICodeConstants.CONSTANT_MethodHandle:
						{
							stack.Push(new VarType(((LinkConstant)constant).descriptor));
							break;
						}
					}
					break;
				}

				case ICodeConstants.opc_aload:
				{
					var1 = data.GetVariable(instr.Operand(0));
					if (var1 != null)
					{
						stack.Push(var1);
					}
					else
					{
						stack.Push(new VarType(ICodeConstants.Type_Object, 0, null));
					}
					break;
				}

				case ICodeConstants.opc_aaload:
				{
					var1 = stack.Pop(2);
					stack.Push(new VarType(var1.type, var1.arrayDim - 1, var1.value));
					break;
				}

				case ICodeConstants.opc_astore:
				{
					data.SetVariable(instr.Operand(0), stack.Pop());
					break;
				}

				case ICodeConstants.opc_dup:
				case ICodeConstants.opc_dup_x1:
				case ICodeConstants.opc_dup_x2:
				{
					int depth1 = 88 - instr.opcode;
					stack.InsertByOffset(depth1, stack.GetByOffset(-1).Copy());
					break;
				}

				case ICodeConstants.opc_dup2:
				case ICodeConstants.opc_dup2_x1:
				case ICodeConstants.opc_dup2_x2:
				{
					int depth2 = 90 - instr.opcode;
					stack.InsertByOffset(depth2, stack.GetByOffset(-2).Copy());
					stack.InsertByOffset(depth2, stack.GetByOffset(-1).Copy());
					break;
				}

				case ICodeConstants.opc_swap:
				{
					var1 = stack.Pop();
					stack.InsertByOffset(-1, var1);
					break;
				}

				case ICodeConstants.opc_getfield:
				{
					stack.Pop();
					goto case ICodeConstants.opc_getstatic;
				}

				case ICodeConstants.opc_getstatic:
				{
					ck = pool.GetLinkConstant(instr.Operand(0));
					var1 = new VarType(ck.descriptor);
					stack.Push(var1);
					if (var1.stackSize == 2)
					{
						stack.Push(new VarType(ICodeConstants.Type_Group2empty));
					}
					break;
				}

				case ICodeConstants.opc_putfield:
				{
					stack.Pop();
					goto case ICodeConstants.opc_putstatic;
				}

				case ICodeConstants.opc_putstatic:
				{
					ck = pool.GetLinkConstant(instr.Operand(0));
					var1 = new VarType(ck.descriptor);
					stack.Pop(var1.stackSize);
					break;
				}

				case ICodeConstants.opc_invokevirtual:
				case ICodeConstants.opc_invokespecial:
				case ICodeConstants.opc_invokeinterface:
				{
					stack.Pop();
					goto case ICodeConstants.opc_invokestatic;
				}

				case ICodeConstants.opc_invokestatic:
				case ICodeConstants.opc_invokedynamic:
				{
					if (instr.opcode != ICodeConstants.opc_invokedynamic || instr.bytecodeVersion >= 
						ICodeConstants.Bytecode_Java_7)
					{
						ck = pool.GetLinkConstant(instr.Operand(0));
						MethodDescriptor md = MethodDescriptor.ParseDescriptor(ck.descriptor);
						for (int i = 0; i < md.@params.Length; i++)
						{
							stack.Pop(md.@params[i].stackSize);
						}
						if (md.ret.type != ICodeConstants.Type_Void)
						{
							stack.Push(md.ret);
							if (md.ret.stackSize == 2)
							{
								stack.Push(new VarType(ICodeConstants.Type_Group2empty));
							}
						}
					}
					break;
				}

				case ICodeConstants.opc_new:
				{
					cn = pool.GetPrimitiveConstant(instr.Operand(0));
					stack.Push(new VarType(ICodeConstants.Type_Object, 0, cn.GetString()));
					break;
				}

				case ICodeConstants.opc_newarray:
				{
					stack.Pop();
					stack.Push(new VarType(arr_type[instr.Operand(0) - 4], 1).ResizeArrayDim(1));
					break;
				}

				case ICodeConstants.opc_athrow:
				{
					var1 = stack.Pop();
					stack.Clear();
					stack.Push(var1);
					break;
				}

				case ICodeConstants.opc_checkcast:
				case ICodeConstants.opc_instanceof:
				{
					stack.Pop();
					cn = pool.GetPrimitiveConstant(instr.Operand(0));
					stack.Push(new VarType(ICodeConstants.Type_Object, 0, cn.GetString()));
					break;
				}

				case ICodeConstants.opc_anewarray:
				case ICodeConstants.opc_multianewarray:
				{
					int dimensions = (instr.opcode == ICodeConstants.opc_anewarray) ? 1 : instr.Operand
						(1);
					stack.Pop(dimensions);
					cn = pool.GetPrimitiveConstant(instr.Operand(0));
					if (cn.isArray)
					{
						var1 = new VarType(ICodeConstants.Type_Object, 0, cn.GetString());
						var1 = var1.ResizeArrayDim(var1.arrayDim + dimensions);
						stack.Push(var1);
					}
					else
					{
						stack.Push(new VarType(ICodeConstants.Type_Object, dimensions, cn.GetString()));
					}
					break;
				}
			}
		}
	}
}
