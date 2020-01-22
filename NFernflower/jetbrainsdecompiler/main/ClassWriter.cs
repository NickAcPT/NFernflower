// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Java.Util;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Modules.Renamer;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Struct.Gen.Generics;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main
{
	public class ClassWriter
	{
		private readonly PoolInterceptor interceptor;

		public ClassWriter()
		{
			interceptor = DecompilerContext.GetPoolInterceptor();
		}

		private static void InvokeProcessors(ClassesProcessor.ClassNode node)
		{
			ClassWrapper wrapper = node.GetWrapper();
			StructClass cl = wrapper.GetClassStruct();
			InitializerProcessor.ExtractInitializers(wrapper);
			if (node.type == ClassesProcessor.ClassNode.Class_Root && !cl.IsVersionGE_1_5() &&
				 DecompilerContext.GetOption(IFernflowerPreferences.Decompile_Class_1_4))
			{
				ClassReference14Processor.ProcessClassReferences(node);
			}
			if (cl.HasModifier(ICodeConstants.Acc_Enum) && DecompilerContext.GetOption(IFernflowerPreferences
				.Decompile_Enum))
			{
				EnumProcessor.ClearEnum(wrapper);
			}
			if (DecompilerContext.GetOption(IFernflowerPreferences.Decompile_Assertions))
			{
				AssertProcessor.BuildAssertions(node);
			}
		}

		public virtual void ClassLambdaToJava(ClassesProcessor.ClassNode node, TextBuffer
			 buffer, Exprent method_object, int indent, BytecodeMappingTracer origTracer)
		{
			ClassWrapper wrapper = node.GetWrapper();
			if (wrapper == null)
			{
				return;
			}
			bool lambdaToAnonymous = DecompilerContext.GetOption(IFernflowerPreferences.Lambda_To_Anonymous_Class
				);
			ClassesProcessor.ClassNode outerNode = (ClassesProcessor.ClassNode)DecompilerContext
				.GetProperty(DecompilerContext.Current_Class_Node);
			DecompilerContext.SetProperty(DecompilerContext.Current_Class_Node, node);
			BytecodeMappingTracer tracer = new BytecodeMappingTracer(origTracer.GetCurrentSourceLine
				());
			try
			{
				StructClass cl = wrapper.GetClassStruct();
				DecompilerContext.GetLogger().StartWriteClass(node.simpleName);
				if (node.lambdaInformation.is_method_reference)
				{
					if (!node.lambdaInformation.is_content_method_static && method_object != null)
					{
						// reference to a virtual method
						buffer.Append(method_object.ToJava(indent, tracer));
					}
					else
					{
						// reference to a static method
						buffer.Append(ExprProcessor.GetCastTypeName(new VarType(node.lambdaInformation.content_class_name
							, true)));
					}
					buffer.Append("::").Append(ICodeConstants.Init_Name.Equals(node.lambdaInformation
						.content_method_name) ? "new" : node.lambdaInformation.content_method_name);
				}
				else
				{
					// lambda method
					StructMethod mt = cl.GetMethod(node.lambdaInformation.content_method_key);
					MethodWrapper methodWrapper = wrapper.GetMethodWrapper(mt.GetName(), mt.GetDescriptor
						());
					MethodDescriptor md_content = MethodDescriptor.ParseDescriptor(node.lambdaInformation
						.content_method_descriptor);
					MethodDescriptor md_lambda = MethodDescriptor.ParseDescriptor(node.lambdaInformation
						.method_descriptor);
					if (!lambdaToAnonymous)
					{
						buffer.Append('(');
						bool firstParameter = true;
						int index = node.lambdaInformation.is_content_method_static ? 0 : 1;
						int start_index = md_content.@params.Length - md_lambda.@params.Length;
						for (int i = 0; i < md_content.@params.Length; i++)
						{
							if (i >= start_index)
							{
								if (!firstParameter)
								{
									buffer.Append(", ");
								}
								string parameterName = methodWrapper.varproc.GetVarName(new VarVersionPair(index, 
									0));
								buffer.Append(parameterName == null ? "param" + index : parameterName);
								// null iff decompiled with errors
								firstParameter = false;
							}
							index += md_content.@params[i].stackSize;
						}
						buffer.Append(") ->");
					}
					buffer.Append(" {").AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
					MethodLambdaToJava(node, wrapper, mt, buffer, indent + 1, !lambdaToAnonymous, tracer
						);
					buffer.AppendIndent(indent).Append("}");
					AddTracer(cl, mt, tracer);
				}
			}
			finally
			{
				DecompilerContext.SetProperty(DecompilerContext.Current_Class_Node, outerNode);
			}
			DecompilerContext.GetLogger().EndWriteClass();
		}

		public virtual void ClassToJava(ClassesProcessor.ClassNode node, TextBuffer buffer
			, int indent, BytecodeMappingTracer tracer)
		{
			ClassesProcessor.ClassNode outerNode = (ClassesProcessor.ClassNode)DecompilerContext
				.GetProperty(DecompilerContext.Current_Class_Node);
			DecompilerContext.SetProperty(DecompilerContext.Current_Class_Node, node);
			int startLine = tracer != null ? tracer.GetCurrentSourceLine() : 0;
			BytecodeMappingTracer dummy_tracer = new BytecodeMappingTracer(startLine);
			try
			{
				// last minute processing
				InvokeProcessors(node);
				ClassWrapper wrapper = node.GetWrapper();
				StructClass cl = wrapper.GetClassStruct();
				DecompilerContext.GetLogger().StartWriteClass(cl.qualifiedName);
				// write class definition
				int start_class_def = buffer.Length();
				WriteClassDefinition(node, buffer, indent);
				bool hasContent = false;
				bool enumFields = false;
				dummy_tracer.IncrementCurrentSourceLine(buffer.CountLines(start_class_def));
				foreach (StructField fd in cl.GetFields())
				{
					bool hide = fd.IsSynthetic() && DecompilerContext.GetOption(IFernflowerPreferences
						.Remove_Synthetic) || wrapper.GetHiddenMembers().Contains(InterpreterUtil.MakeUniqueKey
						(fd.GetName(), fd.GetDescriptor()));
					if (hide)
					{
						continue;
					}
					bool isEnum = fd.HasModifier(ICodeConstants.Acc_Enum) && DecompilerContext.GetOption
						(IFernflowerPreferences.Decompile_Enum);
					if (isEnum)
					{
						if (enumFields)
						{
							buffer.Append(',').AppendLineSeparator();
							dummy_tracer.IncrementCurrentSourceLine();
						}
						enumFields = true;
					}
					else if (enumFields)
					{
						buffer.Append(';');
						buffer.AppendLineSeparator();
						buffer.AppendLineSeparator();
						dummy_tracer.IncrementCurrentSourceLine(2);
						enumFields = false;
					}
					FieldToJava(wrapper, cl, fd, buffer, indent + 1, dummy_tracer);
					// FIXME: insert real tracer
					hasContent = true;
				}
				if (enumFields)
				{
					buffer.Append(';').AppendLineSeparator();
					dummy_tracer.IncrementCurrentSourceLine();
				}
				// FIXME: fields don't matter at the moment
				startLine += buffer.CountLines(start_class_def);
				// methods
				foreach (StructMethod mt in cl.GetMethods())
				{
					bool hide = mt.IsSynthetic() && DecompilerContext.GetOption(IFernflowerPreferences
						.Remove_Synthetic) || mt.HasModifier(ICodeConstants.Acc_Bridge) && DecompilerContext
						.GetOption(IFernflowerPreferences.Remove_Bridge) || wrapper.GetHiddenMembers().
						Contains(InterpreterUtil.MakeUniqueKey(mt.GetName(), mt.GetDescriptor()));
					if (hide)
					{
						continue;
					}
					int position = buffer.Length();
					int storedLine = startLine;
					if (hasContent)
					{
						buffer.AppendLineSeparator();
						startLine++;
					}
					BytecodeMappingTracer method_tracer = new BytecodeMappingTracer(startLine);
					bool methodSkipped = !MethodToJava(node, mt, buffer, indent + 1, method_tracer);
					if (!methodSkipped)
					{
						hasContent = true;
						AddTracer(cl, mt, method_tracer);
						startLine = method_tracer.GetCurrentSourceLine();
					}
					else
					{
						buffer.SetLength(position);
						startLine = storedLine;
					}
				}
				// member classes
				foreach (ClassesProcessor.ClassNode inner in node.nested)
				{
					if (inner.type == ClassesProcessor.ClassNode.Class_Member)
					{
						StructClass innerCl = inner.classStruct;
						bool isSynthetic = (inner.access & ICodeConstants.Acc_Synthetic) != 0 || innerCl.
							IsSynthetic();
						bool hide = isSynthetic && DecompilerContext.GetOption(IFernflowerPreferences.Remove_Synthetic
							) || wrapper.GetHiddenMembers().Contains(innerCl.qualifiedName);
						if (hide)
						{
							continue;
						}
						if (hasContent)
						{
							buffer.AppendLineSeparator();
							startLine++;
						}
						BytecodeMappingTracer class_tracer = new BytecodeMappingTracer(startLine);
						ClassToJava(inner, buffer, indent + 1, class_tracer);
						startLine = buffer.CountLines();
						hasContent = true;
					}
				}
				buffer.AppendIndent(indent).Append('}');
				if (node.type != ClassesProcessor.ClassNode.Class_Anonymous)
				{
					buffer.AppendLineSeparator();
				}
			}
			finally
			{
				DecompilerContext.SetProperty(DecompilerContext.Current_Class_Node, outerNode);
			}
			DecompilerContext.GetLogger().EndWriteClass();
		}

		private static void AddTracer(StructClass cls, StructMethod method, BytecodeMappingTracer
			 tracer)
		{
			StructLineNumberTableAttribute table = method.GetAttribute(StructGeneralAttribute
				.Attribute_Line_Number_Table);
			tracer.SetLineNumberTable(table);
			string key = InterpreterUtil.MakeUniqueKey(method.GetName(), method.GetDescriptor
				());
			DecompilerContext.GetBytecodeSourceMapper().AddTracer(cls.qualifiedName, key, tracer
				);
		}

		private void WriteClassDefinition(ClassesProcessor.ClassNode node, TextBuffer buffer
			, int indent)
		{
			if (node.type == ClassesProcessor.ClassNode.Class_Anonymous)
			{
				buffer.Append(" {").AppendLineSeparator();
				return;
			}
			ClassWrapper wrapper = node.GetWrapper();
			StructClass cl = wrapper.GetClassStruct();
			int flags = node.type == ClassesProcessor.ClassNode.Class_Root ? cl.GetAccessFlags
				() : node.access;
			bool isDeprecated = cl.HasAttribute(StructGeneralAttribute.Attribute_Deprecated);
			bool isSynthetic = (flags & ICodeConstants.Acc_Synthetic) != 0 || cl.HasAttribute
				(StructGeneralAttribute.Attribute_Synthetic);
			bool isEnum = DecompilerContext.GetOption(IFernflowerPreferences.Decompile_Enum)
				 && (flags & ICodeConstants.Acc_Enum) != 0;
			bool isInterface = (flags & ICodeConstants.Acc_Interface) != 0;
			bool isAnnotation = (flags & ICodeConstants.Acc_Annotation) != 0;
			if (isDeprecated)
			{
				AppendDeprecation(buffer, indent);
			}
			if (interceptor != null)
			{
				string oldName = interceptor.GetOldName(cl.qualifiedName);
				AppendRenameComment(buffer, oldName, ClassWriter.MType.Class, indent);
			}
			if (isSynthetic)
			{
				AppendComment(buffer, "synthetic class", indent);
			}
			AppendAnnotations(buffer, indent, cl, -1);
			buffer.AppendIndent(indent);
			if (isEnum)
			{
				// remove abstract and final flags (JLS 8.9 Enums)
				flags &= ~ICodeConstants.Acc_Abstract;
				flags &= ~ICodeConstants.Acc_Final;
			}
			AppendModifiers(buffer, flags, Class_Allowed, isInterface, Class_Excluded);
			if (isEnum)
			{
				buffer.Append("enum ");
			}
			else if (isInterface)
			{
				if (isAnnotation)
				{
					buffer.Append('@');
				}
				buffer.Append("interface ");
			}
			else
			{
				buffer.Append("class ");
			}
			buffer.Append(node.simpleName);
			GenericClassDescriptor descriptor = GetGenericClassDescriptor(cl);
			if (descriptor != null && !(descriptor.fparameters.Count == 0))
			{
				AppendTypeParameters(buffer, descriptor.fparameters, descriptor.fbounds);
			}
			buffer.Append(' ');
			if (!isEnum && !isInterface && cl.superClass != null)
			{
				VarType supertype = new VarType(cl.superClass.GetString(), true);
				if (!VarType.Vartype_Object.Equals(supertype))
				{
					buffer.Append("extends ");
					if (descriptor != null)
					{
						buffer.Append(GenericMain.GetGenericCastTypeName(descriptor.superclass));
					}
					else
					{
						buffer.Append(ExprProcessor.GetCastTypeName(supertype));
					}
					buffer.Append(' ');
				}
			}
			if (!isAnnotation)
			{
				int[] interfaces = cl.GetInterfaces();
				if (interfaces.Length > 0)
				{
					buffer.Append(isInterface ? "extends " : "implements ");
					for (int i = 0; i < interfaces.Length; i++)
					{
						if (i > 0)
						{
							buffer.Append(", ");
						}
						if (descriptor != null)
						{
							buffer.Append(GenericMain.GetGenericCastTypeName(descriptor.superinterfaces[i]));
						}
						else
						{
							buffer.Append(ExprProcessor.GetCastTypeName(new VarType(cl.GetInterface(i), true)
								));
						}
					}
					buffer.Append(' ');
				}
			}
			buffer.Append('{').AppendLineSeparator();
		}

		private void FieldToJava(ClassWrapper wrapper, StructClass cl, StructField fd, TextBuffer
			 buffer, int indent, BytecodeMappingTracer tracer)
		{
			int start = buffer.Length();
			bool isInterface = cl.HasModifier(ICodeConstants.Acc_Interface);
			bool isDeprecated = fd.HasAttribute(StructGeneralAttribute.Attribute_Deprecated);
			bool isEnum = fd.HasModifier(ICodeConstants.Acc_Enum) && DecompilerContext.GetOption
				(IFernflowerPreferences.Decompile_Enum);
			if (isDeprecated)
			{
				AppendDeprecation(buffer, indent);
			}
			if (interceptor != null)
			{
				string oldName = interceptor.GetOldName(cl.qualifiedName + " " + fd.GetName() + " "
					 + fd.GetDescriptor());
				AppendRenameComment(buffer, oldName, ClassWriter.MType.Field, indent);
			}
			if (fd.IsSynthetic())
			{
				AppendComment(buffer, "synthetic field", indent);
			}
			AppendAnnotations(buffer, indent, fd, TypeAnnotation.Field);
			buffer.AppendIndent(indent);
			if (!isEnum)
			{
				AppendModifiers(buffer, fd.GetAccessFlags(), Field_Allowed, isInterface, Field_Excluded
					);
			}
			VarType fieldType = new VarType(fd.GetDescriptor(), false);
			GenericFieldDescriptor descriptor = null;
			if (DecompilerContext.GetOption(IFernflowerPreferences.Decompile_Generic_Signatures
				))
			{
				StructGenericSignatureAttribute attr = fd.GetAttribute(StructGeneralAttribute.Attribute_Signature
					);
				if (attr != null)
				{
					descriptor = GenericMain.ParseFieldSignature(attr.GetSignature());
				}
			}
			if (!isEnum)
			{
				if (descriptor != null)
				{
					buffer.Append(GenericMain.GetGenericCastTypeName(descriptor.type));
				}
				else
				{
					buffer.Append(ExprProcessor.GetCastTypeName(fieldType));
				}
				buffer.Append(' ');
			}
			buffer.Append(fd.GetName());
			tracer.IncrementCurrentSourceLine(buffer.CountLines(start));
			Exprent initializer;
			if (fd.HasModifier(ICodeConstants.Acc_Static))
			{
				initializer = wrapper.GetStaticFieldInitializers().GetWithKey(InterpreterUtil.MakeUniqueKey
					(fd.GetName(), fd.GetDescriptor()));
			}
			else
			{
				initializer = wrapper.GetDynamicFieldInitializers().GetWithKey(InterpreterUtil.MakeUniqueKey
					(fd.GetName(), fd.GetDescriptor()));
			}
			if (initializer != null)
			{
				if (isEnum && initializer.type == Exprent.Exprent_New)
				{
					NewExprent expr = (NewExprent)initializer;
					expr.SetEnumConst(true);
					buffer.Append(expr.ToJava(indent, tracer));
				}
				else
				{
					buffer.Append(" = ");
					if (initializer.type == Exprent.Exprent_Const)
					{
						((ConstExprent)initializer).AdjustConstType(fieldType);
					}
					// FIXME: special case field initializer. Can map to more than one method (constructor) and bytecode instruction
					buffer.Append(initializer.ToJava(indent, tracer));
				}
			}
			else if (fd.HasModifier(ICodeConstants.Acc_Final) && fd.HasModifier(ICodeConstants
				.Acc_Static))
			{
				StructConstantValueAttribute attr = fd.GetAttribute(StructGeneralAttribute.Attribute_Constant_Value
					);
				if (attr != null)
				{
					PrimitiveConstant constant = cl.GetPool().GetPrimitiveConstant(attr.GetIndex());
					buffer.Append(" = ");
					buffer.Append(new ConstExprent(fieldType, constant.value, null).ToJava(indent, tracer
						));
				}
			}
			if (!isEnum)
			{
				buffer.Append(";").AppendLineSeparator();
				tracer.IncrementCurrentSourceLine();
			}
		}

		private static void MethodLambdaToJava(ClassesProcessor.ClassNode lambdaNode, ClassWrapper
			 classWrapper, StructMethod mt, TextBuffer buffer, int indent, bool codeOnly, BytecodeMappingTracer
			 tracer)
		{
			MethodWrapper methodWrapper = classWrapper.GetMethodWrapper(mt.GetName(), mt.GetDescriptor
				());
			MethodWrapper outerWrapper = (MethodWrapper)DecompilerContext.GetProperty(DecompilerContext
				.Current_Method_Wrapper);
			DecompilerContext.SetProperty(DecompilerContext.Current_Method_Wrapper, methodWrapper
				);
			try
			{
				string method_name = lambdaNode.lambdaInformation.method_name;
				MethodDescriptor md_content = MethodDescriptor.ParseDescriptor(lambdaNode.lambdaInformation
					.content_method_descriptor);
				MethodDescriptor md_lambda = MethodDescriptor.ParseDescriptor(lambdaNode.lambdaInformation
					.method_descriptor);
				if (!codeOnly)
				{
					buffer.AppendIndent(indent);
					buffer.Append("public ");
					buffer.Append(method_name);
					buffer.Append("(");
					bool firstParameter = true;
					int index = lambdaNode.lambdaInformation.is_content_method_static ? 0 : 1;
					int start_index = md_content.@params.Length - md_lambda.@params.Length;
					for (int i = 0; i < md_content.@params.Length; i++)
					{
						if (i >= start_index)
						{
							if (!firstParameter)
							{
								buffer.Append(", ");
							}
							string typeName = ExprProcessor.GetCastTypeName(md_content.@params[i].Copy());
							if (ExprProcessor.Undefined_Type_String.Equals(typeName) && DecompilerContext.GetOption
								(IFernflowerPreferences.Undefined_Param_Type_Object))
							{
								typeName = ExprProcessor.GetCastTypeName(VarType.Vartype_Object);
							}
							buffer.Append(typeName);
							buffer.Append(" ");
							string parameterName = methodWrapper.varproc.GetVarName(new VarVersionPair(index, 
								0));
							buffer.Append(parameterName == null ? "param" + index : parameterName);
							// null iff decompiled with errors
							firstParameter = false;
						}
						index += md_content.@params[i].stackSize;
					}
					buffer.Append(") {").AppendLineSeparator();
					indent += 1;
				}
				RootStatement root = classWrapper.GetMethodWrapper(mt.GetName(), mt.GetDescriptor
					()).root;
				if (!methodWrapper.decompiledWithErrors)
				{
					if (root != null)
					{
						// check for existence
						try
						{
							buffer.Append(root.ToJava(indent, tracer));
						}
						catch (Exception t)
						{
							string message = "Method " + mt.GetName() + " " + mt.GetDescriptor() + " couldn't be written.";
							DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
								, t);
							methodWrapper.decompiledWithErrors = true;
						}
					}
				}
				if (methodWrapper.decompiledWithErrors)
				{
					buffer.AppendIndent(indent);
					buffer.Append("// $FF: Couldn't be decompiled");
					buffer.AppendLineSeparator();
				}
				if (root != null)
				{
					tracer.AddMapping(root.GetDummyExit().bytecode);
				}
				if (!codeOnly)
				{
					indent -= 1;
					buffer.AppendIndent(indent).Append('}').AppendLineSeparator();
				}
			}
			finally
			{
				DecompilerContext.SetProperty(DecompilerContext.Current_Method_Wrapper, outerWrapper
					);
			}
		}

		private static string ToValidJavaIdentifier(string name)
		{
			if (name == null || (name.Length == 0))
			{
				return name;
			}
			bool changed = false;
			StringBuilder res = new StringBuilder(name.Length);
			for (int i = 0; i < name.Length; i++)
			{
				char c = name[i];
				if ((i == 0 && !Runtime.IsJavaIdentifierPart(c)) || (i > 0 && !Runtime.IsJavaIdentifierPart
					                                                     (c)))
				{
					changed = true;
					res.Append("_");
				}
				else
				{
					res.Append(c);
				}
			}
			if (!changed)
			{
				return name;
			}
			return res.Append("/* $FF was: ").Append(name).Append("*/").ToString();
		}

		private bool MethodToJava(ClassesProcessor.ClassNode node, StructMethod mt, TextBuffer
			 buffer, int indent, BytecodeMappingTracer tracer)
		{
			ClassWrapper wrapper = node.GetWrapper();
			StructClass cl = wrapper.GetClassStruct();
			MethodWrapper methodWrapper = wrapper.GetMethodWrapper(mt.GetName(), mt.GetDescriptor
				());
			bool hideMethod = false;
			int start_index_method = buffer.Length();
			MethodWrapper outerWrapper = (MethodWrapper)DecompilerContext.GetProperty(DecompilerContext
				.Current_Method_Wrapper);
			DecompilerContext.SetProperty(DecompilerContext.Current_Method_Wrapper, methodWrapper
				);
			try
			{
				bool isInterface = cl.HasModifier(ICodeConstants.Acc_Interface);
				bool isAnnotation = cl.HasModifier(ICodeConstants.Acc_Annotation);
				bool isEnum = cl.HasModifier(ICodeConstants.Acc_Enum) && DecompilerContext.GetOption
					(IFernflowerPreferences.Decompile_Enum);
				bool isDeprecated = mt.HasAttribute(StructGeneralAttribute.Attribute_Deprecated);
				bool clinit = false;
				bool init = false;
				bool dinit = false;
				MethodDescriptor md = MethodDescriptor.ParseDescriptor(mt.GetDescriptor());
				int flags = mt.GetAccessFlags();
				if ((flags & ICodeConstants.Acc_Native) != 0)
				{
					flags &= ~ICodeConstants.Acc_Strict;
				}
				// compiler bug: a strictfp class sets all methods to strictfp
				if (ICodeConstants.Clinit_Name.Equals(mt.GetName()))
				{
					flags &= ICodeConstants.Acc_Static;
				}
				// ignore all modifiers except 'static' in a static initializer
				if (isDeprecated)
				{
					AppendDeprecation(buffer, indent);
				}
				if (interceptor != null)
				{
					string oldName = interceptor.GetOldName(cl.qualifiedName + " " + mt.GetName() + " "
						 + mt.GetDescriptor());
					AppendRenameComment(buffer, oldName, ClassWriter.MType.Method, indent);
				}
				bool isSynthetic = (flags & ICodeConstants.Acc_Synthetic) != 0 || mt.HasAttribute
					(StructGeneralAttribute.Attribute_Synthetic);
				bool isBridge = (flags & ICodeConstants.Acc_Bridge) != 0;
				if (isSynthetic)
				{
					AppendComment(buffer, "synthetic method", indent);
				}
				if (isBridge)
				{
					AppendComment(buffer, "bridge method", indent);
				}
				AppendAnnotations(buffer, indent, mt, TypeAnnotation.Method_Return_Type);
				buffer.AppendIndent(indent);
				AppendModifiers(buffer, flags, Method_Allowed, isInterface, Method_Excluded);
				if (isInterface && !mt.HasModifier(ICodeConstants.Acc_Static) && mt.ContainsCode(
					))
				{
					// 'default' modifier (Java 8)
					buffer.Append("default ");
				}
				string name = mt.GetName();
				if (ICodeConstants.Init_Name.Equals(name))
				{
					if (node.type == ClassesProcessor.ClassNode.Class_Anonymous)
					{
						name = string.Empty;
						dinit = true;
					}
					else
					{
						name = node.simpleName;
						init = true;
					}
				}
				else if (ICodeConstants.Clinit_Name.Equals(name))
				{
					name = string.Empty;
					clinit = true;
				}
				GenericMethodDescriptor descriptor = null;
				if (DecompilerContext.GetOption(IFernflowerPreferences.Decompile_Generic_Signatures
					))
				{
					StructGenericSignatureAttribute attr = mt.GetAttribute(StructGeneralAttribute.Attribute_Signature
						);
					if (attr != null)
					{
						descriptor = GenericMain.ParseMethodSignature(attr.GetSignature());
						if (descriptor != null)
						{
							long actualParams = md.@params.Length;
							List<VarVersionPair> mask = methodWrapper.synthParameters;
							if (mask != null)
							{
								actualParams = mask.Count(c => c == null);
							}
							else if (isEnum && init)
							{
								actualParams -= 2;
							}
							if (actualParams != descriptor.parameterTypes.Count)
							{
								string message = "Inconsistent generic signature in method " + mt.GetName() + " "
									 + mt.GetDescriptor() + " in " + cl.qualifiedName;
								DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
									);
								descriptor = null;
							}
						}
					}
				}
				bool throwsExceptions = false;
				int paramCount = 0;
				if (!clinit && !dinit)
				{
					bool thisVar = !mt.HasModifier(ICodeConstants.Acc_Static);
					if (descriptor != null && !(descriptor.typeParameters.Count == 0))
					{
						AppendTypeParameters(buffer, descriptor.typeParameters, descriptor.typeParameterBounds
							);
						buffer.Append(' ');
					}
					if (!init)
					{
						if (descriptor != null)
						{
							buffer.Append(GenericMain.GetGenericCastTypeName(descriptor.returnType));
						}
						else
						{
							buffer.Append(ExprProcessor.GetCastTypeName(md.ret));
						}
						buffer.Append(' ');
					}
					buffer.Append(ToValidJavaIdentifier(name));
					buffer.Append('(');
					List<VarVersionPair> mask = methodWrapper.synthParameters;
					int lastVisibleParameterIndex = -1;
					for (int i = 0; i < md.@params.Length; i++)
					{
						if (mask == null || mask[i] == null)
						{
							lastVisibleParameterIndex = i;
						}
					}
					List<StructMethodParametersAttribute.Entry> methodParameters = null;
					if (DecompilerContext.GetOption(IFernflowerPreferences.Use_Method_Parameters))
					{
						StructMethodParametersAttribute attr = mt.GetAttribute(StructGeneralAttribute.Attribute_Method_Parameters
							);
						if (attr != null)
						{
							methodParameters = attr.GetEntries();
						}
					}
					int index = isEnum && init ? 3 : thisVar ? 1 : 0;
					int start = isEnum && init ? 2 : 0;
					for (int i = start; i < md.@params.Length; i++)
					{
						if (mask == null || mask[i] == null)
						{
							if (paramCount > 0)
							{
								buffer.Append(", ");
							}
							AppendParameterAnnotations(buffer, mt, paramCount);
							if (methodParameters != null && i < methodParameters.Count)
							{
								AppendModifiers(buffer, methodParameters[i].myAccessFlags, ICodeConstants.Acc_Final
									, isInterface, 0);
							}
							else if (methodWrapper.varproc.GetVarFinal(new VarVersionPair(index, 0)) == VarTypeProcessor
								.Var_Explicit_Final)
							{
								buffer.Append("final ");
							}
							string typeName;
							bool isVarArg = i == lastVisibleParameterIndex && mt.HasModifier(ICodeConstants.Acc_Varargs
								);
							if (descriptor != null)
							{
								GenericType parameterType = descriptor.parameterTypes[paramCount];
								isVarArg &= parameterType.arrayDim > 0;
								if (isVarArg)
								{
									parameterType = parameterType.DecreaseArrayDim();
								}
								typeName = GenericMain.GetGenericCastTypeName(parameterType);
							}
							else
							{
								VarType parameterType = md.@params[i];
								isVarArg &= parameterType.arrayDim > 0;
								if (isVarArg)
								{
									parameterType = parameterType.DecreaseArrayDim();
								}
								typeName = ExprProcessor.GetCastTypeName(parameterType);
							}
							if (ExprProcessor.Undefined_Type_String.Equals(typeName) && DecompilerContext.GetOption
								(IFernflowerPreferences.Undefined_Param_Type_Object))
							{
								typeName = ExprProcessor.GetCastTypeName(VarType.Vartype_Object);
							}
							buffer.Append(typeName);
							if (isVarArg)
							{
								buffer.Append("...");
							}
							buffer.Append(' ');
							string parameterName;
							if (methodParameters != null && i < methodParameters.Count)
							{
								parameterName = methodParameters[i].myName;
							}
							else
							{
								parameterName = methodWrapper.varproc.GetVarName(new VarVersionPair(index, 0));
							}
							buffer.Append(parameterName == null ? "param" + index : parameterName);
							// null iff decompiled with errors
							paramCount++;
						}
						index += md.@params[i].stackSize;
					}
					buffer.Append(')');
					StructExceptionsAttribute attr_1 = mt.GetAttribute(StructGeneralAttribute.Attribute_Exceptions
						);
					if ((descriptor != null && !(descriptor.exceptionTypes.Count == 0)) || attr_1 != 
						null)
					{
						throwsExceptions = true;
						buffer.Append(" throws ");
						for (int i = 0; i < attr_1.GetThrowsExceptions().Count; i++)
						{
							if (i > 0)
							{
								buffer.Append(", ");
							}
							if (descriptor != null && !(descriptor.exceptionTypes.Count == 0))
							{
								GenericType type = descriptor.exceptionTypes[i];
								buffer.Append(GenericMain.GetGenericCastTypeName(type));
							}
							else
							{
								VarType type = new VarType(attr_1.GetExcClassname(i, cl.GetPool()), true);
								buffer.Append(ExprProcessor.GetCastTypeName(type));
							}
						}
					}
				}
				tracer.IncrementCurrentSourceLine(buffer.CountLines(start_index_method));
				if ((flags & (ICodeConstants.Acc_Abstract | ICodeConstants.Acc_Native)) != 0)
				{
					// native or abstract method (explicit or interface)
					if (isAnnotation)
					{
						StructAnnDefaultAttribute attr = mt.GetAttribute(StructGeneralAttribute.Attribute_Annotation_Default
							);
						if (attr != null)
						{
							buffer.Append(" default ");
							buffer.Append(attr.GetDefaultValue().ToJava(0, BytecodeMappingTracer.Dummy));
						}
					}
					buffer.Append(';');
					buffer.AppendLineSeparator();
				}
				else
				{
					if (!clinit && !dinit)
					{
						buffer.Append(' ');
					}
					// We do not have line information for method start, lets have it here for now
					buffer.Append('{').AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
					RootStatement root = wrapper.GetMethodWrapper(mt.GetName(), mt.GetDescriptor()).root;
					if (root != null && !methodWrapper.decompiledWithErrors)
					{
						// check for existence
						try
						{
							// to restore in case of an exception
							BytecodeMappingTracer codeTracer = new BytecodeMappingTracer(tracer.GetCurrentSourceLine
								());
							TextBuffer code = root.ToJava(indent + 1, codeTracer);
							hideMethod = (code.Length() == 0) && (clinit || dinit || HideConstructor(node, init
								, throwsExceptions, paramCount, flags));
							buffer.Append(code);
							tracer.SetCurrentSourceLine(codeTracer.GetCurrentSourceLine());
							tracer.AddTracer(codeTracer);
						}
						catch (Exception t)
						{
							string message = "Method " + mt.GetName() + " " + mt.GetDescriptor() + " couldn't be written.";
							DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
								, t);
							methodWrapper.decompiledWithErrors = true;
						}
					}
					if (methodWrapper.decompiledWithErrors)
					{
						buffer.AppendIndent(indent + 1);
						buffer.Append("// $FF: Couldn't be decompiled");
						buffer.AppendLineSeparator();
						tracer.IncrementCurrentSourceLine();
					}
					else if (root != null)
					{
						tracer.AddMapping(root.GetDummyExit().bytecode);
					}
					buffer.AppendIndent(indent).Append('}').AppendLineSeparator();
				}
				tracer.IncrementCurrentSourceLine();
			}
			finally
			{
				DecompilerContext.SetProperty(DecompilerContext.Current_Method_Wrapper, outerWrapper
					);
			}
			// save total lines
			// TODO: optimize
			//tracer.setCurrentSourceLine(buffer.countLines(start_index_method));
			return !hideMethod;
		}

		private static bool HideConstructor(ClassesProcessor.ClassNode node, bool init, bool
			 throwsExceptions, int paramCount, int methodAccessFlags)
		{
			if (!init || throwsExceptions || paramCount > 0 || !DecompilerContext.GetOption(IFernflowerPreferences
				.Hide_Default_Constructor))
			{
				return false;
			}
			ClassWrapper wrapper = node.GetWrapper();
			StructClass cl = wrapper.GetClassStruct();
			int classAccesFlags = node.type == ClassesProcessor.ClassNode.Class_Root ? cl.GetAccessFlags
				() : node.access;
			bool isEnum = cl.HasModifier(ICodeConstants.Acc_Enum) && DecompilerContext.GetOption
				(IFernflowerPreferences.Decompile_Enum);
			// default constructor requires same accessibility flags. Exception: enum constructor which is always private
			if (!isEnum && ((classAccesFlags & Accessibility_Flags) != (methodAccessFlags & Accessibility_Flags
				)))
			{
				return false;
			}
			int count = 0;
			foreach (StructMethod mt in cl.GetMethods())
			{
				if (ICodeConstants.Init_Name.Equals(mt.GetName()))
				{
					if (++count > 1)
					{
						return false;
					}
				}
			}
			return true;
		}

		private static void AppendDeprecation(TextBuffer buffer, int indent)
		{
			buffer.AppendIndent(indent).Append("/** @deprecated */").AppendLineSeparator();
		}

		[System.Serializable]
		private sealed class MType : Sharpen.EnumBase
		{
			public static readonly ClassWriter.MType Class = new ClassWriter.MType(0, "CLASS"
				);

			public static readonly ClassWriter.MType Field = new ClassWriter.MType(1, "FIELD"
				);

			public static readonly ClassWriter.MType Method = new ClassWriter.MType(2, "METHOD"
				);

			private MType(int ordinal, string name)
				: base(ordinal, name)
			{
			}

			public static MType[] Values()
			{
				return new MType[] { Class, Field, Method };
			}

			static MType()
			{
				RegisterValues<MType>(Values());
			}
		}

		private static void AppendRenameComment(TextBuffer buffer, string oldName, ClassWriter.MType
			 type, int indent)
		{
			if (oldName == null)
			{
				return;
			}
			buffer.AppendIndent(indent);
			buffer.Append("// $FF: renamed from: ");
			switch (type.ordinal())
			{
				case 0:
				{
					buffer.Append(ExprProcessor.BuildJavaClassName(oldName));
					break;
				}

				case 1:
				{
					string[] fParts = oldName.Split(" ");
					FieldDescriptor fd = FieldDescriptor.ParseDescriptor(fParts[2]);
					buffer.Append(fParts[1]);
					buffer.Append(' ');
					buffer.Append(GetTypePrintOut(fd.type));
					break;
				}

				default:
				{
					string[] mParts = oldName.Split(" ");
					MethodDescriptor md = MethodDescriptor.ParseDescriptor(mParts[2]);
					buffer.Append(mParts[1]);
					buffer.Append(" (");
					bool first = true;
					foreach (VarType paramType in md.@params)
					{
						if (!first)
						{
							buffer.Append(", ");
						}
						first = false;
						buffer.Append(GetTypePrintOut(paramType));
					}
					buffer.Append(") ");
					buffer.Append(GetTypePrintOut(md.ret));
					break;
				}
			}
			buffer.AppendLineSeparator();
		}

		private static string GetTypePrintOut(VarType type)
		{
			string typeText = ExprProcessor.GetCastTypeName(type, false);
			if (ExprProcessor.Undefined_Type_String.Equals(typeText) && DecompilerContext.GetOption
				(IFernflowerPreferences.Undefined_Param_Type_Object))
			{
				typeText = ExprProcessor.GetCastTypeName(VarType.Vartype_Object, false);
			}
			return typeText;
		}

		private static void AppendComment(TextBuffer buffer, string comment, int indent)
		{
			buffer.AppendIndent(indent).Append("// $FF: ").Append(comment).AppendLineSeparator
				();
		}

		private static readonly StructGeneralAttribute.Key<StructAnnotationAttribute>[] Annotation_Attributes = new 
			StructGeneralAttribute.Key<StructAnnotationAttribute>[] { StructGeneralAttribute.Attribute_Runtime_Visible_Annotations
			, StructGeneralAttribute.Attribute_Runtime_Invisible_Annotations };

		private static readonly StructGeneralAttribute.Key<StructAnnotationParameterAttribute>[] Parameter_Annotation_Attributes
			 = new StructGeneralAttribute.Key<StructAnnotationParameterAttribute>[] { StructGeneralAttribute.Attribute_Runtime_Visible_Parameter_Annotations
			, StructGeneralAttribute.Attribute_Runtime_Invisible_Parameter_Annotations };

		private static readonly StructGeneralAttribute.Key<StructTypeAnnotationAttribute>[] Type_Annotation_Attributes = 
			new StructGeneralAttribute.Key<StructTypeAnnotationAttribute>[] { StructGeneralAttribute.Attribute_Runtime_Visible_Type_Annotations
			, StructGeneralAttribute.Attribute_Runtime_Invisible_Type_Annotations };

		private static void AppendAnnotations(TextBuffer buffer, int indent, StructMember
			 mb, int targetType)
		{
			HashSet<string> filter = new HashSet<string>();
			foreach (var key in Annotation_Attributes)
			{
				StructAnnotationAttribute attribute = (StructAnnotationAttribute)mb.GetAttribute(
					key);
				if (attribute != null)
				{
					foreach (AnnotationExprent annotation in attribute.GetAnnotations())
					{
						string text = annotation.ToJava(indent, BytecodeMappingTracer.Dummy).ToString();
						filter.Add(text);
						buffer.Append(text).AppendLineSeparator();
					}
				}
			}
			AppendTypeAnnotations(buffer, indent, mb, targetType, -1, filter);
		}

		private static void AppendParameterAnnotations(TextBuffer buffer, StructMethod mt
			, int param)
		{
			HashSet<string> filter = new HashSet<string>();
			foreach (var key in Parameter_Annotation_Attributes)
			{
				StructAnnotationParameterAttribute attribute = (StructAnnotationParameterAttribute
					)mt.GetAttribute(key);
				if (attribute != null)
				{
					List<List<AnnotationExprent>> annotations = attribute.GetParamAnnotations();
					if (param < annotations.Count)
					{
						foreach (AnnotationExprent annotation in annotations[param])
						{
							string text = annotation.ToJava(-1, BytecodeMappingTracer.Dummy).ToString();
							filter.Add(text);
							buffer.Append(text).Append(' ');
						}
					}
				}
			}
			AppendTypeAnnotations(buffer, -1, mt, TypeAnnotation.Method_Parameter, param, filter
				);
		}

		private static void AppendTypeAnnotations(TextBuffer buffer, int indent, StructMember
			 mb, int targetType, int index, HashSet<string> filter)
		{
			foreach (var key in Type_Annotation_Attributes)
			{
				StructTypeAnnotationAttribute attribute = (StructTypeAnnotationAttribute)mb.GetAttribute
					(key);
				if (attribute != null)
				{
					foreach (TypeAnnotation annotation in attribute.GetAnnotations())
					{
						if (annotation.IsTopLevel() && annotation.GetTargetType() == targetType && (index
							 < 0 || annotation.GetIndex() == index))
						{
							string text = annotation.GetAnnotation().ToJava(indent, BytecodeMappingTracer.Dummy
								).ToString();
							if (!filter.Contains(text))
							{
								buffer.Append(text);
								if (indent < 0)
								{
									buffer.Append(' ');
								}
								else
								{
									buffer.AppendLineSeparator();
								}
							}
						}
					}
				}
			}
		}

		private static readonly Dictionary<int, string> Modifiers;

		static ClassWriter()
		{
			Modifiers = new Dictionary<int, string>();
			Sharpen.Collections.Put(Modifiers, ICodeConstants.Acc_Public, "public");
			Sharpen.Collections.Put(Modifiers, ICodeConstants.Acc_Protected, "protected");
			Sharpen.Collections.Put(Modifiers, ICodeConstants.Acc_Private, "private");
			Sharpen.Collections.Put(Modifiers, ICodeConstants.Acc_Abstract, "abstract");
			Sharpen.Collections.Put(Modifiers, ICodeConstants.Acc_Static, "static");
			Sharpen.Collections.Put(Modifiers, ICodeConstants.Acc_Final, "final");
			Sharpen.Collections.Put(Modifiers, ICodeConstants.Acc_Strict, "strictfp");
			Sharpen.Collections.Put(Modifiers, ICodeConstants.Acc_Transient, "transient");
			Sharpen.Collections.Put(Modifiers, ICodeConstants.Acc_Volatile, "volatile");
			Sharpen.Collections.Put(Modifiers, ICodeConstants.Acc_Synchronized, "synchronized"
				);
			Sharpen.Collections.Put(Modifiers, ICodeConstants.Acc_Native, "native");
		}

		private const int Class_Allowed = ICodeConstants.Acc_Public | ICodeConstants.Acc_Protected
			 | ICodeConstants.Acc_Private | ICodeConstants.Acc_Abstract | ICodeConstants.Acc_Static
			 | ICodeConstants.Acc_Final | ICodeConstants.Acc_Strict;

		private const int Field_Allowed = ICodeConstants.Acc_Public | ICodeConstants.Acc_Protected
			 | ICodeConstants.Acc_Private | ICodeConstants.Acc_Static | ICodeConstants.Acc_Final
			 | ICodeConstants.Acc_Transient | ICodeConstants.Acc_Volatile;

		private const int Method_Allowed = ICodeConstants.Acc_Public | ICodeConstants.Acc_Protected
			 | ICodeConstants.Acc_Private | ICodeConstants.Acc_Abstract | ICodeConstants.Acc_Static
			 | ICodeConstants.Acc_Final | ICodeConstants.Acc_Synchronized | ICodeConstants.Acc_Native
			 | ICodeConstants.Acc_Strict;

		private const int Class_Excluded = ICodeConstants.Acc_Abstract | ICodeConstants.Acc_Static;

		private const int Field_Excluded = ICodeConstants.Acc_Public | ICodeConstants.Acc_Static
			 | ICodeConstants.Acc_Final;

		private const int Method_Excluded = ICodeConstants.Acc_Public | ICodeConstants.Acc_Abstract;

		private const int Accessibility_Flags = ICodeConstants.Acc_Public | ICodeConstants
			.Acc_Protected | ICodeConstants.Acc_Private;

		private static void AppendModifiers(TextBuffer buffer, int flags, int allowed, bool
			 isInterface, int excluded)
		{
			flags &= allowed;
			if (!isInterface)
			{
				excluded = 0;
			}
			foreach (int modifier in Modifiers.Keys)
			{
				if ((flags & modifier) == modifier && (modifier & excluded) == 0)
				{
					buffer.Append(Modifiers.GetOrNull(modifier)).Append(' ');
				}
			}
		}

		public static GenericClassDescriptor GetGenericClassDescriptor(StructClass cl)
		{
			if (DecompilerContext.GetOption(IFernflowerPreferences.Decompile_Generic_Signatures
				))
			{
				StructGenericSignatureAttribute attr = cl.GetAttribute(StructGeneralAttribute.Attribute_Signature
					);
				if (attr != null)
				{
					return GenericMain.ParseClassSignature(attr.GetSignature());
				}
			}
			return null;
		}

		public static void AppendTypeParameters<_T0>(TextBuffer buffer, List<string> parameters
			, List<_T0> bounds)
			where _T0 : List<GenericType>
		{
			buffer.Append('<');
			for (int i = 0; i < parameters.Count; i++)
			{
				if (i > 0)
				{
					buffer.Append(", ");
				}
				buffer.Append(parameters[i]);
				var parameterBounds = bounds[i];
				if (parameterBounds.Count > 1 || !"java/lang/Object".Equals(parameterBounds[0].value
					))
				{
					buffer.Append(" extends ");
					buffer.Append(GenericMain.GetGenericCastTypeName(parameterBounds[0]));
					for (int j = 1; j < parameterBounds.Count; j++)
					{
						buffer.Append(" & ");
						buffer.Append(GenericMain.GetGenericCastTypeName(parameterBounds[j]));
					}
				}
			}
			buffer.Append('>');
		}
	}
}
