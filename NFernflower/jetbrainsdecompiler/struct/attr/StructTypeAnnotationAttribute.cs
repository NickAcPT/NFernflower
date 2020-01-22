// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.IO;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using ObjectWeb.Misc.Java.IO;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructTypeAnnotationAttribute : StructGeneralAttribute
	{
		private List<TypeAnnotation> annotations = new System.Collections.Generic.List<TypeAnnotation
			>();

		/// <exception cref="IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			int len = data.ReadUnsignedShort();
			if (len > 0)
			{
				annotations = new List<TypeAnnotation>(len);
				for (int i = 0; i < len; i++)
				{
					annotations.Add(Parse(data, pool));
				}
			}
			else
			{
				annotations = new System.Collections.Generic.List<TypeAnnotation>();
			}
		}

		/// <exception cref="IOException"/>
		private static TypeAnnotation Parse(DataInputStream data, ConstantPool pool)
		{
			int targetType = data.ReadUnsignedByte();
			int target = targetType << 24;
			switch (targetType)
			{
				case TypeAnnotation.Class_Type_Parameter:
				case TypeAnnotation.Method_Type_Parameter:
				case TypeAnnotation.Method_Parameter:
				{
					target |= data.ReadUnsignedByte();
					break;
				}

				case TypeAnnotation.Super_Type_Reference:
				case TypeAnnotation.Class_Type_Parameter_Bound:
				case TypeAnnotation.Method_Type_Parameter_Bound:
				case TypeAnnotation.Throws_Reference:
				case TypeAnnotation.Catch_Clause:
				case TypeAnnotation.Expr_Instanceof:
				case TypeAnnotation.Expr_New:
				case TypeAnnotation.Expr_Constructor_Ref:
				case TypeAnnotation.Expr_Method_Ref:
				{
					target |= data.ReadUnsignedShort();
					break;
				}

				case TypeAnnotation.Type_Arg_Cast:
				case TypeAnnotation.Type_Arg_Constructor_Call:
				case TypeAnnotation.Type_Arg_Method_Call:
				case TypeAnnotation.Type_Arg_Constructor_Ref:
				case TypeAnnotation.Type_Arg_Method_Ref:
				{
					data.SkipBytes(3);
					break;
				}

				case TypeAnnotation.Local_Variable:
				case TypeAnnotation.Resource_Variable:
				{
					data.SkipBytes(data.ReadUnsignedShort() * 6);
					break;
				}

				case TypeAnnotation.Field:
				case TypeAnnotation.Method_Return_Type:
				case TypeAnnotation.Method_Receiver:
				{
					break;
				}

				default:
				{
					throw new Exception("unknown target type: " + targetType);
				}
			}
			int pathLength = data.ReadUnsignedByte();
			byte[] path = null;
			if (pathLength > 0)
			{
				path = new byte[2 * pathLength];
				data.ReadFully(path);
			}
			AnnotationExprent annotation = StructAnnotationAttribute.ParseAnnotation(data, pool
				);
			return new TypeAnnotation(target, path, annotation);
		}

		public virtual List<TypeAnnotation> GetAnnotations()
		{
			return annotations;
		}
	}
}
