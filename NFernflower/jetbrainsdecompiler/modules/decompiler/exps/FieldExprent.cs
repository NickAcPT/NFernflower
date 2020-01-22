/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Struct.Match;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class FieldExprent : Exprent
	{
		private readonly string name;

		private readonly string classname;

		private readonly bool isStatic__;

		private Exprent instance;

		private readonly FieldDescriptor descriptor;

		public FieldExprent(LinkConstant cn, Exprent instance, HashSet<int> bytecodeOffsets
			)
			: this(cn.elementname, cn.classname, instance == null, instance, FieldDescriptor.
				ParseDescriptor(cn.descriptor), bytecodeOffsets)
		{
		}

		public FieldExprent(string name, string classname, bool isStatic, Exprent instance
			, FieldDescriptor descriptor, HashSet<int> bytecodeOffsets)
			: base(Exprent_Field)
		{
			this.name = name;
			this.classname = classname;
			this.isStatic__ = isStatic;
			this.instance = instance;
			this.descriptor = descriptor;
			AddBytecodeOffsets(bytecodeOffsets);
		}

		public override VarType GetExprType()
		{
			return descriptor.type;
		}

		public override int GetExprentUse()
		{
			return 0;
		}

		// multiple references to a field considered dangerous in a multithreaded environment, thus no Exprent.MULTIPLE_USES set here
		public override List<Exprent> GetAllExprents()
		{
			List<Exprent> lst = new List<Exprent>();
			if (instance != null)
			{
				lst.Add(instance);
			}
			return lst;
		}

		public override Exprent Copy()
		{
			return new FieldExprent(name, classname, isStatic__, instance == null ? null : instance
				.Copy(), descriptor, bytecode);
		}

		private bool IsAmbiguous()
		{
			MethodWrapper method = (MethodWrapper)DecompilerContext.GetProperty(DecompilerContext
				.Current_Method_Wrapper);
			if (method != null)
			{
				StructLocalVariableTableAttribute attr = method.methodStruct.GetLocalVariableAttr
					();
				if (attr != null)
				{
					return attr.ContainsName(name);
				}
			}
			return false;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer buf = new TextBuffer();
			if (isStatic__)
			{
				ClassesProcessor.ClassNode node = (ClassesProcessor.ClassNode)DecompilerContext.GetProperty
					(DecompilerContext.Current_Class_Node);
				if (node == null || !classname.Equals(node.classStruct.qualifiedName) || IsAmbiguous
					())
				{
					buf.Append(DecompilerContext.GetImportCollector().GetShortNameInClassContext(ExprProcessor
						.BuildJavaClassName(classname)));
					buf.Append(".");
				}
			}
			else
			{
				string super_qualifier = null;
				if (instance != null && instance.type == Exprent.Exprent_Var)
				{
					VarExprent instVar = (VarExprent)instance;
					VarVersionPair pair = new VarVersionPair(instVar);
					MethodWrapper currentMethod = (MethodWrapper)DecompilerContext.GetProperty(DecompilerContext
						.Current_Method_Wrapper);
					if (currentMethod != null)
					{
						// FIXME: remove
						string this_classname = currentMethod.varproc.GetThisVars().GetOrNull(pair);
						if (this_classname != null)
						{
							if (!classname.Equals(this_classname))
							{
								// TODO: direct comparison to the super class?
								super_qualifier = this_classname;
							}
						}
					}
				}
				if (super_qualifier != null)
				{
					TextUtil.WriteQualifiedSuper(buf, super_qualifier);
				}
				else
				{
					TextBuffer buff = new TextBuffer();
					bool casted = ExprProcessor.GetCastedExprent(instance, new VarType(ICodeConstants
						.Type_Object, 0, classname), buff, indent, true, tracer);
					string res = buff.ToString();
					if (casted || instance.GetPrecedence() > GetPrecedence())
					{
						res = "(" + res + ")";
					}
					buf.Append(res);
				}
				if (buf.ToString().Equals(VarExprent.Var_Nameless_Enclosure))
				{
					// FIXME: workaround for field access of an anonymous enclosing class. Find a better way.
					buf.SetLength(0);
				}
				else
				{
					buf.Append(".");
				}
			}
			buf.Append(name);
			tracer.AddMapping(bytecode);
			return buf;
		}

		public override void ReplaceExprent(Exprent oldExpr, Exprent newExpr)
		{
			if (oldExpr == instance)
			{
				instance = newExpr;
			}
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is FieldExprent))
			{
				return false;
			}
			FieldExprent ft = (FieldExprent)o;
			return InterpreterUtil.EqualObjects(name, ft.GetName()) && InterpreterUtil.EqualObjects
				(classname, ft.GetClassname()) && isStatic__ == ft.IsStatic() && InterpreterUtil
				.EqualObjects(instance, ft.GetInstance()) && InterpreterUtil.EqualObjects(descriptor
				, ft.GetDescriptor());
		}

		public virtual string GetClassname()
		{
			return classname;
		}

		public virtual FieldDescriptor GetDescriptor()
		{
			return descriptor;
		}

		public virtual Exprent GetInstance()
		{
			return instance;
		}

		public virtual bool IsStatic()
		{
			return isStatic__;
		}

		public virtual string GetName()
		{
			return name;
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
			MatchNode.RuleValue rule = matchNode.GetRules().GetOrNull(IMatchable.MatchProperties
				.Exprent_Field_Name);
			if (rule != null)
			{
				if (rule.IsVariable())
				{
					return engine.CheckAndSetVariableValue((string)rule.value, this.name);
				}
				else
				{
					return rule.value.Equals(this.name);
				}
			}
			return true;
		}
	}
}
