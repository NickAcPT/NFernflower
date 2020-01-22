/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System.Collections.Generic;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class IfExprent : Exprent
	{
		public const int If_Eq = 0;

		public const int If_Ne = 1;

		public const int If_Lt = 2;

		public const int If_Ge = 3;

		public const int If_Gt = 4;

		public const int If_Le = 5;

		public const int If_Null = 6;

		public const int If_Nonnull = 7;

		public const int If_Icmpeq = 8;

		public const int If_Icmpne = 9;

		public const int If_Icmplt = 10;

		public const int If_Icmpge = 11;

		public const int If_Icmpgt = 12;

		public const int If_Icmple = 13;

		public const int If_Acmpeq = 14;

		public const int If_Acmpne = 15;

		public const int If_Value = 19;

		private static readonly int[] Func_Types = new int[] { FunctionExprent.Function_Eq
			, FunctionExprent.Function_Ne, FunctionExprent.Function_Lt, FunctionExprent.Function_Ge
			, FunctionExprent.Function_Gt, FunctionExprent.Function_Le, FunctionExprent.Function_Eq
			, FunctionExprent.Function_Ne, FunctionExprent.Function_Eq, FunctionExprent.Function_Ne
			, FunctionExprent.Function_Lt, FunctionExprent.Function_Ge, FunctionExprent.Function_Gt
			, FunctionExprent.Function_Le, FunctionExprent.Function_Eq, FunctionExprent.Function_Ne
			, FunctionExprent.Function_Cadd, FunctionExprent.Function_Cor, FunctionExprent.Function_Bool_Not
			, -1 };

		private Exprent condition;

		public IfExprent(int ifType, ListStack<Exprent> stack, HashSet<int> bytecodeOffsets
			)
			: this(null, bytecodeOffsets)
		{
			//public static final int IF_CAND = 16;
			//public static final int IF_COR = 17;
			//public static final int IF_NOT = 18;
			if (ifType <= If_Le)
			{
				stack.Push(new ConstExprent(0, true, null));
			}
			else if (ifType <= If_Nonnull)
			{
				stack.Push(new ConstExprent(VarType.Vartype_Null, null, null));
			}
			if (ifType == If_Value)
			{
				condition = stack.Pop();
			}
			else
			{
				condition = new FunctionExprent(Func_Types[ifType], stack, bytecodeOffsets);
			}
		}

		private IfExprent(Exprent condition, HashSet<int> bytecodeOffsets)
			: base(Exprent_If)
		{
			this.condition = condition;
			AddBytecodeOffsets(bytecodeOffsets);
		}

		public override Exprent Copy()
		{
			return new IfExprent(condition.Copy(), bytecode);
		}

		public override List<Exprent> GetAllExprents()
		{
			List<Exprent> lst = new List<Exprent>();
			lst.Add(condition);
			return lst;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			tracer.AddMapping(bytecode);
			return condition.ToJava(indent, tracer).Enclose("if (", ")");
		}

		public override void ReplaceExprent(Exprent oldExpr, Exprent newExpr)
		{
			if (oldExpr == condition)
			{
				condition = newExpr;
			}
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is IfExprent))
			{
				return false;
			}
			IfExprent ie = (IfExprent)o;
			return InterpreterUtil.EqualObjects(condition, ie.GetCondition());
		}

		public virtual IfExprent NegateIf()
		{
			condition = new FunctionExprent(FunctionExprent.Function_Bool_Not, condition, condition
				.bytecode);
			return this;
		}

		public virtual Exprent GetCondition()
		{
			return condition;
		}

		public virtual void SetCondition(Exprent condition)
		{
			this.condition = condition;
		}
	}
}
