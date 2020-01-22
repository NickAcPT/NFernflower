// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.Text;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Struct.Match;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class ConstExprent : Exprent
	{
		private static readonly Dictionary<int, string> Char_Escapes;

		static ConstExprent()
		{
			Char_Escapes = new Dictionary<int, string>();
			Sharpen.Collections.Put(Char_Escapes, unchecked((int)(0x8)), "\\b");
			/* \u0008: backspace BS */
			Sharpen.Collections.Put(Char_Escapes, unchecked((int)(0x9)), "\\t");
			/* \u0009: horizontal tab HT */
			Sharpen.Collections.Put(Char_Escapes, unchecked((int)(0xA)), "\\n");
			/* \u000a: linefeed LF */
			Sharpen.Collections.Put(Char_Escapes, unchecked((int)(0xC)), "\\f");
			/* \u000c: form feed FF */
			Sharpen.Collections.Put(Char_Escapes, unchecked((int)(0xD)), "\\r");
			/* \u000d: carriage return CR */
			//CHAR_ESCAPES.put(0x22, "\\\""); /* \u0022: double quote " */
			Sharpen.Collections.Put(Char_Escapes, unchecked((int)(0x27)), "\\\'");
			/* \u0027: single quote ' */
			Sharpen.Collections.Put(Char_Escapes, unchecked((int)(0x5C)), "\\\\");
		}

		private VarType constType;

		private readonly object value;

		private readonly bool boolPermitted;

		public ConstExprent(int val, bool boolPermitted, HashSet<int> bytecodeOffsets)
			: this(GuessType(val, boolPermitted), val, boolPermitted, bytecodeOffsets)
		{
		}

		public ConstExprent(VarType constType, object value, HashSet<int> bytecodeOffsets
			)
			: this(constType, value, false, bytecodeOffsets)
		{
		}

		private ConstExprent(VarType constType, object value, bool boolPermitted, HashSet
			<int> bytecodeOffsets)
			: base(Exprent_Const)
		{
			/* \u005c: backslash \ */
			this.constType = constType;
			this.value = value;
			this.boolPermitted = boolPermitted;
			AddBytecodeOffsets(bytecodeOffsets);
		}

		private static VarType GuessType(int val, bool boolPermitted)
		{
			if (boolPermitted)
			{
				VarType constType = VarType.Vartype_Boolean;
				if (val != 0 && val != 1)
				{
					constType = constType.Copy(true);
				}
				return constType;
			}
			else if (0 <= val && val <= 127)
			{
				return VarType.Vartype_Bytechar;
			}
			else if (-128 <= val && val <= 127)
			{
				return VarType.Vartype_Byte;
			}
			else if (0 <= val && val <= 32767)
			{
				return VarType.Vartype_Shortchar;
			}
			else if (-32768 <= val && val <= 32767)
			{
				return VarType.Vartype_Short;
			}
			else if (0 <= val && val <= unchecked((int)(0xFFFF)))
			{
				return VarType.Vartype_Char;
			}
			else
			{
				return VarType.Vartype_Int;
			}
		}

		public override Exprent Copy()
		{
			return new ConstExprent(constType, value, bytecode);
		}

		public override VarType GetExprType()
		{
			return constType;
		}

		public override int GetExprentUse()
		{
			return Exprent.Multiple_Uses | Exprent.Side_Effects_Free;
		}

		public override List<Exprent> GetAllExprents()
		{
			return new List<Exprent>();
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			bool literal = DecompilerContext.GetOption(IFernflowerPreferences.Literals_As_Is
				);
			bool ascii = DecompilerContext.GetOption(IFernflowerPreferences.Ascii_String_Characters
				);
			tracer.AddMapping(bytecode);
			if (constType.type != ICodeConstants.Type_Null && value == null)
			{
				return new TextBuffer(ExprProcessor.GetCastTypeName(constType));
			}
			switch (constType.type)
			{
				case ICodeConstants.Type_Boolean:
				{
					return new TextBuffer(((int)value != 0).ToString());
				}

				case ICodeConstants.Type_Char:
				{
					int val = (int)value;
					string ret = Char_Escapes.GetOrNull(val);
					if (ret == null)
					{
						char c = (char)val;
						if (IsPrintableAscii(c) || !ascii && TextUtil.IsPrintableUnicode(c))
						{
							ret = c.ToString();
						}
						else
						{
							ret = TextUtil.CharToUnicodeLiteral(c);
						}
					}
					return new TextBuffer(ret).Enclose("'", "'");
				}

				case ICodeConstants.Type_Byte:
				case ICodeConstants.Type_Bytechar:
				case ICodeConstants.Type_Short:
				case ICodeConstants.Type_Shortchar:
				case ICodeConstants.Type_Int:
				{
					int intVal = (int)value;
					if (!literal)
					{
						if (intVal == int.MaxValue)
						{
							return new FieldExprent("MAX_VALUE", "java/lang/Integer", true, null, FieldDescriptor
								.Integer_Descriptor, bytecode).ToJava(0, tracer);
						}
						else if (intVal == int.MinValue)
						{
							return new FieldExprent("MIN_VALUE", "java/lang/Integer", true, null, FieldDescriptor
								.Integer_Descriptor, bytecode).ToJava(0, tracer);
						}
					}
					return new TextBuffer(value.ToString());
				}

				case ICodeConstants.Type_Long:
				{
					long longVal = (long)value;
					if (!literal)
					{
						if (longVal == long.MaxValue)
						{
							return new FieldExprent("MAX_VALUE", "java/lang/Long", true, null, FieldDescriptor
								.Long_Descriptor, bytecode).ToJava(0, tracer);
						}
						else if (longVal == long.MinValue)
						{
							return new FieldExprent("MIN_VALUE", "java/lang/Long", true, null, FieldDescriptor
								.Long_Descriptor, bytecode).ToJava(0, tracer);
						}
					}
					return new TextBuffer(value.ToString()).Append('L');
				}

				case ICodeConstants.Type_Float:
				{
					float floatVal = (float)value;
					if (!literal)
					{
						if (float.IsNaN(floatVal))
						{
							return new FieldExprent("NaN", "java/lang/Float", true, null, FieldDescriptor.Float_Descriptor
								, bytecode).ToJava(0, tracer);
						}
						else if (floatVal == float.PositiveInfinity)
						{
							return new FieldExprent("POSITIVE_INFINITY", "java/lang/Float", true, null, FieldDescriptor
								.Float_Descriptor, bytecode).ToJava(0, tracer);
						}
						else if (floatVal == float.NegativeInfinity)
						{
							return new FieldExprent("NEGATIVE_INFINITY", "java/lang/Float", true, null, FieldDescriptor
								.Float_Descriptor, bytecode).ToJava(0, tracer);
						}
						else if (floatVal == float.MaxValue)
						{
							return new FieldExprent("MAX_VALUE", "java/lang/Float", true, null, FieldDescriptor
								.Float_Descriptor, bytecode).ToJava(0, tracer);
						}
						else if (floatVal == float.MinValue)
						{
							return new FieldExprent("MIN_VALUE", "java/lang/Float", true, null, FieldDescriptor
								.Float_Descriptor, bytecode).ToJava(0, tracer);
						}
					}
					else if (float.IsNaN(floatVal))
					{
						return new TextBuffer("0.0F / 0.0");
					}
					else if (floatVal == float.PositiveInfinity)
					{
						return new TextBuffer("1.0F / 0.0");
					}
					else if (floatVal == float.NegativeInfinity)
					{
						return new TextBuffer("-1.0F / 0.0");
					}
					return new TextBuffer(value.ToString()).Append('F');
				}

				case ICodeConstants.Type_Double:
				{
					double doubleVal = (double)value;
					if (!literal)
					{
						if (double.IsNaN(doubleVal))
						{
							return new FieldExprent("NaN", "java/lang/Double", true, null, FieldDescriptor.Double_Descriptor
								, bytecode).ToJava(0, tracer);
						}
						else if (doubleVal == double.PositiveInfinity)
						{
							return new FieldExprent("POSITIVE_INFINITY", "java/lang/Double", true, null, FieldDescriptor
								.Double_Descriptor, bytecode).ToJava(0, tracer);
						}
						else if (doubleVal == double.NegativeInfinity)
						{
							return new FieldExprent("NEGATIVE_INFINITY", "java/lang/Double", true, null, FieldDescriptor
								.Double_Descriptor, bytecode).ToJava(0, tracer);
						}
						else if (doubleVal == double.MaxValue)
						{
							return new FieldExprent("MAX_VALUE", "java/lang/Double", true, null, FieldDescriptor
								.Double_Descriptor, bytecode).ToJava(0, tracer);
						}
						else if (doubleVal == double.MinValue)
						{
							return new FieldExprent("MIN_VALUE", "java/lang/Double", true, null, FieldDescriptor
								.Double_Descriptor, bytecode).ToJava(0, tracer);
						}
					}
					else if (double.IsNaN(doubleVal))
					{
						return new TextBuffer("0.0D / 0.0");
					}
					else if (doubleVal == double.PositiveInfinity)
					{
						return new TextBuffer("1.0D / 0.0");
					}
					else if (doubleVal == double.NegativeInfinity)
					{
						return new TextBuffer("-1.0D / 0.0");
					}
					return new TextBuffer(value.ToString()).Append('D');
				}

				case ICodeConstants.Type_Null:
				{
					return new TextBuffer("null");
				}

				case ICodeConstants.Type_Object:
				{
					if (constType.Equals(VarType.Vartype_String))
					{
						return new TextBuffer(ConvertStringToJava(value.ToString(), ascii)).Enclose("\"", 
							"\"");
					}
					else if (constType.Equals(VarType.Vartype_Class))
					{
						string stringVal = value.ToString();
						VarType type = new VarType(stringVal, !stringVal.StartsWith("["));
						return new TextBuffer(ExprProcessor.GetCastTypeName(type)).Append(".class");
					}
					break;
				}
			}
			throw new Exception("invalid constant type: " + constType);
		}

		public virtual bool IsNull()
		{
			return ICodeConstants.Type_Null == constType.type;
		}

		private static string ConvertStringToJava(string value, bool ascii)
		{
			char[] arr = value.ToCharArray();
			StringBuilder buffer = new StringBuilder(arr.Length);
			foreach (char c in arr)
			{
				switch (c)
				{
					case '\\':
					{
						//  u005c: backslash \
						buffer.Append("\\\\");
						break;
					}

					case (char)unchecked((int)(0x8)):
					{
						// "\\\\b");  //  u0008: backspace BS
						buffer.Append("\\b");
						break;
					}

					case (char)unchecked((int)(0x9)):
					{
						//"\\\\t");  //  u0009: horizontal tab HT
						buffer.Append("\\t");
						break;
					}

					case (char)unchecked((int)(0xA)):
					{
						//"\\\\n");  //  u000a: linefeed LF
						buffer.Append("\\n");
						break;
					}

					case (char)unchecked((int)(0xC)):
					{
						//"\\\\f");  //  u000c: form feed FF
						buffer.Append("\\f");
						break;
					}

					case (char)unchecked((int)(0xD)):
					{
						//"\\\\r");  //  u000d: carriage return CR
						buffer.Append("\\r");
						break;
					}

					case (char)unchecked((int)(0x22)):
					{
						//"\\\\\""); // u0022: double quote "
						buffer.Append("\\\"");
						break;
					}

					default:
					{
						//case 0x27: //"\\\\'");  // u0027: single quote '
						//  buffer.append("\\\'");
						//  break;
						if (IsPrintableAscii(c) || !ascii && TextUtil.IsPrintableUnicode(c))
						{
							buffer.Append(c);
						}
						else
						{
							buffer.Append(TextUtil.CharToUnicodeLiteral(c));
						}
						break;
					}
				}
			}
			return buffer.ToString();
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is ConstExprent))
			{
				return false;
			}
			ConstExprent cn = (ConstExprent)o;
			return InterpreterUtil.EqualObjects(constType, cn.GetConstType()) && InterpreterUtil
				.EqualObjects(value, cn.GetValue());
		}

		public override int GetHashCode()
		{
			int result = constType != null ? constType.GetHashCode() : 0;
			result = 31 * result + (value != null ? value.GetHashCode() : 0);
			return result;
		}

		public virtual bool HasBooleanValue()
		{
			switch (constType.type)
			{
				case ICodeConstants.Type_Boolean:
				case ICodeConstants.Type_Char:
				case ICodeConstants.Type_Byte:
				case ICodeConstants.Type_Bytechar:
				case ICodeConstants.Type_Short:
				case ICodeConstants.Type_Shortchar:
				case ICodeConstants.Type_Int:
				{
					int value = (int)this.value;
					return value == 0 || (DecompilerContext.GetOption(IFernflowerPreferences.Boolean_True_One
						) && value == 1);
				}
			}
			return false;
		}

		public virtual bool HasValueOne()
		{
			switch (constType.type)
			{
				case ICodeConstants.Type_Boolean:
				case ICodeConstants.Type_Char:
				case ICodeConstants.Type_Byte:
				case ICodeConstants.Type_Bytechar:
				case ICodeConstants.Type_Short:
				case ICodeConstants.Type_Shortchar:
				case ICodeConstants.Type_Int:
				{
					return (int)value == 1;
				}

				case ICodeConstants.Type_Long:
				{
					return ((long)value) == 1;
				}

				case ICodeConstants.Type_Double:
				{
					return ((double)value) == 1;
				}

				case ICodeConstants.Type_Float:
				{
					return ((float)value) == 1;
				}
			}
			return false;
		}

		public static ConstExprent GetZeroConstant(int type)
		{
			switch (type)
			{
				case ICodeConstants.Type_Int:
				{
					return new ConstExprent(VarType.Vartype_Int, 0, null);
				}

				case ICodeConstants.Type_Long:
				{
					return new ConstExprent(VarType.Vartype_Long, 0L, null);
				}

				case ICodeConstants.Type_Double:
				{
					return new ConstExprent(VarType.Vartype_Double, 0d, null);
				}

				case ICodeConstants.Type_Float:
				{
					return new ConstExprent(VarType.Vartype_Float, 0f, null);
				}
			}
			throw new Exception("Invalid argument: " + type);
		}

		public virtual VarType GetConstType()
		{
			return constType;
		}

		public virtual void SetConstType(VarType constType)
		{
			this.constType = constType;
		}

		public virtual void AdjustConstType(VarType expectedType)
		{
			// BYTECHAR and SHORTCHAR => CHAR in the CHAR context
			if ((expectedType.Equals(VarType.Vartype_Char) || expectedType.Equals(VarType.Vartype_Character
				)) && (constType.Equals(VarType.Vartype_Bytechar) || constType.Equals(VarType.Vartype_Shortchar
				)))
			{
				int intValue = GetIntValue();
				if (IsPrintableAscii(intValue) || Char_Escapes.ContainsKey(intValue))
				{
					SetConstType(VarType.Vartype_Char);
				}
			}
			else if ((expectedType.Equals(VarType.Vartype_Int) || expectedType.Equals(VarType
				.Vartype_Integer)) && constType.typeFamily == ICodeConstants.Type_Family_Integer)
			{
				// BYTE, BYTECHAR, SHORTCHAR, SHORT, CHAR => INT in the INT context
				SetConstType(VarType.Vartype_Int);
			}
		}

		private static bool IsPrintableAscii(int c)
		{
			return c >= 32 && c < 127;
		}

		public virtual object GetValue()
		{
			return value;
		}

		public virtual int GetIntValue()
		{
			return (int)value;
		}

		public virtual bool IsBoolPermitted()
		{
			return boolPermitted;
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
			foreach (KeyValuePair<IMatchable.MatchProperties, MatchNode.RuleValue> rule in matchNode
				.GetRules())
			{
				MatchNode.RuleValue value = rule.Value;
				IMatchable.MatchProperties key = rule.Key;
				if (key == IMatchable.MatchProperties.Exprent_Consttype)
				{
					if (!value.value.Equals(this.constType))
					{
						return false;
					}
				}
				else if (key == IMatchable.MatchProperties.Exprent_Constvalue)
				{
					if (value.IsVariable() && !engine.CheckAndSetVariableValue(value.value.ToString()
						, this.value))
					{
						return false;
					}
				}
			}
			return true;
		}
	}
}
