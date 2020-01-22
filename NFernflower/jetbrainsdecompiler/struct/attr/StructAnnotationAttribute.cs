// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using Java.IO;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructAnnotationAttribute : StructGeneralAttribute
	{
		private List<AnnotationExprent> annotations;

		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			annotations = ParseAnnotations(pool, data);
		}

		/// <exception cref="System.IO.IOException"/>
		public static List<AnnotationExprent> ParseAnnotations(ConstantPool pool, DataInputStream
			 data)
		{
			int len = data.ReadUnsignedShort();
			if (len > 0)
			{
				List<AnnotationExprent> annotations = new List<AnnotationExprent>(len);
				for (int i = 0; i < len; i++)
				{
					annotations.Add(ParseAnnotation(data, pool));
				}
				return annotations;
			}
			else
			{
				return new System.Collections.Generic.List<AnnotationExprent>();
			}
		}

		/// <exception cref="System.IO.IOException"/>
		public static AnnotationExprent ParseAnnotation(DataInputStream data, ConstantPool
			 pool)
		{
			string className = pool.GetPrimitiveConstant(data.ReadUnsignedShort()).GetString(
				);
			List<string> names;
			List<Exprent> values;
			int len = data.ReadUnsignedShort();
			if (len > 0)
			{
				names = new List<string>(len);
				values = new List<Exprent>(len);
				for (int i = 0; i < len; i++)
				{
					names.Add(pool.GetPrimitiveConstant(data.ReadUnsignedShort()).GetString());
					values.Add(ParseAnnotationElement(data, pool));
				}
			}
			else
			{
				names = new System.Collections.Generic.List<string>();
				values = new System.Collections.Generic.List<Exprent>();
			}
			return new AnnotationExprent(new VarType(className).value, names, values);
		}

		/// <exception cref="System.IO.IOException"/>
		public static Exprent ParseAnnotationElement(DataInputStream data, ConstantPool pool
			)
		{
			int tag = data.ReadUnsignedByte();
			switch (tag)
			{
				case 'e':
				{
					// enum constant
					string className = pool.GetPrimitiveConstant(data.ReadUnsignedShort()).GetString(
						);
					string constName = pool.GetPrimitiveConstant(data.ReadUnsignedShort()).GetString(
						);
					FieldDescriptor descr = FieldDescriptor.ParseDescriptor(className);
					return new FieldExprent(constName, descr.type.value, true, null, descr, null);
				}

				case 'c':
				{
					// class
					string descriptor = pool.GetPrimitiveConstant(data.ReadUnsignedShort()).GetString
						();
					VarType type = FieldDescriptor.ParseDescriptor(descriptor).type;
					string value;
					switch (type.type)
					{
						case ICodeConstants.Type_Object:
						{
							value = type.value;
							break;
						}

						case ICodeConstants.Type_Byte:
						{
							value = typeof(byte).FullName;
							break;
						}

						case ICodeConstants.Type_Char:
						{
							value = typeof(char).FullName;
							break;
						}

						case ICodeConstants.Type_Double:
						{
							value = typeof(double).FullName;
							break;
						}

						case ICodeConstants.Type_Float:
						{
							value = typeof(float).FullName;
							break;
						}

						case ICodeConstants.Type_Int:
						{
							value = typeof(int).FullName;
							break;
						}

						case ICodeConstants.Type_Long:
						{
							value = typeof(long).FullName;
							break;
						}

						case ICodeConstants.Type_Short:
						{
							value = typeof(short).FullName;
							break;
						}

						case ICodeConstants.Type_Boolean:
						{
							value = typeof(bool).FullName;
							break;
						}

						case ICodeConstants.Type_Void:
						{
							value = typeof(void).FullName;
							break;
						}

						default:
						{
							throw new Exception("invalid class type: " + type.type);
						}
					}
					return new ConstExprent(VarType.Vartype_Class, value, null);
				}

				case '[':
				{
					// array
					List<Exprent> elements = new System.Collections.Generic.List<Exprent>();
					int len = data.ReadUnsignedShort();
					if (len > 0)
					{
						elements = new List<Exprent>(len);
						for (int i = 0; i < len; i++)
						{
							elements.Add(ParseAnnotationElement(data, pool));
						}
					}
					VarType newType;
					if ((elements.Count == 0))
					{
						newType = new VarType(ICodeConstants.Type_Object, 1, "java/lang/Object");
					}
					else
					{
						VarType elementType = elements[0].GetExprType();
						newType = new VarType(elementType.type, 1, elementType.value);
					}
					NewExprent newExpr = new NewExprent(newType, new System.Collections.Generic.List<
						Exprent>(), null);
					newExpr.SetDirectArrayInit(true);
					newExpr.SetLstArrayElements(elements);
					return newExpr;
				}

				case '@':
				{
					// annotation
					return ParseAnnotation(data, pool);
				}

				default:
				{
					PrimitiveConstant cn = pool.GetPrimitiveConstant(data.ReadUnsignedShort());
					switch (tag)
					{
						case 'B':
						{
							return new ConstExprent(VarType.Vartype_Byte, cn.value, null);
						}

						case 'C':
						{
							return new ConstExprent(VarType.Vartype_Char, cn.value, null);
						}

						case 'D':
						{
							return new ConstExprent(VarType.Vartype_Double, cn.value, null);
						}

						case 'F':
						{
							return new ConstExprent(VarType.Vartype_Float, cn.value, null);
						}

						case 'I':
						{
							return new ConstExprent(VarType.Vartype_Int, cn.value, null);
						}

						case 'J':
						{
							return new ConstExprent(VarType.Vartype_Long, cn.value, null);
						}

						case 'S':
						{
							return new ConstExprent(VarType.Vartype_Short, cn.value, null);
						}

						case 'Z':
						{
							return new ConstExprent(VarType.Vartype_Boolean, cn.value, null);
						}

						case 's':
						{
							return new ConstExprent(VarType.Vartype_String, cn.value, null);
						}

						default:
						{
							throw new Exception("invalid element type!");
						}
					}
					break;
				}
			}
		}

		public virtual List<AnnotationExprent> GetAnnotations()
		{
			return annotations;
		}
	}
}
