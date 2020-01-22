/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class AssignmentExprent : Exprent
	{
		public const int Condition_None = -1;

		private static readonly string[] Operators = new string[] { " += ", " -= ", " *= "
			, " /= ", " &= ", " |= ", " ^= ", " %= ", " <<= ", " >>= ", " >>>= " };

		private Exprent left;

		private Exprent right;

		private int condType = Condition_None;

		public AssignmentExprent(Exprent left, Exprent right, HashSet<int> bytecodeOffsets
			)
			: base(Exprent_Assignment)
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
			this.left = left;
			this.right = right;
			AddBytecodeOffsets(bytecodeOffsets);
		}

		public override VarType GetExprType()
		{
			return left.GetExprType();
		}

		public override CheckTypesResult CheckExprTypeBounds()
		{
			CheckTypesResult result = new CheckTypesResult();
			VarType typeLeft = left.GetExprType();
			VarType typeRight = right.GetExprType();
			if (typeLeft.typeFamily > typeRight.typeFamily)
			{
				result.AddMinTypeExprent(right, VarType.GetMinTypeInFamily(typeLeft.typeFamily));
			}
			else if (typeLeft.typeFamily < typeRight.typeFamily)
			{
				result.AddMinTypeExprent(left, typeRight);
			}
			else
			{
				result.AddMinTypeExprent(left, VarType.GetCommonSupertype(typeLeft, typeRight));
			}
			return result;
		}

		public override List<Exprent> GetAllExprents()
		{
			List<Exprent> lst = new List<Exprent>();
			lst.Add(left);
			lst.Add(right);
			return lst;
		}

		public override Exprent Copy()
		{
			return new AssignmentExprent(left.Copy(), right.Copy(), bytecode);
		}

		public override int GetPrecedence()
		{
			return 13;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			VarType leftType = left.GetExprType();
			VarType rightType = right.GetExprType();
			bool fieldInClassInit = false;
			bool hiddenField = false;
			if (left.type == Exprent.Exprent_Field)
			{
				// first assignment to a final field. Field name without "this" in front of it
				FieldExprent field = (FieldExprent)left;
				ClassesProcessor.ClassNode node = ((ClassesProcessor.ClassNode)DecompilerContext.
					GetProperty(DecompilerContext.Current_Class_Node));
				if (node != null)
				{
					StructField fd = node.classStruct.GetField(field.GetName(), field.GetDescriptor()
						.descriptorString);
					if (fd != null)
					{
						if (field.IsStatic() && fd.HasModifier(ICodeConstants.Acc_Final))
						{
							fieldInClassInit = true;
						}
						if (node.GetWrapper() != null && node.GetWrapper().GetHiddenMembers().Contains(InterpreterUtil
							.MakeUniqueKey(fd.GetName(), fd.GetDescriptor())))
						{
							hiddenField = true;
						}
					}
				}
			}
			if (hiddenField)
			{
				return new TextBuffer();
			}
			TextBuffer buffer = new TextBuffer();
			if (fieldInClassInit)
			{
				buffer.Append(((FieldExprent)left).GetName());
			}
			else
			{
				buffer.Append(left.ToJava(indent, tracer));
			}
			if (right.type == Exprent_Const)
			{
				((ConstExprent)right).AdjustConstType(leftType);
			}
			TextBuffer res = right.ToJava(indent, tracer);
			if (condType == Condition_None && !leftType.IsSuperset(rightType) && (rightType.Equals
				(VarType.Vartype_Object) || leftType.type != ICodeConstants.Type_Object))
			{
				if (right.GetPrecedence() >= FunctionExprent.GetPrecedence(FunctionExprent.Function_Cast
					))
				{
					res.Enclose("(", ")");
				}
				res.Prepend("(" + ExprProcessor.GetCastTypeName(leftType) + ")");
			}
			buffer.Append(condType == Condition_None ? " = " : Operators[condType]).Append(res
				);
			tracer.AddMapping(bytecode);
			return buffer;
		}

		public override void ReplaceExprent(Exprent oldExpr, Exprent newExpr)
		{
			if (oldExpr == left)
			{
				left = newExpr;
			}
			if (oldExpr == right)
			{
				right = newExpr;
			}
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is AssignmentExprent))
			{
				return false;
			}
			AssignmentExprent @as = (AssignmentExprent)o;
			return InterpreterUtil.EqualObjects(left, @as.GetLeft()) && InterpreterUtil.EqualObjects
				(right, @as.GetRight()) && condType == @as.GetCondType();
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual Exprent GetLeft()
		{
			return left;
		}

		public virtual Exprent GetRight()
		{
			return right;
		}

		public virtual void SetRight(Exprent right)
		{
			this.right = right;
		}

		public virtual int GetCondType()
		{
			return condType;
		}

		public virtual void SetCondType(int condType)
		{
			this.condType = condType;
		}
	}
}
