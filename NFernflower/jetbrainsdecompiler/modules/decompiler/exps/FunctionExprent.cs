/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System;
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Struct.Match;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class FunctionExprent : Exprent
	{
		public const int Function_Add = 0;

		public const int Function_Sub = 1;

		public const int Function_Mul = 2;

		public const int Function_Div = 3;

		public const int Function_And = 4;

		public const int Function_Or = 5;

		public const int Function_Xor = 6;

		public const int Function_Rem = 7;

		public const int Function_Shl = 8;

		public const int Function_Shr = 9;

		public const int Function_Ushr = 10;

		public const int Function_Bit_Not = 11;

		public const int Function_Bool_Not = 12;

		public const int Function_Neg = 13;

		public const int Function_I2l = 14;

		public const int Function_I2f = 15;

		public const int Function_I2d = 16;

		public const int Function_L2i = 17;

		public const int Function_L2f = 18;

		public const int Function_L2d = 19;

		public const int Function_F2i = 20;

		public const int Function_F2l = 21;

		public const int Function_F2d = 22;

		public const int Function_D2i = 23;

		public const int Function_D2l = 24;

		public const int Function_D2f = 25;

		public const int Function_I2b = 26;

		public const int Function_I2c = 27;

		public const int Function_I2s = 28;

		public const int Function_Cast = 29;

		public const int Function_Instanceof = 30;

		public const int Function_Array_Length = 31;

		public const int Function_Imm = 32;

		public const int Function_Mmi = 33;

		public const int Function_Ipp = 34;

		public const int Function_Ppi = 35;

		public const int Function_Iif = 36;

		public const int Function_Lcmp = 37;

		public const int Function_Fcmpl = 38;

		public const int Function_Fcmpg = 39;

		public const int Function_Dcmpl = 40;

		public const int Function_Dcmpg = 41;

		public const int Function_Eq = 42;

		public const int Function_Ne = 43;

		public const int Function_Lt = 44;

		public const int Function_Ge = 45;

		public const int Function_Gt = 46;

		public const int Function_Le = 47;

		public const int Function_Cadd = 48;

		public const int Function_Cor = 49;

		public const int Function_Str_Concat = 50;

		private static readonly VarType[] Types = new VarType[] { VarType.Vartype_Long, VarType
			.Vartype_Float, VarType.Vartype_Double, VarType.Vartype_Int, VarType.Vartype_Float
			, VarType.Vartype_Double, VarType.Vartype_Int, VarType.Vartype_Long, VarType.Vartype_Double
			, VarType.Vartype_Int, VarType.Vartype_Long, VarType.Vartype_Float, VarType.Vartype_Byte
			, VarType.Vartype_Char, VarType.Vartype_Short };

		private static readonly string[] Operators = new string[] { " + ", " - ", " * ", 
			" / ", " & ", " | ", " ^ ", " % ", " << ", " >> ", " >>> ", " == ", " != ", " < "
			, " >= ", " > ", " <= ", " && ", " || ", " + " };

		private static readonly int[] Precedence = new int[] { 3, 3, 2, 2, 7, 9, 8, 2, 4, 
			4, 4, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 6, 0, 1, 1, 1, 1, 
			12, -1, -1, -1, -1, -1, 6, 6, 5, 5, 5, 5, 10, 11, 3 };

		private static readonly HashSet<int> Associativity = new HashSet<int>(Sharpen.Arrays.AsList
			(Function_Add, Function_Mul, Function_And, Function_Or, Function_Xor, Function_Cadd
			, Function_Cor, Function_Str_Concat));

		private int funcType;

		private VarType implicitType;

		private readonly List<Exprent> lstOperands;

		public FunctionExprent(int funcType, ListStack<Exprent> stack, HashSet<int> bytecodeOffsets
			)
			: this(funcType, new List<Exprent>(), bytecodeOffsets)
		{
			// FUNCTION_ADD
			// FUNCTION_SUB
			// FUNCTION_MUL
			// FUNCTION_DIV
			// FUNCTION_AND
			// FUNCTION_OR
			// FUNCTION_XOR
			// FUNCTION_REM
			// FUNCTION_SHL
			// FUNCTION_SHR
			// FUNCTION_USHR
			// FUNCTION_BIT_NOT
			// FUNCTION_BOOL_NOT
			// FUNCTION_NEG
			// FUNCTION_I2L
			// FUNCTION_I2F
			// FUNCTION_I2D
			// FUNCTION_L2I
			// FUNCTION_L2F
			// FUNCTION_L2D
			// FUNCTION_F2I
			// FUNCTION_F2L
			// FUNCTION_F2D
			// FUNCTION_D2I
			// FUNCTION_D2L
			// FUNCTION_D2F
			// FUNCTION_I2B
			// FUNCTION_I2C
			// FUNCTION_I2S
			// FUNCTION_CAST
			// FUNCTION_INSTANCEOF
			// FUNCTION_ARRAY_LENGTH
			// FUNCTION_IMM
			// FUNCTION_MMI
			// FUNCTION_IPP
			// FUNCTION_PPI
			// FUNCTION_IFF
			// FUNCTION_LCMP
			// FUNCTION_FCMPL
			// FUNCTION_FCMPG
			// FUNCTION_DCMPL
			// FUNCTION_DCMPG
			// FUNCTION_EQ = 41;
			// FUNCTION_NE = 42;
			// FUNCTION_LT = 43;
			// FUNCTION_GE = 44;
			// FUNCTION_GT = 45;
			// FUNCTION_LE = 46;
			// FUNCTION_CADD = 47;
			// FUNCTION_COR = 48;
			// FUNCTION_STR_CONCAT = 49;
			if (funcType >= Function_Bit_Not && funcType <= Function_Ppi && funcType != Function_Cast
				 && funcType != Function_Instanceof)
			{
				lstOperands.Add(stack.Pop());
			}
			else if (funcType == Function_Iif)
			{
				throw new Exception("no direct instantiation possible");
			}
			else
			{
				Exprent expr = stack.Pop();
				lstOperands.Add(stack.Pop());
				lstOperands.Add(expr);
			}
		}

		public FunctionExprent(int funcType, List<Exprent> operands, HashSet<int> bytecodeOffsets
			)
			: base(Exprent_Function)
		{
			this.funcType = funcType;
			this.lstOperands = operands;
			AddBytecodeOffsets(bytecodeOffsets);
		}

		public FunctionExprent(int funcType, Exprent operand, HashSet<int> bytecodeOffsets
			)
			: this(funcType, new List<Exprent>(1), bytecodeOffsets)
		{
			lstOperands.Add(operand);
		}

		public override VarType GetExprType()
		{
			VarType exprType = null;
			if (funcType <= Function_Neg || funcType == Function_Ipp || funcType == Function_Ppi
				 || funcType == Function_Imm || funcType == Function_Mmi)
			{
				VarType type1 = lstOperands[0].GetExprType();
				VarType type2 = null;
				if (lstOperands.Count > 1)
				{
					type2 = lstOperands[1].GetExprType();
				}
				switch (funcType)
				{
					case Function_Imm:
					case Function_Mmi:
					case Function_Ipp:
					case Function_Ppi:
					{
						exprType = implicitType;
						break;
					}

					case Function_Bool_Not:
					{
						exprType = VarType.Vartype_Boolean;
						break;
					}

					case Function_Shl:
					case Function_Shr:
					case Function_Ushr:
					case Function_Bit_Not:
					case Function_Neg:
					{
						exprType = GetMaxVarType(new VarType[] { type1 });
						break;
					}

					case Function_Add:
					case Function_Sub:
					case Function_Mul:
					case Function_Div:
					case Function_Rem:
					{
						exprType = GetMaxVarType(new VarType[] { type1, type2 });
						break;
					}

					case Function_And:
					case Function_Or:
					case Function_Xor:
					{
						if (type1.type == ICodeConstants.Type_Boolean & type2.type == ICodeConstants.Type_Boolean)
						{
							exprType = VarType.Vartype_Boolean;
						}
						else
						{
							exprType = GetMaxVarType(new VarType[] { type1, type2 });
						}
						break;
					}
				}
			}
			else if (funcType == Function_Cast)
			{
				exprType = lstOperands[1].GetExprType();
			}
			else if (funcType == Function_Iif)
			{
				Exprent param1 = lstOperands[1];
				Exprent param2 = lstOperands[2];
				VarType supertype = VarType.GetCommonSupertype(param1.GetExprType(), param2.GetExprType
					());
				if (param1.type == Exprent.Exprent_Const && param2.type == Exprent.Exprent_Const 
					&& supertype.type != ICodeConstants.Type_Boolean && VarType.Vartype_Int.IsSuperset
					(supertype))
				{
					exprType = VarType.Vartype_Int;
				}
				else
				{
					exprType = supertype;
				}
			}
			else if (funcType == Function_Str_Concat)
			{
				exprType = VarType.Vartype_String;
			}
			else if (funcType >= Function_Eq || funcType == Function_Instanceof)
			{
				exprType = VarType.Vartype_Boolean;
			}
			else if (funcType >= Function_Array_Length)
			{
				exprType = VarType.Vartype_Int;
			}
			else
			{
				exprType = Types[funcType - Function_I2l];
			}
			return exprType;
		}

		public override int GetExprentUse()
		{
			if (funcType >= Function_Imm && funcType <= Function_Ppi)
			{
				return 0;
			}
			else
			{
				int ret = Exprent.Multiple_Uses | Exprent.Side_Effects_Free;
				foreach (Exprent expr in lstOperands)
				{
					ret &= expr.GetExprentUse();
				}
				return ret;
			}
		}

		public override CheckTypesResult CheckExprTypeBounds()
		{
			CheckTypesResult result = new CheckTypesResult();
			Exprent param1 = lstOperands[0];
			VarType type1 = param1.GetExprType();
			Exprent param2 = null;
			VarType type2 = null;
			if (lstOperands.Count > 1)
			{
				param2 = lstOperands[1];
				type2 = param2.GetExprType();
			}
			switch (funcType)
			{
				case Function_Iif:
				{
					VarType supertype = GetExprType();
					result.AddMinTypeExprent(param1, VarType.Vartype_Boolean);
					result.AddMinTypeExprent(param2, VarType.GetMinTypeInFamily(supertype.typeFamily)
						);
					result.AddMinTypeExprent(lstOperands[2], VarType.GetMinTypeInFamily(supertype.typeFamily
						));
					break;
				}

				case Function_I2l:
				case Function_I2f:
				case Function_I2d:
				case Function_I2b:
				case Function_I2c:
				case Function_I2s:
				{
					result.AddMinTypeExprent(param1, VarType.Vartype_Bytechar);
					result.AddMaxTypeExprent(param1, VarType.Vartype_Int);
					break;
				}

				case Function_Imm:
				case Function_Ipp:
				case Function_Mmi:
				case Function_Ppi:
				{
					result.AddMinTypeExprent(param1, implicitType);
					result.AddMaxTypeExprent(param1, implicitType);
					break;
				}

				case Function_Add:
				case Function_Sub:
				case Function_Mul:
				case Function_Div:
				case Function_Rem:
				case Function_Shl:
				case Function_Shr:
				case Function_Ushr:
				case Function_Lt:
				case Function_Ge:
				case Function_Gt:
				case Function_Le:
				{
					result.AddMinTypeExprent(param2, VarType.Vartype_Bytechar);
					goto case Function_Bit_Not;
				}

				case Function_Bit_Not:
				case Function_Neg:
				{
					// case FUNCTION_BOOL_NOT:
					result.AddMinTypeExprent(param1, VarType.Vartype_Bytechar);
					break;
				}

				case Function_And:
				case Function_Or:
				case Function_Xor:
				case Function_Eq:
				case Function_Ne:
				{
					if (type1.type == ICodeConstants.Type_Boolean)
					{
						if (type2.IsStrictSuperset(type1))
						{
							result.AddMinTypeExprent(param1, VarType.Vartype_Bytechar);
						}
						else
						{
							// both are booleans
							bool param1_false_boolean = type1.IsFalseBoolean() || (param1.type == Exprent.Exprent_Const
								 && !((ConstExprent)param1).HasBooleanValue());
							bool param2_false_boolean = type1.IsFalseBoolean() || (param2.type == Exprent.Exprent_Const
								 && !((ConstExprent)param2).HasBooleanValue());
							if (param1_false_boolean || param2_false_boolean)
							{
								result.AddMinTypeExprent(param1, VarType.Vartype_Bytechar);
								result.AddMinTypeExprent(param2, VarType.Vartype_Bytechar);
							}
						}
					}
					else if (type2.type == ICodeConstants.Type_Boolean)
					{
						if (type1.IsStrictSuperset(type2))
						{
							result.AddMinTypeExprent(param2, VarType.Vartype_Bytechar);
						}
					}
					break;
				}
			}
			return result;
		}

		public override List<Exprent> GetAllExprents()
		{
			return new List<Exprent>(lstOperands);
		}

		public override Exprent Copy()
		{
			List<Exprent> lst = new List<Exprent>();
			foreach (Exprent expr in lstOperands)
			{
				lst.Add(expr.Copy());
			}
			FunctionExprent func = new FunctionExprent(funcType, lst, bytecode);
			func.SetImplicitType(implicitType);
			return func;
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is FunctionExprent))
			{
				return false;
			}
			FunctionExprent fe = (FunctionExprent)o;
			return funcType == fe.GetFuncType() && InterpreterUtil.EqualLists(lstOperands, fe
				.GetLstOperands());
		}

		// TODO: order of operands insignificant
		public override void ReplaceExprent(Exprent oldExpr, Exprent newExpr)
		{
			for (int i = 0; i < lstOperands.Count; i++)
			{
				if (oldExpr == lstOperands[i])
				{
					lstOperands[i] = newExpr;
				}
			}
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			tracer.AddMapping(bytecode);
			if (funcType <= Function_Ushr)
			{
				return WrapOperandString(lstOperands[0], false, indent, tracer).Append(Operators[
					funcType]).Append(WrapOperandString(lstOperands[1], true, indent, tracer));
			}
			// try to determine more accurate type for 'char' literals
			if (funcType >= Function_Eq)
			{
				if (funcType <= Function_Le)
				{
					Exprent left = lstOperands[0];
					Exprent right = lstOperands[1];
					if (right.type == Exprent_Const)
					{
						((ConstExprent)right).AdjustConstType(left.GetExprType());
					}
					else if (left.type == Exprent_Const)
					{
						((ConstExprent)left).AdjustConstType(right.GetExprType());
					}
				}
				return WrapOperandString(lstOperands[0], false, indent, tracer).Append(Operators[
					funcType - Function_Eq + 11]).Append(WrapOperandString(lstOperands[1], true, indent
					, tracer));
			}
			switch (funcType)
			{
				case Function_Bit_Not:
				{
					return WrapOperandString(lstOperands[0], true, indent, tracer).Prepend("~");
				}

				case Function_Bool_Not:
				{
					return WrapOperandString(lstOperands[0], true, indent, tracer).Prepend("!");
				}

				case Function_Neg:
				{
					return WrapOperandString(lstOperands[0], true, indent, tracer).Prepend("-");
				}

				case Function_Cast:
				{
					return lstOperands[1].ToJava(indent, tracer).Enclose("(", ")").Append(WrapOperandString
						(lstOperands[0], true, indent, tracer));
				}

				case Function_Array_Length:
				{
					Exprent arr = lstOperands[0];
					TextBuffer res = WrapOperandString(arr, false, indent, tracer);
					if (arr.GetExprType().arrayDim == 0)
					{
						VarType objArr = VarType.Vartype_Object.ResizeArrayDim(1);
						// type family does not change
						res.Enclose("((" + ExprProcessor.GetCastTypeName(objArr) + ")", ")");
					}
					return res.Append(".length");
				}

				case Function_Iif:
				{
					return WrapOperandString(lstOperands[0], true, indent, tracer).Append(" ? ").Append
						(WrapOperandString(lstOperands[1], true, indent, tracer)).Append(" : ").Append(WrapOperandString
						(lstOperands[2], true, indent, tracer));
				}

				case Function_Ipp:
				{
					return WrapOperandString(lstOperands[0], true, indent, tracer).Append("++");
				}

				case Function_Ppi:
				{
					return WrapOperandString(lstOperands[0], true, indent, tracer).Prepend("++");
				}

				case Function_Imm:
				{
					return WrapOperandString(lstOperands[0], true, indent, tracer).Append("--");
				}

				case Function_Mmi:
				{
					return WrapOperandString(lstOperands[0], true, indent, tracer).Prepend("--");
				}

				case Function_Instanceof:
				{
					return WrapOperandString(lstOperands[0], true, indent, tracer).Append(" instanceof "
						).Append(WrapOperandString(lstOperands[1], true, indent, tracer));
				}

				case Function_Lcmp:
				{
					// shouldn't appear in the final code
					return WrapOperandString(lstOperands[0], true, indent, tracer).Prepend("__lcmp__("
						).Append(", ").Append(WrapOperandString(lstOperands[1], true, indent, tracer)).Append
						(")");
				}

				case Function_Fcmpl:
				{
					// shouldn't appear in the final code
					return WrapOperandString(lstOperands[0], true, indent, tracer).Prepend("__fcmpl__("
						).Append(", ").Append(WrapOperandString(lstOperands[1], true, indent, tracer)).Append
						(")");
				}

				case Function_Fcmpg:
				{
					// shouldn't appear in the final code
					return WrapOperandString(lstOperands[0], true, indent, tracer).Prepend("__fcmpg__("
						).Append(", ").Append(WrapOperandString(lstOperands[1], true, indent, tracer)).Append
						(")");
				}

				case Function_Dcmpl:
				{
					// shouldn't appear in the final code
					return WrapOperandString(lstOperands[0], true, indent, tracer).Prepend("__dcmpl__("
						).Append(", ").Append(WrapOperandString(lstOperands[1], true, indent, tracer)).Append
						(")");
				}

				case Function_Dcmpg:
				{
					// shouldn't appear in the final code
					return WrapOperandString(lstOperands[0], true, indent, tracer).Prepend("__dcmpg__("
						).Append(", ").Append(WrapOperandString(lstOperands[1], true, indent, tracer)).Append
						(")");
				}
			}
			if (funcType <= Function_I2s)
			{
				return WrapOperandString(lstOperands[0], true, indent, tracer).Prepend("(" + ExprProcessor
					.GetTypeName(Types[funcType - Function_I2l]) + ")");
			}
			//		return "<unknown function>";
			throw new Exception("invalid function");
		}

		public override int GetPrecedence()
		{
			return GetPrecedence(funcType);
		}

		public static int GetPrecedence(int func)
		{
			return Precedence[func];
		}

		public virtual VarType GetSimpleCastType()
		{
			return Types[funcType - Function_I2l];
		}

		private TextBuffer WrapOperandString(Exprent expr, bool eq, int indent, BytecodeMappingTracer
			 tracer)
		{
			int myprec = GetPrecedence();
			int exprprec = expr.GetPrecedence();
			bool parentheses = exprprec > myprec;
			if (!parentheses && eq)
			{
				parentheses = (exprprec == myprec);
				if (parentheses)
				{
					if (expr.type == Exprent.Exprent_Function && ((FunctionExprent)expr).GetFuncType(
						) == funcType)
					{
						parentheses = !Associativity.Contains(funcType);
					}
				}
			}
			TextBuffer res = expr.ToJava(indent, tracer);
			if (parentheses)
			{
				res.Enclose("(", ")");
			}
			return res;
		}

		private static VarType GetMaxVarType(VarType[] arr)
		{
			int[] types = new int[] { ICodeConstants.Type_Double, ICodeConstants.Type_Float, 
				ICodeConstants.Type_Long };
			VarType[] vartypes = new VarType[] { VarType.Vartype_Double, VarType.Vartype_Float
				, VarType.Vartype_Long };
			for (int i = 0; i < types.Length; i++)
			{
				foreach (VarType anArr in arr)
				{
					if (anArr.type == types[i])
					{
						return vartypes[i];
					}
				}
			}
			return VarType.Vartype_Int;
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual int GetFuncType()
		{
			return funcType;
		}

		public virtual void SetFuncType(int funcType)
		{
			this.funcType = funcType;
		}

		public virtual List<Exprent> GetLstOperands()
		{
			return lstOperands;
		}

		public virtual void SetImplicitType(VarType implicitType)
		{
			this.implicitType = implicitType;
		}

		// *****************************************************************************
		// IMatchable implementation
		// *****************************************************************************
		public override bool Match(MatchNode matchNode, MatchEngine engine)
		{
			if (!base.Match(matchNode, engine))
			{
				return false;
			}
			int type = (int)matchNode.GetRuleValue(IMatchable.MatchProperties.Exprent_Functype
				);
			return type == null || this.funcType == type;
		}
	}
}
