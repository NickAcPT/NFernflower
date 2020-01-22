// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Struct.Match;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class ExitExprent : Exprent
	{
		public const int Exit_Return = 0;

		public const int Exit_Throw = 1;

		private readonly int exitType;

		private Exprent value;

		private readonly VarType retType;

		public ExitExprent(int exitType, Exprent value, VarType retType, HashSet<int> bytecodeOffsets
			)
			: base(Exprent_Exit)
		{
			this.exitType = exitType;
			this.value = value;
			this.retType = retType;
			AddBytecodeOffsets(bytecodeOffsets);
		}

		public override Exprent Copy()
		{
			return new ExitExprent(exitType, value == null ? null : value.Copy(), retType, bytecode
				);
		}

		public override CheckTypesResult CheckExprTypeBounds()
		{
			CheckTypesResult result = new CheckTypesResult();
			if (exitType == Exit_Return && retType.type != ICodeConstants.Type_Void)
			{
				result.AddMinTypeExprent(value, VarType.GetMinTypeInFamily(retType.typeFamily));
				result.AddMaxTypeExprent(value, retType);
			}
			return result;
		}

		public override List<Exprent> GetAllExprents()
		{
			List<Exprent> lst = new List<Exprent>();
			if (value != null)
			{
				lst.Add(value);
			}
			return lst;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			tracer.AddMapping(bytecode);
			if (exitType == Exit_Return)
			{
				TextBuffer buffer = new TextBuffer("return");
				if (retType.type != ICodeConstants.Type_Void)
				{
					buffer.Append(' ');
					ExprProcessor.GetCastedExprent(value, retType, buffer, indent, false, tracer);
				}
				return buffer;
			}
			else
			{
				MethodWrapper method = (MethodWrapper)DecompilerContext.GetProperty(DecompilerContext
					.Current_Method_Wrapper);
				ClassesProcessor.ClassNode node = ((ClassesProcessor.ClassNode)DecompilerContext.
					GetProperty(DecompilerContext.Current_Class_Node));
				if (method != null && node != null)
				{
					StructExceptionsAttribute attr = method.methodStruct.GetAttribute(StructGeneralAttribute
						.Attribute_Exceptions);
					if (attr != null)
					{
						string classname = null;
						for (int i = 0; i < attr.GetThrowsExceptions().Count; i++)
						{
							string exClassName = attr.GetExcClassname(i, node.classStruct.GetPool());
							if ("java/lang/Throwable".Equals(exClassName))
							{
								classname = exClassName;
								break;
							}
							else if ("java/lang/Exception".Equals(exClassName))
							{
								classname = exClassName;
							}
						}
						if (classname != null)
						{
							VarType exType = new VarType(classname, true);
							TextBuffer buffer = new TextBuffer("throw ");
							ExprProcessor.GetCastedExprent(value, exType, buffer, indent, false, tracer);
							return buffer;
						}
					}
				}
				return value.ToJava(indent, tracer).Prepend("throw ");
			}
		}

		public override void ReplaceExprent(Exprent oldExpr, Exprent newExpr)
		{
			if (oldExpr == value)
			{
				value = newExpr;
			}
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is ExitExprent))
			{
				return false;
			}
			ExitExprent et = (ExitExprent)o;
			return exitType == et.GetExitType() && InterpreterUtil.EqualObjects(value, et.GetValue
				());
		}

		public virtual int GetExitType()
		{
			return exitType;
		}

		public virtual Exprent GetValue()
		{
			return value;
		}

		public virtual VarType GetRetType()
		{
			return retType;
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
			int type = (int)matchNode.GetRuleValue(IMatchable.MatchProperties.Exprent_Exittype
				);
			return type == null || this.exitType == type;
		}
	}
}
