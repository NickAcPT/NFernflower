// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class ArrayExprent : Exprent
	{
		private Exprent array;

		private Exprent index;

		private readonly VarType hardType;

		public ArrayExprent(Exprent array, Exprent index, VarType hardType, HashSet<int> 
			bytecodeOffsets)
			: base(Exprent_Array)
		{
			this.array = array;
			this.index = index;
			this.hardType = hardType;
			AddBytecodeOffsets(bytecodeOffsets);
		}

		public override Exprent Copy()
		{
			return new ArrayExprent(array.Copy(), index.Copy(), hardType, bytecode);
		}

		public override VarType GetExprType()
		{
			VarType exprType = array.GetExprType();
			if (exprType.Equals(VarType.Vartype_Null))
			{
				return hardType.Copy();
			}
			else
			{
				return exprType.DecreaseArrayDim();
			}
		}

		public override int GetExprentUse()
		{
			return array.GetExprentUse() & index.GetExprentUse() & Exprent.Multiple_Uses;
		}

		public override CheckTypesResult CheckExprTypeBounds()
		{
			CheckTypesResult result = new CheckTypesResult();
			result.AddMinTypeExprent(index, VarType.Vartype_Bytechar);
			result.AddMaxTypeExprent(index, VarType.Vartype_Int);
			return result;
		}

		public override List<Exprent> GetAllExprents()
		{
			List<Exprent> lst = new List<Exprent>();
			lst.Add(array);
			lst.Add(index);
			return lst;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer res = array.ToJava(indent, tracer);
			if (array.GetPrecedence() > GetPrecedence())
			{
				// array precedence equals 0
				res.Enclose("(", ")");
			}
			VarType arrType = array.GetExprType();
			if (arrType.arrayDim == 0)
			{
				VarType objArr = VarType.Vartype_Object.ResizeArrayDim(1);
				// type family does not change
				res.Enclose("((" + ExprProcessor.GetCastTypeName(objArr) + ")", ")");
			}
			tracer.AddMapping(bytecode);
			return res.Append('[').Append(index.ToJava(indent, tracer)).Append(']');
		}

		public override void ReplaceExprent(Exprent oldExpr, Exprent newExpr)
		{
			if (oldExpr == array)
			{
				array = newExpr;
			}
			if (oldExpr == index)
			{
				index = newExpr;
			}
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is ArrayExprent))
			{
				return false;
			}
			ArrayExprent arr = (ArrayExprent)o;
			return InterpreterUtil.EqualObjects(array, arr.GetArray()) && InterpreterUtil.EqualObjects
				(index, arr.GetIndex());
		}

		public virtual Exprent GetArray()
		{
			return array;
		}

		public virtual Exprent GetIndex()
		{
			return index;
		}
	}
}
