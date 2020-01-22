// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using System.IO;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main
{
	public class ClassesProcessor : ICodeConstants
	{
		public const int Average_Class_Size = 16 * 1024;

		private readonly StructContext context;

		private readonly IDictionary<string, ClassesProcessor.ClassNode> mapRootClasses = 
			new Dictionary<string, ClassesProcessor.ClassNode>();

		private class Inner
		{
			private string simpleName;

			private int type;

			private int accessFlags;

			private static bool Equal(ClassesProcessor.Inner o1, ClassesProcessor.Inner o2)
			{
				return o1.type == o2.type && o1.accessFlags == o2.accessFlags && InterpreterUtil.
					EqualObjects(o1.simpleName, o2.simpleName);
			}
		}

		public ClassesProcessor(StructContext context)
		{
			this.context = context;
		}

		public virtual void LoadClasses(IIIdentifierRenamer renamer)
		{
			IDictionary<string, ClassesProcessor.Inner> mapInnerClasses = new Dictionary<string
				, ClassesProcessor.Inner>();
			IDictionary<string, HashSet<string>> mapNestedClassReferences = new Dictionary<string
				, HashSet<string>>();
			IDictionary<string, HashSet<string>> mapEnclosingClassReferences = new Dictionary
				<string, HashSet<string>>();
			IDictionary<string, string> mapNewSimpleNames = new Dictionary<string, string>();
			bool bDecompileInner = DecompilerContext.GetOption(IIFernflowerPreferences.Decompile_Inner
				);
			bool verifyAnonymousClasses = DecompilerContext.GetOption(IIFernflowerPreferences
				.Verify_Anonymous_Classes);
			// create class nodes
			foreach (StructClass cl in context.GetClasses().Values)
			{
				if (cl.IsOwn() && !mapRootClasses.ContainsKey(cl.qualifiedName))
				{
					if (bDecompileInner)
					{
						StructInnerClassesAttribute inner = cl.GetAttribute(StructGeneralAttribute.Attribute_Inner_Classes
							);
						if (inner != null)
						{
							foreach (StructInnerClassesAttribute.Entry entry in inner.GetEntries())
							{
								string innerName = entry.innerName;
								// original simple name
								string simpleName = entry.simpleName;
								string savedName = mapNewSimpleNames.GetOrNull(innerName);
								if (savedName != null)
								{
									simpleName = savedName;
								}
								else if (simpleName != null && renamer != null && renamer.ToBeRenamed(IIdentifierRenamer.Type
									.Element_Class, simpleName, null, null))
								{
									simpleName = renamer.GetNextClassName(innerName, simpleName);
									Sharpen.Collections.Put(mapNewSimpleNames, innerName, simpleName);
								}
								ClassesProcessor.Inner rec = new ClassesProcessor.Inner();
								rec.simpleName = simpleName;
								rec.type = entry.simpleNameIdx == 0 ? ClassesProcessor.ClassNode.Class_Anonymous : 
									entry.outerNameIdx == 0 ? ClassesProcessor.ClassNode.Class_Local : ClassesProcessor.ClassNode
									.Class_Member;
								rec.accessFlags = entry.accessFlags;
								// enclosing class
								string enclClassName = entry.outerNameIdx != 0 ? entry.enclosingName : cl.qualifiedName;
								if (enclClassName == null || innerName.Equals(enclClassName))
								{
									continue;
								}
								// invalid name or self reference
								if (rec.type == ClassesProcessor.ClassNode.Class_Member && !innerName.Equals(enclClassName
									 + '$' + entry.simpleName))
								{
									continue;
								}
								// not a real inner class
								StructClass enclosingClass = context.GetClasses().GetOrNull(enclClassName);
								if (enclosingClass != null && enclosingClass.IsOwn())
								{
									// own classes only
									ClassesProcessor.Inner existingRec = mapInnerClasses.GetOrNull(innerName);
									if (existingRec == null)
									{
										Sharpen.Collections.Put(mapInnerClasses, innerName, rec);
									}
									else if (!ClassesProcessor.Inner.Equal(existingRec, rec))
									{
										string message = "Inconsistent inner class entries for " + innerName + "!";
										DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
											);
									}
									// reference to the nested class
									mapNestedClassReferences.ComputeIfAbsent(enclClassName, (string k) => new HashSet
										<string>()).Add(innerName);
									// reference to the enclosing class
									mapEnclosingClassReferences.ComputeIfAbsent(innerName, (string k) => new HashSet<
										string>()).Add(enclClassName);
								}
							}
						}
					}
					ClassesProcessor.ClassNode node = new ClassesProcessor.ClassNode(ClassesProcessor.ClassNode
						.Class_Root, cl);
					node.access = cl.GetAccessFlags();
					Sharpen.Collections.Put(mapRootClasses, cl.qualifiedName, node);
				}
			}
			if (bDecompileInner)
			{
				// connect nested classes
				foreach (KeyValuePair<string, ClassesProcessor.ClassNode> ent in mapRootClasses)
				{
					// root class?
					if (!mapInnerClasses.ContainsKey(ent.Key))
					{
						HashSet<string> setVisited = new HashSet<string>();
						LinkedList<string> stack = new LinkedList<string>();
						stack.Add(ent.Key);
						setVisited.Add(ent.Key);
						while (!(stack.Count == 0))
						{
							string superClass = Sharpen.Collections.RemoveFirst(stack);
							ClassesProcessor.ClassNode superNode = mapRootClasses.GetOrNull(superClass);
							HashSet<string> setNestedClasses = mapNestedClassReferences.GetOrNull(superClass);
							if (setNestedClasses != null)
							{
								StructClass scl = superNode.classStruct;
								StructInnerClassesAttribute inner = scl.GetAttribute(StructGeneralAttribute.Attribute_Inner_Classes
									);
								if (inner == null || (inner.GetEntries().Count == 0))
								{
									DecompilerContext.GetLogger().WriteMessage(superClass + " does not contain inner classes!"
										, IFernflowerLogger.Severity.Warn);
									continue;
								}
								foreach (StructInnerClassesAttribute.Entry entry in inner.GetEntries())
								{
									string nestedClass = entry.innerName;
									if (!setNestedClasses.Contains(nestedClass))
									{
										continue;
									}
									if (!setVisited.Add(nestedClass))
									{
										continue;
									}
									ClassesProcessor.ClassNode nestedNode = mapRootClasses.GetOrNull(nestedClass);
									if (nestedNode == null)
									{
										DecompilerContext.GetLogger().WriteMessage("Nested class " + nestedClass + " missing!"
											, IFernflowerLogger.Severity.Warn);
										continue;
									}
									ClassesProcessor.Inner rec = mapInnerClasses.GetOrNull(nestedClass);
									//if ((Integer)arr[2] == ClassNode.CLASS_MEMBER) {
									// FIXME: check for consistent naming
									//}
									nestedNode.simpleName = rec.simpleName;
									nestedNode.type = rec.type;
									nestedNode.access = rec.accessFlags;
									// sanity checks of the class supposed to be anonymous
									if (verifyAnonymousClasses && nestedNode.type == ClassesProcessor.ClassNode.Class_Anonymous
										 && !IsAnonymous(nestedNode.classStruct, scl))
									{
										nestedNode.type = ClassesProcessor.ClassNode.Class_Local;
									}
									if (nestedNode.type == ClassesProcessor.ClassNode.Class_Anonymous)
									{
										StructClass cl = nestedNode.classStruct;
										// remove static if anonymous class (a common compiler bug)
										nestedNode.access &= ~ICodeConstants.Acc_Static;
										int[] interfaces = cl.GetInterfaces();
										if (interfaces.Length > 0)
										{
											nestedNode.anonymousClassType = new VarType(cl.GetInterface(0), true);
										}
										else
										{
											nestedNode.anonymousClassType = new VarType(cl.superClass.GetString(), true);
										}
									}
									else if (nestedNode.type == ClassesProcessor.ClassNode.Class_Local)
									{
										// only abstract and final are permitted (a common compiler bug)
										nestedNode.access &= (ICodeConstants.Acc_Abstract | ICodeConstants.Acc_Final);
									}
									superNode.nested.Add(nestedNode);
									nestedNode.parent = superNode;
									Sharpen.Collections.AddAll(nestedNode.enclosingClasses, mapEnclosingClassReferences
										.GetOrNull(nestedClass));
									stack.Add(nestedClass);
								}
							}
						}
					}
				}
			}
		}

		private static bool IsAnonymous(StructClass cl, StructClass enclosingCl)
		{
			// checking super class and interfaces
			int[] interfaces = cl.GetInterfaces();
			if (interfaces.Length > 0)
			{
				bool hasNonTrivialSuperClass = cl.superClass != null && !VarType.Vartype_Object.Equals
					(new VarType(cl.superClass.GetString(), true));
				if (hasNonTrivialSuperClass || interfaces.Length > 1)
				{
					// can't have multiple 'sources'
					string message = "Inconsistent anonymous class definition: '" + cl.qualifiedName 
						+ "'. Multiple interfaces and/or super class defined.";
					DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
						);
					return false;
				}
			}
			else if (cl.superClass == null)
			{
				// neither interface nor super class defined
				string message = "Inconsistent anonymous class definition: '" + cl.qualifiedName 
					+ "'. Neither interface nor super class defined.";
				DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
					);
				return false;
			}
			// FIXME: check constructors
			// FIXME: check enclosing class/method
			ConstantPool pool = enclosingCl.GetPool();
			int refCounter = 0;
			bool refNotNew = false;
			StructEnclosingMethodAttribute attribute = cl.GetAttribute(StructGeneralAttribute
				.Attribute_Enclosing_Method);
			string enclosingMethod = attribute != null ? attribute.GetMethodName() : null;
			// checking references in the enclosing class
			foreach (StructMethod mt in enclosingCl.GetMethods())
			{
				if (enclosingMethod != null && !enclosingMethod.Equals(mt.GetName()))
				{
					continue;
				}
				try
				{
					mt.ExpandData();
					InstructionSequence seq = mt.GetInstructionSequence();
					if (seq != null)
					{
						int len = seq.Length();
						for (int i = 0; i < len; i++)
						{
							Instruction instr = seq.GetInstr(i);
							switch (instr.opcode)
							{
								case opc_checkcast:
								case opc_instanceof:
								{
									if (cl.qualifiedName.Equals(pool.GetPrimitiveConstant(instr.Operand(0)).GetString
										()))
									{
										refCounter++;
										refNotNew = true;
									}
									break;
								}

								case opc_new:
								case opc_anewarray:
								case opc_multianewarray:
								{
									if (cl.qualifiedName.Equals(pool.GetPrimitiveConstant(instr.Operand(0)).GetString
										()))
									{
										refCounter++;
									}
									break;
								}

								case opc_getstatic:
								case opc_putstatic:
								{
									if (cl.qualifiedName.Equals(pool.GetLinkConstant(instr.Operand(0)).classname))
									{
										refCounter++;
										refNotNew = true;
									}
									break;
								}
							}
						}
					}
					mt.ReleaseResources();
				}
				catch (IOException)
				{
					string message = "Could not read method while checking anonymous class definition: '"
						 + enclosingCl.qualifiedName + "', '" + InterpreterUtil.MakeUniqueKey(mt.GetName
						(), mt.GetDescriptor()) + "'";
					DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
						);
					return false;
				}
				if (refCounter > 1 || refNotNew)
				{
					string message = "Inconsistent references to the class '" + cl.qualifiedName + "' which is supposed to be anonymous";
					DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
						);
					return false;
				}
			}
			return true;
		}

		/// <exception cref="System.IO.IOException"/>
		public virtual void WriteClass(StructClass cl, TextBuffer buffer)
		{
			ClassesProcessor.ClassNode root = mapRootClasses.GetOrNull(cl.qualifiedName);
			if (root.type != ClassesProcessor.ClassNode.Class_Root)
			{
				return;
			}
			DecompilerContext.GetLogger().StartReadingClass(cl.qualifiedName);
			try
			{
				ImportCollector importCollector = new ImportCollector(root);
				DecompilerContext.StartClass(importCollector);
				new LambdaProcessor().ProcessClass(root);
				// add simple class names to implicit import
				AddClassnameToImport(root, importCollector);
				// build wrappers for all nested classes (that's where actual processing takes place)
				InitWrappers(root);
				new NestedClassProcessor().ProcessClass(root, root);
				new NestedMemberAccess().PropagateMemberAccess(root);
				TextBuffer classBuffer = new TextBuffer(Average_Class_Size);
				new ClassWriter().ClassToJava(root, classBuffer, 0, null);
				int index = cl.qualifiedName.LastIndexOf("/");
				if (index >= 0)
				{
					string packageName = Sharpen.Runtime.Substring(cl.qualifiedName, 0, index).Replace
						('/', '.');
					buffer.Append("package ");
					buffer.Append(packageName);
					buffer.Append(";");
					buffer.AppendLineSeparator();
					buffer.AppendLineSeparator();
				}
				int import_lines_written = importCollector.WriteImports(buffer);
				if (import_lines_written > 0)
				{
					buffer.AppendLineSeparator();
				}
				int offsetLines = buffer.CountLines();
				buffer.Append(classBuffer);
				if (DecompilerContext.GetOption(IIFernflowerPreferences.Bytecode_Source_Mapping))
				{
					BytecodeSourceMapper mapper = DecompilerContext.GetBytecodeSourceMapper();
					mapper.AddTotalOffset(offsetLines);
					if (DecompilerContext.GetOption(IIFernflowerPreferences.Dump_Original_Lines))
					{
						buffer.DumpOriginalLineNumbers(mapper.GetOriginalLinesMapping());
					}
					if (DecompilerContext.GetOption(IIFernflowerPreferences.Unit_Test_Mode))
					{
						buffer.AppendLineSeparator();
						mapper.DumpMapping(buffer, true);
					}
				}
			}
			finally
			{
				DestroyWrappers(root);
				DecompilerContext.GetLogger().EndReadingClass();
			}
		}

		private static void InitWrappers(ClassesProcessor.ClassNode node)
		{
			if (node.type == ClassesProcessor.ClassNode.Class_Lambda)
			{
				return;
			}
			ClassWrapper wrapper = new ClassWrapper(node.classStruct);
			wrapper.Init();
			node.wrapper = wrapper;
			foreach (ClassesProcessor.ClassNode nd in node.nested)
			{
				InitWrappers(nd);
			}
		}

		private static void AddClassnameToImport(ClassesProcessor.ClassNode node, ImportCollector
			 imp)
		{
			if (node.simpleName != null && node.simpleName.Length > 0)
			{
				imp.GetShortName(node.type == ClassesProcessor.ClassNode.Class_Root ? node.classStruct
					.qualifiedName : node.simpleName, false);
			}
			foreach (ClassesProcessor.ClassNode nd in node.nested)
			{
				AddClassnameToImport(nd, imp);
			}
		}

		private static void DestroyWrappers(ClassesProcessor.ClassNode node)
		{
			node.wrapper = null;
			node.classStruct.ReleaseResources();
			foreach (ClassesProcessor.ClassNode nd in node.nested)
			{
				DestroyWrappers(nd);
			}
		}

		public virtual IDictionary<string, ClassesProcessor.ClassNode> GetMapRootClasses(
			)
		{
			return mapRootClasses;
		}

		public class ClassNode
		{
			public const int Class_Root = 0;

			public const int Class_Member = 1;

			public const int Class_Anonymous = 2;

			public const int Class_Local = 4;

			public const int Class_Lambda = 8;

			public int type;

			public int access;

			public string simpleName;

			public readonly StructClass classStruct;

			private ClassWrapper wrapper;

			public string enclosingMethod;

			public InvocationExprent superInvocation;

			public readonly IDictionary<string, VarVersionPair> mapFieldsToVars = new Dictionary
				<string, VarVersionPair>();

			public VarType anonymousClassType;

			public readonly List<ClassesProcessor.ClassNode> nested = new List<ClassesProcessor.ClassNode
				>();

			public readonly HashSet<string> enclosingClasses = new HashSet<string>();

			public ClassesProcessor.ClassNode parent;

			public ClassesProcessor.ClassNode.LambdaInformation lambdaInformation;

			public ClassNode(string content_class_name, string content_method_name, string content_method_descriptor
				, int content_method_invocation_type, string lambda_class_name, string lambda_method_name
				, string lambda_method_descriptor, StructClass classStruct)
			{
				// lambda class constructor
				this.type = Class_Lambda;
				this.classStruct = classStruct;
				// 'parent' class containing the static function
				lambdaInformation = new ClassesProcessor.ClassNode.LambdaInformation();
				lambdaInformation.method_name = lambda_method_name;
				lambdaInformation.method_descriptor = lambda_method_descriptor;
				lambdaInformation.content_class_name = content_class_name;
				lambdaInformation.content_method_name = content_method_name;
				lambdaInformation.content_method_descriptor = content_method_descriptor;
				lambdaInformation.content_method_invocation_type = content_method_invocation_type;
				lambdaInformation.content_method_key = InterpreterUtil.MakeUniqueKey(lambdaInformation
					.content_method_name, lambdaInformation.content_method_descriptor);
				anonymousClassType = new VarType(lambda_class_name, true);
				bool is_method_reference = (content_class_name != classStruct.qualifiedName);
				if (!is_method_reference)
				{
					// content method in the same class, check synthetic flag
					StructMethod mt = classStruct.GetMethod(content_method_name, content_method_descriptor
						);
					is_method_reference = !mt.IsSynthetic();
				}
				// if not synthetic -> method reference
				lambdaInformation.is_method_reference = is_method_reference;
				lambdaInformation.is_content_method_static = (lambdaInformation.content_method_invocation_type
					 == ICodeConstants.CONSTANT_MethodHandle_REF_invokeStatic);
			}

			public ClassNode(int type, StructClass classStruct)
			{
				// FIXME: redundant?
				this.type = type;
				this.classStruct = classStruct;
				simpleName = Sharpen.Runtime.Substring(classStruct.qualifiedName, classStruct.qualifiedName
					.LastIndexOf('/') + 1);
			}

			public virtual ClassesProcessor.ClassNode GetClassNode(string qualifiedName)
			{
				foreach (ClassesProcessor.ClassNode node in nested)
				{
					if (qualifiedName.Equals(node.classStruct.qualifiedName))
					{
						return node;
					}
				}
				return null;
			}

			public virtual ClassWrapper GetWrapper()
			{
				ClassesProcessor.ClassNode node = this;
				while (node.type == Class_Lambda)
				{
					node = node.parent;
				}
				return node.wrapper;
			}

			public class LambdaInformation
			{
				public string method_name;

				public string method_descriptor;

				public string content_class_name;

				public string content_method_name;

				public string content_method_descriptor;

				public int content_method_invocation_type;

				public string content_method_key;

				public bool is_method_reference;

				public bool is_content_method_static;
				// values from CONSTANT_MethodHandle_REF_*
			}
		}
	}
}
