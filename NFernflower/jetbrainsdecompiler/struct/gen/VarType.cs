// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Text;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Gen
{
	public class VarType
	{
		public static readonly VarType[] Empty_Array = new VarType[] {  };

		public static readonly VarType Vartype_Unknown = new VarType(ICodeConstants.Type_Unknown
			);

		public static readonly VarType Vartype_Int = new VarType(ICodeConstants.Type_Int);

		public static readonly VarType Vartype_Float = new VarType(ICodeConstants.Type_Float
			);

		public static readonly VarType Vartype_Long = new VarType(ICodeConstants.Type_Long
			);

		public static readonly VarType Vartype_Double = new VarType(ICodeConstants.Type_Double
			);

		public static readonly VarType Vartype_Byte = new VarType(ICodeConstants.Type_Byte
			);

		public static readonly VarType Vartype_Char = new VarType(ICodeConstants.Type_Char
			);

		public static readonly VarType Vartype_Short = new VarType(ICodeConstants.Type_Short
			);

		public static readonly VarType Vartype_Boolean = new VarType(ICodeConstants.Type_Boolean
			);

		public static readonly VarType Vartype_Bytechar = new VarType(ICodeConstants.Type_Bytechar
			);

		public static readonly VarType Vartype_Shortchar = new VarType(ICodeConstants.Type_Shortchar
			);

		public static readonly VarType Vartype_Null = new VarType(ICodeConstants.Type_Null
			, 0, null);

		public static readonly VarType Vartype_String = new VarType(ICodeConstants.Type_Object
			, 0, "java/lang/String");

		public static readonly VarType Vartype_Class = new VarType(ICodeConstants.Type_Object
			, 0, "java/lang/Class");

		public static readonly VarType Vartype_Object = new VarType(ICodeConstants.Type_Object
			, 0, "java/lang/Object");

		public static readonly VarType Vartype_Integer = new VarType(ICodeConstants.Type_Object
			, 0, "java/lang/Integer");

		public static readonly VarType Vartype_Character = new VarType(ICodeConstants.Type_Object
			, 0, "java/lang/Character");

		public static readonly VarType Vartype_Byte_Obj = new VarType(ICodeConstants.Type_Object
			, 0, "java/lang/Byte");

		public static readonly VarType Vartype_Short_Obj = new VarType(ICodeConstants.Type_Object
			, 0, "java/lang/Short");

		public static readonly VarType Vartype_Void = new VarType(ICodeConstants.Type_Void
			);

		public readonly int type;

		public readonly int arrayDim;

		public readonly string value;

		public readonly int typeFamily;

		public readonly int stackSize;

		public readonly bool falseBoolean;

		public VarType(int type)
			: this(type, 0)
		{
		}

		public VarType(int type, int arrayDim)
			: this(type, arrayDim, GetChar(type))
		{
		}

		public VarType(int type, int arrayDim, string value)
			: this(type, arrayDim, value, GetFamily(type, arrayDim), GetStackSize(type, arrayDim
				), false)
		{
		}

		private VarType(int type, int arrayDim, string value, int typeFamily, int stackSize
			, bool falseBoolean)
		{
			// TODO: optimize switch
			this.type = type;
			this.arrayDim = arrayDim;
			this.value = value;
			this.typeFamily = typeFamily;
			this.stackSize = stackSize;
			this.falseBoolean = falseBoolean;
		}

		public VarType(string signature)
			: this(signature, false)
		{
		}

		public VarType(string signature, bool clType)
		{
			int type = 0;
			int arrayDim = 0;
			string value = null;
			for (int i = 0; i < signature.Length; i++)
			{
				switch (signature[i])
				{
					case '[':
					{
						arrayDim++;
						break;
					}

					case 'L':
					{
						if (signature[signature.Length - 1] == ';')
						{
							type = ICodeConstants.Type_Object;
							value = Sharpen.Runtime.Substring(signature, i + 1, signature.Length - 1);
							goto loop_break;
						}
						goto default;
					}

					default:
					{
						value = Sharpen.Runtime.Substring(signature, i);
						if ((clType && i == 0) || value.Length > 1)
						{
							type = ICodeConstants.Type_Object;
						}
						else
						{
							type = GetType(value[0]);
						}
						goto loop_break;
					}
				}
loop_continue: ;
			}
loop_break: ;
			this.type = type;
			this.arrayDim = arrayDim;
			this.value = value;
			this.typeFamily = GetFamily(type, arrayDim);
			this.stackSize = GetStackSize(type, arrayDim);
			this.falseBoolean = false;
		}

		private static string GetChar(int type)
		{
			switch (type)
			{
				case ICodeConstants.Type_Byte:
				{
					return "B";
				}

				case ICodeConstants.Type_Char:
				{
					return "C";
				}

				case ICodeConstants.Type_Double:
				{
					return "D";
				}

				case ICodeConstants.Type_Float:
				{
					return "F";
				}

				case ICodeConstants.Type_Int:
				{
					return "I";
				}

				case ICodeConstants.Type_Long:
				{
					return "J";
				}

				case ICodeConstants.Type_Short:
				{
					return "S";
				}

				case ICodeConstants.Type_Boolean:
				{
					return "Z";
				}

				case ICodeConstants.Type_Void:
				{
					return "V";
				}

				case ICodeConstants.Type_Group2empty:
				{
					return "G";
				}

				case ICodeConstants.Type_Notinitialized:
				{
					return "N";
				}

				case ICodeConstants.Type_Address:
				{
					return "A";
				}

				case ICodeConstants.Type_Bytechar:
				{
					return "X";
				}

				case ICodeConstants.Type_Shortchar:
				{
					return "Y";
				}

				case ICodeConstants.Type_Unknown:
				{
					return "U";
				}

				case ICodeConstants.Type_Null:
				case ICodeConstants.Type_Object:
				{
					return null;
				}

				default:
				{
					throw new Exception("Invalid type");
				}
			}
		}

		private static int GetStackSize(int type, int arrayDim)
		{
			if (arrayDim > 0)
			{
				return 1;
			}
			switch (type)
			{
				case ICodeConstants.Type_Double:
				case ICodeConstants.Type_Long:
				{
					return 2;
				}

				case ICodeConstants.Type_Void:
				case ICodeConstants.Type_Group2empty:
				{
					return 0;
				}

				default:
				{
					return 1;
				}
			}
		}

		private static int GetFamily(int type, int arrayDim)
		{
			if (arrayDim > 0)
			{
				return ICodeConstants.Type_Family_Object;
			}
			switch (type)
			{
				case ICodeConstants.Type_Byte:
				case ICodeConstants.Type_Bytechar:
				case ICodeConstants.Type_Shortchar:
				case ICodeConstants.Type_Char:
				case ICodeConstants.Type_Short:
				case ICodeConstants.Type_Int:
				{
					return ICodeConstants.Type_Family_Integer;
				}

				case ICodeConstants.Type_Double:
				{
					return ICodeConstants.Type_Family_Double;
				}

				case ICodeConstants.Type_Float:
				{
					return ICodeConstants.Type_Family_Float;
				}

				case ICodeConstants.Type_Long:
				{
					return ICodeConstants.Type_Family_Long;
				}

				case ICodeConstants.Type_Boolean:
				{
					return ICodeConstants.Type_Family_Boolean;
				}

				case ICodeConstants.Type_Null:
				case ICodeConstants.Type_Object:
				{
					return ICodeConstants.Type_Family_Object;
				}

				default:
				{
					return ICodeConstants.Type_Family_Unknown;
				}
			}
		}

		public virtual VarType DecreaseArrayDim()
		{
			if (arrayDim > 0)
			{
				return new VarType(type, arrayDim - 1, value);
			}
			else
			{
				//throw new RuntimeException("array dimension equals 0!"); FIXME: investigate this case
				return this;
			}
		}

		public virtual VarType ResizeArrayDim(int newArrayDim)
		{
			return new VarType(type, newArrayDim, value, typeFamily, stackSize, falseBoolean);
		}

		public virtual VarType Copy()
		{
			return Copy(false);
		}

		public virtual VarType Copy(bool forceFalseBoolean)
		{
			return new VarType(type, arrayDim, value, typeFamily, stackSize, falseBoolean || 
				forceFalseBoolean);
		}

		public virtual bool IsFalseBoolean()
		{
			return falseBoolean;
		}

		public virtual bool IsSuperset(VarType val)
		{
			return this.Equals(val) || this.IsStrictSuperset(val);
		}

		public virtual bool IsStrictSuperset(VarType val)
		{
			int valType = val.type;
			if (valType == ICodeConstants.Type_Unknown && type != ICodeConstants.Type_Unknown)
			{
				return true;
			}
			if (val.arrayDim > 0)
			{
				return this.Equals(Vartype_Object);
			}
			else if (arrayDim > 0)
			{
				return (valType == ICodeConstants.Type_Null);
			}
			bool res = false;
			switch (type)
			{
				case ICodeConstants.Type_Int:
				{
					res = (valType == ICodeConstants.Type_Short || valType == ICodeConstants.Type_Char
						);
					goto case ICodeConstants.Type_Short;
				}

				case ICodeConstants.Type_Short:
				{
					res |= (valType == ICodeConstants.Type_Byte);
					goto case ICodeConstants.Type_Char;
				}

				case ICodeConstants.Type_Char:
				{
					res |= (valType == ICodeConstants.Type_Shortchar);
					goto case ICodeConstants.Type_Byte;
				}

				case ICodeConstants.Type_Byte:
				case ICodeConstants.Type_Shortchar:
				{
					res |= (valType == ICodeConstants.Type_Bytechar);
					goto case ICodeConstants.Type_Bytechar;
				}

				case ICodeConstants.Type_Bytechar:
				{
					res |= (valType == ICodeConstants.Type_Boolean);
					break;
				}

				case ICodeConstants.Type_Object:
				{
					if (valType == ICodeConstants.Type_Null)
					{
						return true;
					}
					else if (this.Equals(Vartype_Object))
					{
						return valType == ICodeConstants.Type_Object && !val.Equals(Vartype_Object);
					}
					break;
				}
			}
			return res;
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is VarType))
			{
				return false;
			}
			VarType vt = (VarType)o;
			return type == vt.type && arrayDim == vt.arrayDim && InterpreterUtil.EqualObjects
				(value, vt.value);
		}

		public override string ToString()
		{
			StringBuilder res = new StringBuilder();
			for (int i = 0; i < arrayDim; i++)
			{
				res.Append('[');
			}
			if (type == ICodeConstants.Type_Object)
			{
				res.Append('L').Append(value).Append(';');
			}
			else
			{
				res.Append(value);
			}
			return res.ToString();
		}

		// type1 and type2 must not be null
		public static VarType GetCommonMinType(VarType type1, VarType type2)
		{
			if (type1.type == ICodeConstants.Type_Boolean && type2.type == ICodeConstants.Type_Boolean)
			{
				// special case booleans
				return type1.IsFalseBoolean() ? type2 : type1;
			}
			if (type1.IsSuperset(type2))
			{
				return type2;
			}
			else if (type2.IsSuperset(type1))
			{
				return type1;
			}
			else if (type1.typeFamily == type2.typeFamily)
			{
				switch (type1.typeFamily)
				{
					case ICodeConstants.Type_Family_Integer:
					{
						if ((type1.type == ICodeConstants.Type_Char && type2.type == ICodeConstants.Type_Short
							) || (type1.type == ICodeConstants.Type_Short && type2.type == ICodeConstants.Type_Char
							))
						{
							return Vartype_Shortchar;
						}
						else
						{
							return Vartype_Bytechar;
						}
						goto case ICodeConstants.Type_Family_Object;
					}

					case ICodeConstants.Type_Family_Object:
					{
						return Vartype_Null;
					}
				}
			}
			return null;
		}

		// type1 and type2 must not be null
		public static VarType GetCommonSupertype(VarType type1, VarType type2)
		{
			if (type1.type == ICodeConstants.Type_Boolean && type2.type == ICodeConstants.Type_Boolean)
			{
				// special case booleans
				return type1.IsFalseBoolean() ? type1 : type2;
			}
			if (type1.IsSuperset(type2))
			{
				return type1;
			}
			else if (type2.IsSuperset(type1))
			{
				return type2;
			}
			else if (type1.typeFamily == type2.typeFamily)
			{
				switch (type1.typeFamily)
				{
					case ICodeConstants.Type_Family_Integer:
					{
						if ((type1.type == ICodeConstants.Type_Shortchar && type2.type == ICodeConstants.
							Type_Byte) || (type1.type == ICodeConstants.Type_Byte && type2.type == ICodeConstants
							.Type_Shortchar))
						{
							return Vartype_Short;
						}
						else
						{
							return Vartype_Int;
						}
						goto case ICodeConstants.Type_Family_Object;
					}

					case ICodeConstants.Type_Family_Object:
					{
						return Vartype_Object;
					}
				}
			}
			return null;
		}

		public static VarType GetMinTypeInFamily(int family)
		{
			switch (family)
			{
				case ICodeConstants.Type_Family_Boolean:
				{
					return Vartype_Boolean;
				}

				case ICodeConstants.Type_Family_Integer:
				{
					return Vartype_Bytechar;
				}

				case ICodeConstants.Type_Family_Object:
				{
					return Vartype_Null;
				}

				case ICodeConstants.Type_Family_Float:
				{
					return Vartype_Float;
				}

				case ICodeConstants.Type_Family_Long:
				{
					return Vartype_Long;
				}

				case ICodeConstants.Type_Family_Double:
				{
					return Vartype_Double;
				}

				case ICodeConstants.Type_Family_Unknown:
				{
					return Vartype_Unknown;
				}

				default:
				{
					throw new ArgumentException("Invalid type family: " + family);
				}
			}
		}

		public static int GetType(char c)
		{
			switch (c)
			{
				case 'B':
				{
					return ICodeConstants.Type_Byte;
				}

				case 'C':
				{
					return ICodeConstants.Type_Char;
				}

				case 'D':
				{
					return ICodeConstants.Type_Double;
				}

				case 'F':
				{
					return ICodeConstants.Type_Float;
				}

				case 'I':
				{
					return ICodeConstants.Type_Int;
				}

				case 'J':
				{
					return ICodeConstants.Type_Long;
				}

				case 'S':
				{
					return ICodeConstants.Type_Short;
				}

				case 'Z':
				{
					return ICodeConstants.Type_Boolean;
				}

				case 'V':
				{
					return ICodeConstants.Type_Void;
				}

				case 'G':
				{
					return ICodeConstants.Type_Group2empty;
				}

				case 'N':
				{
					return ICodeConstants.Type_Notinitialized;
				}

				case 'A':
				{
					return ICodeConstants.Type_Address;
				}

				case 'X':
				{
					return ICodeConstants.Type_Bytechar;
				}

				case 'Y':
				{
					return ICodeConstants.Type_Shortchar;
				}

				case 'U':
				{
					return ICodeConstants.Type_Unknown;
				}

				default:
				{
					throw new ArgumentException("Invalid type: " + c);
				}
			}
		}
	}
}
