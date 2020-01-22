// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Struct.Gen.Generics;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class NewExprent : Exprent
	{
		private InvocationExprent constructor;

		private readonly VarType newType;

		private List<Exprent> lstDims = new List<Exprent>();

		private List<Exprent> lstArrayElements = new List<Exprent>();

		private bool directArrayInit;

		private bool isVarArgParam;

		private bool anonymous;

		private bool lambda;

		private bool enumConst;

		public NewExprent(VarType newType, ListStack<Exprent> stack, int arrayDim, HashSet
			<int> bytecodeOffsets)
			: this(newType, GetDimensions(arrayDim, stack), bytecodeOffsets)
		{
		}

		public NewExprent(VarType newType, List<Exprent> lstDims, HashSet<int> bytecodeOffsets
			)
			: base(Exprent_New)
		{
			this.newType = newType;
			this.lstDims = lstDims;
			anonymous = false;
			lambda = false;
			if (newType.type == ICodeConstants.Type_Object && newType.arrayDim == 0)
			{
				ClassesProcessor.ClassNode node = DecompilerContext.GetClassProcessor().GetMapRootClasses
					().GetOrNull(newType.value);
				if (node != null && (node.type == ClassesProcessor.ClassNode.Class_Anonymous || node
					.type == ClassesProcessor.ClassNode.Class_Lambda))
				{
					anonymous = true;
					if (node.type == ClassesProcessor.ClassNode.Class_Lambda)
					{
						lambda = true;
					}
				}
			}
			AddBytecodeOffsets(bytecodeOffsets);
		}

		private static List<Exprent> GetDimensions(int arrayDim, ListStack<Exprent> stack
			)
		{
			List<Exprent> lstDims = new List<Exprent>();
			for (int i = 0; i < arrayDim; i++)
			{
				lstDims.Add(0, stack.Pop());
			}
			return lstDims;
		}

		public override VarType GetExprType()
		{
			return anonymous ? DecompilerContext.GetClassProcessor().GetMapRootClasses().GetOrNull
				(newType.value).anonymousClassType : newType;
		}

		public override CheckTypesResult CheckExprTypeBounds()
		{
			CheckTypesResult result = new CheckTypesResult();
			if (newType.arrayDim != 0)
			{
				foreach (Exprent dim in lstDims)
				{
					result.AddMinTypeExprent(dim, VarType.Vartype_Bytechar);
					result.AddMaxTypeExprent(dim, VarType.Vartype_Int);
				}
				if (newType.arrayDim == 1)
				{
					VarType leftType = newType.DecreaseArrayDim();
					foreach (Exprent element in lstArrayElements)
					{
						result.AddMinTypeExprent(element, VarType.GetMinTypeInFamily(leftType.typeFamily)
							);
						result.AddMaxTypeExprent(element, leftType);
					}
				}
			}
			else if (constructor != null)
			{
				return constructor.CheckExprTypeBounds();
			}
			return result;
		}

		public override List<Exprent> GetAllExprents()
		{
			List<Exprent> lst = new List<Exprent>();
			if (newType.arrayDim != 0)
			{
				Sharpen.Collections.AddAll(lst, lstDims);
				Sharpen.Collections.AddAll(lst, lstArrayElements);
			}
			else if (constructor != null)
			{
				Exprent constructor = this.constructor.GetInstance();
				if (constructor != null)
				{
					// should be true only for a lambda expression with a virtual content method
					lst.Add(constructor);
				}
				Sharpen.Collections.AddAll(lst, this.constructor.GetLstParameters());
			}
			return lst;
		}

		public override Exprent Copy()
		{
			List<Exprent> lst = new List<Exprent>();
			foreach (Exprent expr in lstDims)
			{
				lst.Add(expr.Copy());
			}
			NewExprent ret = new NewExprent(newType, lst, bytecode);
			ret.SetConstructor(constructor == null ? null : (InvocationExprent)constructor.Copy
				());
			ret.SetLstArrayElements(lstArrayElements);
			ret.SetDirectArrayInit(directArrayInit);
			ret.SetAnonymous(anonymous);
			ret.SetEnumConst(enumConst);
			return ret;
		}

		public override int GetPrecedence()
		{
			return 1;
		}

		// precedence of new
		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer buf = new TextBuffer();
			if (anonymous)
			{
				ClassesProcessor.ClassNode child = DecompilerContext.GetClassProcessor().GetMapRootClasses
					().GetOrNull(newType.value);
				// IDEA-204310 - avoid backtracking later on for lambdas (causes spurious imports)
				if (!enumConst && (!lambda || DecompilerContext.GetOption(IFernflowerPreferences
					.Lambda_To_Anonymous_Class)))
				{
					string enclosing = null;
					if (!lambda && constructor != null)
					{
						enclosing = GetQualifiedNewInstance(child.anonymousClassType.value, constructor.GetLstParameters
							(), indent, tracer);
						if (enclosing != null)
						{
							buf.Append(enclosing).Append('.');
						}
					}
					buf.Append("new ");
					string typename = ExprProcessor.GetCastTypeName(child.anonymousClassType);
					if (enclosing != null)
					{
						ClassesProcessor.ClassNode anonymousNode = DecompilerContext.GetClassProcessor().
							GetMapRootClasses().GetOrNull(child.anonymousClassType.value);
						if (anonymousNode != null)
						{
							typename = anonymousNode.simpleName;
						}
						else
						{
							typename = Sharpen.Runtime.Substring(typename, typename.LastIndexOf('.') + 1);
						}
					}
					GenericClassDescriptor descriptor = ClassWriter.GetGenericClassDescriptor(child.classStruct
						);
					if (descriptor != null)
					{
						if ((descriptor.superinterfaces.Count == 0))
						{
							buf.Append(GenericMain.GetGenericCastTypeName(descriptor.superclass));
						}
						else
						{
							if (descriptor.superinterfaces.Count > 1 && !lambda)
							{
								DecompilerContext.GetLogger().WriteMessage("Inconsistent anonymous class signature: "
									 + child.classStruct.qualifiedName, IFernflowerLogger.Severity.Warn);
							}
							buf.Append(GenericMain.GetGenericCastTypeName(descriptor.superinterfaces[0]));
						}
					}
					else
					{
						buf.Append(typename);
					}
				}
				buf.Append('(');
				if (!lambda && constructor != null)
				{
					List<Exprent> parameters = constructor.GetLstParameters();
					List<VarVersionPair> mask = child.GetWrapper().GetMethodWrapper(ICodeConstants.Init_Name
						, constructor.GetStringDescriptor()).synthParameters;
					if (mask == null)
					{
						InvocationExprent superCall = child.superInvocation;
						mask = ExprUtil.GetSyntheticParametersMask(superCall.GetClassname(), superCall.GetStringDescriptor
							(), parameters.Count);
					}
					int start = enumConst ? 2 : 0;
					bool firstParam = true;
					for (int i = start; i < parameters.Count; i++)
					{
						if (mask == null || mask[i] == null)
						{
							if (!firstParam)
							{
								buf.Append(", ");
							}
							ExprProcessor.GetCastedExprent(parameters[i], constructor.GetDescriptor().@params
								[i], buf, indent, true, tracer);
							firstParam = false;
						}
					}
				}
				buf.Append(')');
				if (enumConst && buf.Length() == 2)
				{
					buf.SetLength(0);
				}
				if (lambda)
				{
					if (!DecompilerContext.GetOption(IFernflowerPreferences.Lambda_To_Anonymous_Class
						))
					{
						buf.SetLength(0);
					}
					// remove the usual 'new <class>()', it will be replaced with lambda style '() ->'
					Exprent methodObject = constructor == null ? null : constructor.GetInstance();
					TextBuffer clsBuf = new TextBuffer();
					new ClassWriter().ClassLambdaToJava(child, clsBuf, methodObject, indent, tracer);
					buf.Append(clsBuf);
					tracer.IncrementCurrentSourceLine(clsBuf.CountLines());
				}
				else
				{
					TextBuffer clsBuf = new TextBuffer();
					new ClassWriter().ClassToJava(child, clsBuf, indent, tracer);
					buf.Append(clsBuf);
					tracer.IncrementCurrentSourceLine(clsBuf.CountLines());
				}
			}
			else if (directArrayInit)
			{
				VarType leftType = newType.DecreaseArrayDim();
				buf.Append('{');
				for (int i = 0; i < lstArrayElements.Count; i++)
				{
					if (i > 0)
					{
						buf.Append(", ");
					}
					ExprProcessor.GetCastedExprent(lstArrayElements[i], leftType, buf, indent, false, 
						tracer);
				}
				buf.Append('}');
			}
			else if (newType.arrayDim == 0)
			{
				if (!enumConst)
				{
					string enclosing = null;
					if (constructor != null)
					{
						enclosing = GetQualifiedNewInstance(newType.value, constructor.GetLstParameters()
							, indent, tracer);
						if (enclosing != null)
						{
							buf.Append(enclosing).Append('.');
						}
					}
					buf.Append("new ");
					string typename = ExprProcessor.GetTypeName(newType);
					if (enclosing != null)
					{
						ClassesProcessor.ClassNode newNode = DecompilerContext.GetClassProcessor().GetMapRootClasses
							().GetOrNull(newType.value);
						if (newNode != null)
						{
							typename = newNode.simpleName;
						}
						else
						{
							typename = Sharpen.Runtime.Substring(typename, typename.LastIndexOf('.') + 1);
						}
					}
					buf.Append(typename);
				}
				if (constructor != null)
				{
					List<Exprent> parameters = constructor.GetLstParameters();
					List<VarVersionPair> mask = ExprUtil.GetSyntheticParametersMask(constructor.GetClassname
						(), constructor.GetStringDescriptor(), parameters.Count);
					int start = enumConst ? 2 : 0;
					if (!enumConst || start < parameters.Count)
					{
						buf.Append('(');
						bool firstParam = true;
						for (int i = start; i < parameters.Count; i++)
						{
							if (mask == null || mask[i] == null)
							{
								Exprent expr = parameters[i];
								VarType leftType = constructor.GetDescriptor().@params[i];
								if (i == parameters.Count - 1 && expr.GetExprType() == VarType.Vartype_Null && ProbablySyntheticParameter
									(leftType.value))
								{
									break;
								}
								// skip last parameter of synthetic constructor call
								if (!firstParam)
								{
									buf.Append(", ");
								}
								ExprProcessor.GetCastedExprent(expr, leftType, buf, indent, true, false, true, true
									, tracer);
								firstParam = false;
							}
						}
						buf.Append(')');
					}
				}
			}
			else if (isVarArgParam)
			{
				// just print the array elements
				VarType leftType = newType.DecreaseArrayDim();
				for (int i = 0; i < lstArrayElements.Count; i++)
				{
					if (i > 0)
					{
						buf.Append(", ");
					}
					// new String[][]{{"abc"}, {"DEF"}} => new String[]{"abc"}, new String[]{"DEF"}
					Exprent element = lstArrayElements[i];
					if (element.type == Exprent_New)
					{
						((NewExprent)element).SetDirectArrayInit(false);
					}
					ExprProcessor.GetCastedExprent(element, leftType, buf, indent, false, tracer);
				}
				// if there is just one element of Object[] type it needs to be casted to resolve ambiguity
				if (lstArrayElements.Count == 1)
				{
					VarType elementType = lstArrayElements[0].GetExprType();
					if (elementType.type == ICodeConstants.Type_Object && elementType.value.Equals("java/lang/Object"
						) && elementType.arrayDim >= 1)
					{
						buf.Prepend("(Object)");
					}
				}
			}
			else
			{
				buf.Append("new ").Append(ExprProcessor.GetTypeName(newType));
				if ((lstArrayElements.Count == 0))
				{
					for (int i = 0; i < newType.arrayDim; i++)
					{
						buf.Append('[');
						if (i < lstDims.Count)
						{
							buf.Append(lstDims[i].ToJava(indent, tracer));
						}
						buf.Append(']');
					}
				}
				else
				{
					for (int i = 0; i < newType.arrayDim; i++)
					{
						buf.Append("[]");
					}
					VarType leftType = newType.DecreaseArrayDim();
					buf.Append('{');
					for (int i = 0; i < lstArrayElements.Count; i++)
					{
						if (i > 0)
						{
							buf.Append(", ");
						}
						ExprProcessor.GetCastedExprent(lstArrayElements[i], leftType, buf, indent, false, 
							tracer);
					}
					buf.Append('}');
				}
			}
			return buf;
		}

		private static bool ProbablySyntheticParameter(string className)
		{
			ClassesProcessor.ClassNode node = DecompilerContext.GetClassProcessor().GetMapRootClasses
				().GetOrNull(className);
			return node != null && node.type == ClassesProcessor.ClassNode.Class_Anonymous;
		}

		private static string GetQualifiedNewInstance(string classname, List<Exprent> lstParams
			, int indent, BytecodeMappingTracer tracer)
		{
			ClassesProcessor.ClassNode node = DecompilerContext.GetClassProcessor().GetMapRootClasses
				().GetOrNull(classname);
			if (node != null && node.type != ClassesProcessor.ClassNode.Class_Root && node.type
				 != ClassesProcessor.ClassNode.Class_Local && (node.access & ICodeConstants.Acc_Static
				) == 0)
			{
				if (!(lstParams.Count == 0))
				{
					Exprent enclosing = lstParams[0];
					bool isQualifiedNew = false;
					if (enclosing.type == Exprent.Exprent_Var)
					{
						VarExprent varEnclosing = (VarExprent)enclosing;
						StructClass current_class = ((ClassesProcessor.ClassNode)DecompilerContext.GetProperty
							(DecompilerContext.Current_Class_Node)).classStruct;
						string this_classname = varEnclosing.GetProcessor().GetThisVars().GetOrNull(new VarVersionPair
							(varEnclosing));
						if (!current_class.qualifiedName.Equals(this_classname))
						{
							isQualifiedNew = true;
						}
					}
					else
					{
						isQualifiedNew = true;
					}
					if (isQualifiedNew)
					{
						return enclosing.ToJava(indent, tracer).ToString();
					}
				}
			}
			return null;
		}

		public override void ReplaceExprent(Exprent oldExpr, Exprent newExpr)
		{
			if (oldExpr == constructor)
			{
				constructor = (InvocationExprent)newExpr;
			}
			if (constructor != null)
			{
				constructor.ReplaceExprent(oldExpr, newExpr);
			}
			for (int i = 0; i < lstDims.Count; i++)
			{
				if (oldExpr == lstDims[i])
				{
					lstDims[i] = newExpr;
				}
			}
			for (int i = 0; i < lstArrayElements.Count; i++)
			{
				if (oldExpr == lstArrayElements[i])
				{
					lstArrayElements[i] = newExpr;
				}
			}
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is NewExprent))
			{
				return false;
			}
			NewExprent ne = (NewExprent)o;
			return InterpreterUtil.EqualObjects(newType, ne.GetNewType()) && InterpreterUtil.
				EqualLists(lstDims, ne.GetLstDims()) && InterpreterUtil.EqualObjects(constructor
				, ne.GetConstructor()) && directArrayInit == ne.directArrayInit && InterpreterUtil
				.EqualLists(lstArrayElements, ne.GetLstArrayElements());
		}

		public virtual InvocationExprent GetConstructor()
		{
			return constructor;
		}

		public virtual void SetConstructor(InvocationExprent constructor)
		{
			this.constructor = constructor;
		}

		public virtual List<Exprent> GetLstDims()
		{
			return lstDims;
		}

		public virtual VarType GetNewType()
		{
			return newType;
		}

		public virtual List<Exprent> GetLstArrayElements()
		{
			return lstArrayElements;
		}

		public virtual void SetLstArrayElements(List<Exprent> lstArrayElements)
		{
			this.lstArrayElements = lstArrayElements;
		}

		public virtual void SetDirectArrayInit(bool directArrayInit)
		{
			this.directArrayInit = directArrayInit;
		}

		public virtual void SetVarArgParam(bool isVarArgParam)
		{
			this.isVarArgParam = isVarArgParam;
		}

		public virtual bool IsLambda()
		{
			return lambda;
		}

		public virtual bool IsAnonymous()
		{
			return anonymous;
		}

		public virtual void SetAnonymous(bool anonymous)
		{
			this.anonymous = anonymous;
		}

		public virtual void SetEnumConst(bool enumConst)
		{
			this.enumConst = enumConst;
		}
	}
}
