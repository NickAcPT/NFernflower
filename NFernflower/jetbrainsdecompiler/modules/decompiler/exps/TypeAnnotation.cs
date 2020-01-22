// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class TypeAnnotation
	{
		public const int Class_Type_Parameter = unchecked((int)(0x00));

		public const int Method_Type_Parameter = unchecked((int)(0x01));

		public const int Super_Type_Reference = unchecked((int)(0x10));

		public const int Class_Type_Parameter_Bound = unchecked((int)(0x11));

		public const int Method_Type_Parameter_Bound = unchecked((int)(0x12));

		public const int Field = unchecked((int)(0x13));

		public const int Method_Return_Type = unchecked((int)(0x14));

		public const int Method_Receiver = unchecked((int)(0x15));

		public const int Method_Parameter = unchecked((int)(0x16));

		public const int Throws_Reference = unchecked((int)(0x17));

		public const int Local_Variable = unchecked((int)(0x40));

		public const int Resource_Variable = unchecked((int)(0x41));

		public const int Catch_Clause = unchecked((int)(0x42));

		public const int Expr_Instanceof = unchecked((int)(0x43));

		public const int Expr_New = unchecked((int)(0x44));

		public const int Expr_Constructor_Ref = unchecked((int)(0x45));

		public const int Expr_Method_Ref = unchecked((int)(0x46));

		public const int Type_Arg_Cast = unchecked((int)(0x47));

		public const int Type_Arg_Constructor_Call = unchecked((int)(0x48));

		public const int Type_Arg_Method_Call = unchecked((int)(0x49));

		public const int Type_Arg_Constructor_Ref = unchecked((int)(0x4A));

		public const int Type_Arg_Method_Ref = unchecked((int)(0x4B));

		private readonly int target;

		private readonly byte[] path;

		private readonly AnnotationExprent annotation;

		public TypeAnnotation(int target, byte[] path, AnnotationExprent annotation)
		{
			this.target = target;
			this.path = path;
			this.annotation = annotation;
		}

		public virtual int GetTargetType()
		{
			return target >> 24;
		}

		public virtual int GetIndex()
		{
			return target & unchecked((int)(0x0FFFF));
		}

		public virtual bool IsTopLevel()
		{
			return path == null;
		}

		public virtual AnnotationExprent GetAnnotation()
		{
			return annotation;
		}
	}
}
