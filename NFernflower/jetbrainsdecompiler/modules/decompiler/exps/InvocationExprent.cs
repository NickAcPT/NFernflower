// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.Reflection;
using Java.Util;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Struct.Match;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class InvocationExprent : Exprent
	{
		public const int Invoke_Special = 1;

		public const int Invoke_Virtual = 2;

		public const int Invoke_Static = 3;

		public const int Invoke_Interface = 4;

		public const int Invoke_Dynamic = 5;

		public const int Typ_General = 1;

		public const int Typ_Init = 2;

		public const int Typ_Clinit = 3;

		private static readonly BitSet Empty_Bit_Set = new BitSet(0);

		private string name;

		private string classname;

		private bool isStatic__;

		private bool canIgnoreBoxing = true;

		private int functype = Typ_General;

		private Exprent instance;

		private MethodDescriptor descriptor;

		private string stringDescriptor;

		private string invokeDynamicClassSuffix;

		private int invocationTyp = Invoke_Virtual;

		private List<Exprent> lstParameters = new List<Exprent>();

		private List<PooledConstant> bootstrapArguments;

		public InvocationExprent()
			: base(Exprent_Invocation)
		{
		}

		public InvocationExprent(int opcode, LinkConstant cn, List<PooledConstant> bootstrapArguments
			, ListStack<Exprent> stack, HashSet<int> bytecodeOffsets)
			: this()
		{
			name = cn.elementname;
			classname = cn.classname;
			this.bootstrapArguments = bootstrapArguments;
			switch (opcode)
			{
				case ICodeConstants.opc_invokestatic:
				{
					invocationTyp = Invoke_Static;
					break;
				}

				case ICodeConstants.opc_invokespecial:
				{
					invocationTyp = Invoke_Special;
					break;
				}

				case ICodeConstants.opc_invokevirtual:
				{
					invocationTyp = Invoke_Virtual;
					break;
				}

				case ICodeConstants.opc_invokeinterface:
				{
					invocationTyp = Invoke_Interface;
					break;
				}

				case ICodeConstants.opc_invokedynamic:
				{
					invocationTyp = Invoke_Dynamic;
					classname = "java/lang/Class";
					// dummy class name
					invokeDynamicClassSuffix = "##Lambda_" + cn.index1 + "_" + cn.index2;
					break;
				}
			}
			if (ICodeConstants.Init_Name.Equals(name))
			{
				functype = Typ_Init;
			}
			else if (ICodeConstants.Clinit_Name.Equals(name))
			{
				functype = Typ_Clinit;
			}
			stringDescriptor = cn.descriptor;
			descriptor = MethodDescriptor.ParseDescriptor(cn.descriptor);
			foreach (VarType ignored in descriptor.@params)
			{
				lstParameters.Add(0, stack.Pop());
			}
			if (opcode == ICodeConstants.opc_invokedynamic)
			{
				int dynamicInvocationType = -1;
				if (bootstrapArguments != null)
				{
					if (bootstrapArguments.Count > 1)
					{
						// INVOKEDYNAMIC is used not only for lambdas
						PooledConstant link = bootstrapArguments[1];
						if (link is LinkConstant)
						{
							dynamicInvocationType = ((LinkConstant)link).index1;
						}
					}
				}
				if (dynamicInvocationType == ICodeConstants.CONSTANT_MethodHandle_REF_invokeStatic)
				{
					isStatic__ = true;
				}
				else if (!(lstParameters.Count == 0))
				{
					// FIXME: remove the first parameter completely from the list. It's the object type for a virtual lambda method.
					instance = lstParameters[0];
				}
			}
			else if (opcode == ICodeConstants.opc_invokestatic)
			{
				isStatic__ = true;
			}
			else
			{
				instance = stack.Pop();
			}
			AddBytecodeOffsets(bytecodeOffsets);
		}

		private InvocationExprent(InvocationExprent expr)
			: this()
		{
			name = expr.GetName();
			classname = expr.GetClassname();
			isStatic__ = expr.IsStatic();
			canIgnoreBoxing = expr.canIgnoreBoxing;
			functype = expr.GetFunctype();
			instance = expr.GetInstance();
			if (instance != null)
			{
				instance = instance.Copy();
			}
			invocationTyp = expr.GetInvocationTyp();
			invokeDynamicClassSuffix = expr.GetInvokeDynamicClassSuffix();
			stringDescriptor = expr.GetStringDescriptor();
			descriptor = expr.GetDescriptor();
			lstParameters = new List<Exprent>(expr.GetLstParameters());
			ExprProcessor.CopyEntries(lstParameters);
			AddBytecodeOffsets(expr.bytecode);
			bootstrapArguments = expr.GetBootstrapArguments();
		}

		public override VarType GetExprType()
		{
			return descriptor.ret;
		}

		public override CheckTypesResult CheckExprTypeBounds()
		{
			CheckTypesResult result = new CheckTypesResult();
			for (int i = 0; i < lstParameters.Count; i++)
			{
				Exprent parameter = lstParameters[i];
				VarType leftType = descriptor.@params[i];
				result.AddMinTypeExprent(parameter, VarType.GetMinTypeInFamily(leftType.typeFamily
					));
				result.AddMaxTypeExprent(parameter, leftType);
			}
			return result;
		}

		public override List<Exprent> GetAllExprents()
		{
			List<Exprent> lst = new List<Exprent>();
			if (instance != null)
			{
				lst.Add(instance);
			}
			Sharpen.Collections.AddAll(lst, lstParameters);
			return lst;
		}

		public override Exprent Copy()
		{
			return new InvocationExprent(this);
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer buf = new TextBuffer();
			string super_qualifier = null;
			bool isInstanceThis = false;
			tracer.AddMapping(bytecode);
			if (instance is InvocationExprent)
			{
				((InvocationExprent)instance).MarkUsingBoxingResult();
			}
			if (isStatic__)
			{
				if (IsBoxingCall() && canIgnoreBoxing)
				{
					// process general "boxing" calls, e.g. 'Object[] data = { true }' or 'Byte b = 123'
					// here 'byte' and 'short' values do not need an explicit narrowing type cast
					ExprProcessor.GetCastedExprent(lstParameters[0], descriptor.@params[0], buf, indent
						, false, false, false, false, tracer);
					return buf;
				}
				ClassesProcessor.ClassNode node = (ClassesProcessor.ClassNode)DecompilerContext.GetProperty
					(DecompilerContext.Current_Class_Node);
				if (node == null || !classname.Equals(node.classStruct.qualifiedName))
				{
					buf.Append(DecompilerContext.GetImportCollector().GetShortNameInClassContext(ExprProcessor
						.BuildJavaClassName(classname)));
				}
			}
			else
			{
				if (instance != null && instance.type == Exprent.Exprent_Var)
				{
					VarExprent instVar = (VarExprent)instance;
					VarVersionPair varPair = new VarVersionPair(instVar);
					VarProcessor varProc = instVar.GetProcessor();
					if (varProc == null)
					{
						MethodWrapper currentMethod = (MethodWrapper)DecompilerContext.GetProperty(DecompilerContext
							.Current_Method_Wrapper);
						if (currentMethod != null)
						{
							varProc = currentMethod.varproc;
						}
					}
					string this_classname = null;
					if (varProc != null)
					{
						this_classname = varProc.GetThisVars().GetOrNull(varPair);
					}
					if (this_classname != null)
					{
						isInstanceThis = true;
						if (invocationTyp == Invoke_Special)
						{
							if (!classname.Equals(this_classname))
							{
								// TODO: direct comparison to the super class?
								StructClass cl = DecompilerContext.GetStructContext().GetClass(classname);
								bool isInterface = cl != null && cl.HasModifier(ICodeConstants.Acc_Interface);
								super_qualifier = !isInterface ? this_classname : classname;
							}
						}
					}
				}
				if (functype == Typ_General)
				{
					if (super_qualifier != null)
					{
						TextUtil.WriteQualifiedSuper(buf, super_qualifier);
					}
					else if (instance != null)
					{
						TextBuffer res = instance.ToJava(indent, tracer);
						if (IsUnboxingCall())
						{
							// we don't print the unboxing call - no need to bother with the instance wrapping / casting
							buf.Append(res);
							return buf;
						}
						VarType rightType = instance.GetExprType();
						VarType leftType = new VarType(ICodeConstants.Type_Object, 0, classname);
						if (rightType.Equals(VarType.Vartype_Object) && !leftType.Equals(rightType))
						{
							buf.Append("((").Append(ExprProcessor.GetCastTypeName(leftType)).Append(")");
							if (instance.GetPrecedence() >= FunctionExprent.GetPrecedence(FunctionExprent.Function_Cast
								))
							{
								res.Enclose("(", ")");
							}
							buf.Append(res).Append(")");
						}
						else if (instance.GetPrecedence() > GetPrecedence())
						{
							buf.Append("(").Append(res).Append(")");
						}
						else
						{
							buf.Append(res);
						}
					}
				}
			}
			switch (functype)
			{
				case Typ_General:
				{
					if (VarExprent.Var_Nameless_Enclosure.Equals(buf.ToString()))
					{
						buf = new TextBuffer();
					}
					if (buf.Length() > 0)
					{
						buf.Append(".");
					}
					buf.Append(name);
					if (invocationTyp == Invoke_Dynamic)
					{
						buf.Append("<invokedynamic>");
					}
					buf.Append("(");
					break;
				}

				case Typ_Clinit:
				{
					throw new Exception("Explicit invocation of " + ICodeConstants.Clinit_Name);
				}

				case Typ_Init:
				{
					if (super_qualifier != null)
					{
						buf.Append("super(");
					}
					else if (isInstanceThis)
					{
						buf.Append("this(");
					}
					else if (instance != null)
					{
						buf.Append(instance.ToJava(indent, tracer)).Append(".<init>(");
					}
					else
					{
						throw new Exception("Unrecognized invocation of " + ICodeConstants.Init_Name);
					}
					break;
				}
			}
			List<VarVersionPair> mask = null;
			bool isEnum = false;
			if (functype == Typ_Init)
			{
				ClassesProcessor.ClassNode newNode = DecompilerContext.GetClassProcessor().GetMapRootClasses
					().GetOrNull(classname);
				if (newNode != null)
				{
					mask = ExprUtil.GetSyntheticParametersMask(newNode, stringDescriptor, lstParameters
						.Count);
					isEnum = newNode.classStruct.HasModifier(ICodeConstants.Acc_Enum) && DecompilerContext
						.GetOption(IIFernflowerPreferences.Decompile_Enum);
				}
			}
			BitSet setAmbiguousParameters = GetAmbiguousParameters();
			// omit 'new Type[] {}' for the last parameter of a vararg method call
			if (lstParameters.Count == descriptor.@params.Length && IsVarArgCall())
			{
				Exprent lastParam = lstParameters[lstParameters.Count - 1];
				if (lastParam.type == Exprent_New && lastParam.GetExprType().arrayDim >= 1)
				{
					((NewExprent)lastParam).SetVarArgParam(true);
				}
			}
			bool firstParameter = true;
			int start = isEnum ? 2 : 0;
			for (int i = start; i < lstParameters.Count; i++)
			{
				if (mask == null || mask[i] == null)
				{
					TextBuffer buff = new TextBuffer();
					bool ambiguous = setAmbiguousParameters.Get(i);
					// 'byte' and 'short' literals need an explicit narrowing type cast when used as a parameter
					ExprProcessor.GetCastedExprent(lstParameters[i], descriptor.@params[i], buff, indent
						, true, ambiguous, true, true, tracer);
					// the last "new Object[0]" in the vararg call is not printed
					if (buff.Length() > 0)
					{
						if (!firstParameter)
						{
							buf.Append(", ");
						}
						buf.Append(buff);
					}
					firstParameter = false;
				}
			}
			buf.Append(')');
			return buf;
		}

		private bool IsVarArgCall()
		{
			StructClass cl = DecompilerContext.GetStructContext().GetClass(classname);
			if (cl != null)
			{
				StructMethod mt = cl.GetMethod(InterpreterUtil.MakeUniqueKey(name, stringDescriptor
					));
				if (mt != null)
				{
					return mt.HasModifier(ICodeConstants.Acc_Varargs);
				}
			}
			else
			{
				// TODO: tap into IDEA indices to access libraries methods details
				// try to check the class on the classpath
				MethodInfo mtd = ClasspathHelper.FindMethod(classname, name, descriptor);
				return mtd != null && mtd.IsVarArgs();
			}
			return false;
		}

		public virtual bool IsBoxingCall()
		{
			if (isStatic__ && "valueOf".Equals(name) && lstParameters.Count == 1)
			{
				int paramType = lstParameters[0].GetExprType().type;
				// special handling for ambiguous types
				if (lstParameters[0].type == Exprent.Exprent_Const)
				{
					// 'Integer.valueOf(1)' has '1' type detected as TYPE_BYTECHAR
					// 'Integer.valueOf(40_000)' has '40_000' type detected as TYPE_CHAR
					// so we check the type family instead
					if (lstParameters[0].GetExprType().typeFamily == ICodeConstants.Type_Family_Integer)
					{
						if (classname.Equals("java/lang/Integer"))
						{
							return true;
						}
					}
					if (paramType == ICodeConstants.Type_Bytechar || paramType == ICodeConstants.Type_Shortchar)
					{
						if (classname.Equals("java/lang/Character"))
						{
							return true;
						}
					}
				}
				return classname.Equals(GetClassNameForPrimitiveType(paramType));
			}
			return false;
		}

		public virtual void MarkUsingBoxingResult()
		{
			canIgnoreBoxing = false;
		}

		// TODO: move to CodeConstants ???
		private static string GetClassNameForPrimitiveType(int type)
		{
			switch (type)
			{
				case ICodeConstants.Type_Boolean:
				{
					return "java/lang/Boolean";
				}

				case ICodeConstants.Type_Byte:
				case ICodeConstants.Type_Bytechar:
				{
					return "java/lang/Byte";
				}

				case ICodeConstants.Type_Char:
				{
					return "java/lang/Character";
				}

				case ICodeConstants.Type_Short:
				case ICodeConstants.Type_Shortchar:
				{
					return "java/lang/Short";
				}

				case ICodeConstants.Type_Int:
				{
					return "java/lang/Integer";
				}

				case ICodeConstants.Type_Long:
				{
					return "java/lang/Long";
				}

				case ICodeConstants.Type_Float:
				{
					return "java/lang/Float";
				}

				case ICodeConstants.Type_Double:
				{
					return "java/lang/Double";
				}
			}
			return null;
		}

		private static readonly IDictionary<string, string> Unboxing_Methods;

		static InvocationExprent()
		{
			Unboxing_Methods = new Dictionary<string, string>();
			Sharpen.Collections.Put(Unboxing_Methods, "booleanValue", "java/lang/Boolean");
			Sharpen.Collections.Put(Unboxing_Methods, "byteValue", "java/lang/Byte");
			Sharpen.Collections.Put(Unboxing_Methods, "shortValue", "java/lang/Short");
			Sharpen.Collections.Put(Unboxing_Methods, "intValue", "java/lang/Integer");
			Sharpen.Collections.Put(Unboxing_Methods, "longValue", "java/lang/Long");
			Sharpen.Collections.Put(Unboxing_Methods, "floatValue", "java/lang/Float");
			Sharpen.Collections.Put(Unboxing_Methods, "doubleValue", "java/lang/Double");
			Sharpen.Collections.Put(Unboxing_Methods, "charValue", "java/lang/Character");
		}

		private bool IsUnboxingCall()
		{
			return !isStatic__ && lstParameters.Count == 0 && classname.Equals(Unboxing_Methods
				.GetOrNull(name));
		}

		private BitSet GetAmbiguousParameters()
		{
			StructClass cl = DecompilerContext.GetStructContext().GetClass(classname);
			if (cl == null)
			{
				return Empty_Bit_Set;
			}
			// check number of matches
			List<MethodDescriptor> matches = new List<MethodDescriptor>();
			foreach (StructMethod mt in cl.GetMethods())
			{
				if (name.Equals(mt.GetName()))
				{
					MethodDescriptor md = MethodDescriptor.ParseDescriptor(mt.GetDescriptor());
					if (md.@params.Length == descriptor.@params.Length)
					{
						for (int i = 0; i < md.@params.Length; i++)
						{
							if (md.@params[i].typeFamily != descriptor.@params[i].typeFamily)
							{
								goto nextMethod_continue;
							}
						}
						matches.Add(md);
					}
				}
			}
nextMethod_break: ;
			if (matches.Count == 1)
			{
				return Empty_Bit_Set;
			}
			// check if a call is unambiguous
			StructMethod mt_1 = cl.GetMethod(InterpreterUtil.MakeUniqueKey(name, stringDescriptor
				));
			if (mt_1 != null)
			{
				MethodDescriptor md = MethodDescriptor.ParseDescriptor(mt_1.GetDescriptor());
				if (md.@params.Length == lstParameters.Count)
				{
					bool exact = true;
					for (int i = 0; i < md.@params.Length; i++)
					{
						if (!md.@params[i].Equals(lstParameters[i].GetExprType()))
						{
							exact = false;
							break;
						}
					}
					if (exact)
					{
						return Empty_Bit_Set;
					}
				}
			}
			// mark parameters
			BitSet ambiguous = new BitSet(descriptor.@params.Length);
			for (int i = 0; i < descriptor.@params.Length; i++)
			{
				VarType paramType = descriptor.@params[i];
				foreach (MethodDescriptor md in matches)
				{
					if (!paramType.Equals(md.@params[i]))
					{
						ambiguous.Set(i);
						break;
					}
				}
			}
			return ambiguous;
		}

		public override void ReplaceExprent(Exprent oldExpr, Exprent newExpr)
		{
			if (oldExpr == instance)
			{
				instance = newExpr;
			}
			for (int i = 0; i < lstParameters.Count; i++)
			{
				if (oldExpr == lstParameters[i])
				{
					lstParameters[i] = newExpr;
				}
			}
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is InvocationExprent))
			{
				return false;
			}
			InvocationExprent it = (InvocationExprent)o;
			return InterpreterUtil.EqualObjects(name, it.GetName()) && InterpreterUtil.EqualObjects
				(classname, it.GetClassname()) && isStatic__ == it.IsStatic() && InterpreterUtil
				.EqualObjects(instance, it.GetInstance()) && InterpreterUtil.EqualObjects(descriptor
				, it.GetDescriptor()) && functype == it.GetFunctype() && InterpreterUtil.EqualLists
				(lstParameters, it.GetLstParameters());
		}

		public virtual List<Exprent> GetLstParameters()
		{
			return lstParameters;
		}

		public virtual void SetLstParameters(List<Exprent> lstParameters)
		{
			this.lstParameters = lstParameters;
		}

		public virtual MethodDescriptor GetDescriptor()
		{
			return descriptor;
		}

		public virtual void SetDescriptor(MethodDescriptor descriptor)
		{
			this.descriptor = descriptor;
		}

		public virtual string GetClassname()
		{
			return classname;
		}

		public virtual void SetClassname(string classname)
		{
			this.classname = classname;
		}

		public virtual int GetFunctype()
		{
			return functype;
		}

		public virtual void SetFunctype(int functype)
		{
			this.functype = functype;
		}

		public virtual Exprent GetInstance()
		{
			return instance;
		}

		public virtual void SetInstance(Exprent instance)
		{
			this.instance = instance;
		}

		public virtual bool IsStatic()
		{
			return isStatic__;
		}

		public virtual void SetStatic(bool isStatic)
		{
			this.isStatic__ = isStatic;
		}

		public virtual string GetName()
		{
			return name;
		}

		public virtual void SetName(string name)
		{
			this.name = name;
		}

		public virtual string GetStringDescriptor()
		{
			return stringDescriptor;
		}

		public virtual void SetStringDescriptor(string stringDescriptor)
		{
			this.stringDescriptor = stringDescriptor;
		}

		public virtual int GetInvocationTyp()
		{
			return invocationTyp;
		}

		public virtual string GetInvokeDynamicClassSuffix()
		{
			return invokeDynamicClassSuffix;
		}

		public virtual List<PooledConstant> GetBootstrapArguments()
		{
			return bootstrapArguments;
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
				if (key == IMatchable.MatchProperties.Exprent_Invocation_Parameter)
				{
					if (value.IsVariable() && (value.parameter >= lstParameters.Count || !engine.CheckAndSetVariableValue
						(value.value.ToString(), lstParameters[value.parameter])))
					{
						return false;
					}
				}
				else if (key == IMatchable.MatchProperties.Exprent_Invocation_Class)
				{
					if (!value.value.Equals(this.classname))
					{
						return false;
					}
				}
				else if (key == IMatchable.MatchProperties.Exprent_Invocation_Signature)
				{
					if (!value.value.Equals(this.name + this.stringDescriptor))
					{
						return false;
					}
				}
			}
			return true;
		}
	}
}
