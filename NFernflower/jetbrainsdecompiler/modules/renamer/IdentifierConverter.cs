// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.IO;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Renamer
{
	public class IdentifierConverter : INewClassNameBuilder
	{
		private readonly StructContext context;

		private readonly IIdentifierRenamer helper;

		private readonly PoolInterceptor interceptor;

		private List<ClassWrapperNode> rootClasses = new List<ClassWrapperNode>();

		private List<ClassWrapperNode> rootInterfaces = new List<ClassWrapperNode>();

		private Dictionary<string, Dictionary<string, string>> interfaceNameMaps = new 
			Dictionary<string, Dictionary<string, string>>();

		public IdentifierConverter(StructContext context, IIdentifierRenamer helper, PoolInterceptor
			 interceptor)
		{
			this.context = context;
			this.helper = helper;
			this.interceptor = interceptor;
		}

		public virtual void Rename()
		{
			try
			{
				BuildInheritanceTree();
				RenameAllClasses();
				RenameInterfaces();
				RenameClasses();
				context.ReloadContext();
			}
			catch (IOException)
			{
				throw new Exception("Renaming failed!");
			}
		}

		private void RenameClasses()
		{
			List<ClassWrapperNode> lstClasses = GetReversePostOrderListIterative(rootClasses
				);
			Dictionary<string, Dictionary<string, string>> classNameMaps = new Dictionary<string
				, Dictionary<string, string>>();
			foreach (ClassWrapperNode node in lstClasses)
			{
				StructClass cl = node.GetClassStruct();
				Dictionary<string, string> names = new Dictionary<string, string>();
				// merge information on super class
				if (cl.superClass != null)
				{
					Dictionary<string, string> mapClass = classNameMaps.GetOrNull(cl.superClass.GetString
						());
					if (mapClass != null)
					{
						Sharpen.Collections.PutAll(names, mapClass);
					}
				}
				// merge information on interfaces
				foreach (string ifName in cl.GetInterfaceNames())
				{
					Dictionary<string, string> mapInt = interfaceNameMaps.GetOrNull(ifName);
					if (mapInt != null)
					{
						Sharpen.Collections.PutAll(names, mapInt);
					}
					else
					{
						StructClass clintr = context.GetClass(ifName);
						if (clintr != null)
						{
							Sharpen.Collections.PutAll(names, ProcessExternalInterface(clintr));
						}
					}
				}
				RenameClassIdentifiers(cl, names);
				if (!(node.GetSubclasses().Count == 0))
				{
					Sharpen.Collections.Put(classNameMaps, cl.qualifiedName, names);
				}
			}
		}

		private Dictionary<string, string> ProcessExternalInterface(StructClass cl)
		{
			Dictionary<string, string> names = new Dictionary<string, string>();
			foreach (string ifName in cl.GetInterfaceNames())
			{
				Dictionary<string, string> mapInt = interfaceNameMaps.GetOrNull(ifName);
				if (mapInt != null)
				{
					Sharpen.Collections.PutAll(names, mapInt);
				}
				else
				{
					StructClass clintr = context.GetClass(ifName);
					if (clintr != null)
					{
						Sharpen.Collections.PutAll(names, ProcessExternalInterface(clintr));
					}
				}
			}
			RenameClassIdentifiers(cl, names);
			return names;
		}

		private void RenameInterfaces()
		{
			List<ClassWrapperNode> lstInterfaces = GetReversePostOrderListIterative(rootInterfaces
				);
			Dictionary<string, Dictionary<string, string>> interfaceNameMaps = new Dictionary
				<string, Dictionary<string, string>>();
			// rename methods and fields
			foreach (ClassWrapperNode node in lstInterfaces)
			{
				StructClass cl = node.GetClassStruct();
				Dictionary<string, string> names = new Dictionary<string, string>();
				// merge information on super interfaces
				foreach (string ifName in cl.GetInterfaceNames())
				{
					Dictionary<string, string> mapInt = interfaceNameMaps.GetOrNull(ifName);
					if (mapInt != null)
					{
						Sharpen.Collections.PutAll(names, mapInt);
					}
				}
				RenameClassIdentifiers(cl, names);
				Sharpen.Collections.Put(interfaceNameMaps, cl.qualifiedName, names);
			}
			this.interfaceNameMaps = interfaceNameMaps;
		}

		private void RenameAllClasses()
		{
			// order not important
			List<ClassWrapperNode> lstAllClasses = new List<ClassWrapperNode>(GetReversePostOrderListIterative
				(rootInterfaces));
			Sharpen.Collections.AddAll(lstAllClasses, GetReversePostOrderListIterative(rootClasses
				));
			// rename all interfaces and classes
			foreach (ClassWrapperNode node in lstAllClasses)
			{
				RenameClass(node.GetClassStruct());
			}
		}

		private void RenameClass(StructClass cl)
		{
			if (!cl.IsOwn())
			{
				return;
			}
			string classOldFullName = cl.qualifiedName;
			// TODO: rename packages
			string clSimpleName = ConverterHelper.GetSimpleClassName(classOldFullName);
			if (helper.ToBeRenamed(IIdentifierRenamer.Type.Element_Class, clSimpleName, null, 
				null))
			{
				string classNewFullName;
				do
				{
					string classname = helper.GetNextClassName(classOldFullName, ConverterHelper.GetSimpleClassName
						(classOldFullName));
					classNewFullName = ConverterHelper.ReplaceSimpleClassName(classOldFullName, classname
						);
				}
				while (context.GetClasses().ContainsKey(classNewFullName));
				interceptor.AddName(classOldFullName, classNewFullName);
			}
		}

		private void RenameClassIdentifiers(StructClass cl, Dictionary<string, string> names
			)
		{
			// all classes are already renamed
			string classOldFullName = cl.qualifiedName;
			string classNewFullName = interceptor.GetName(classOldFullName);
			if (classNewFullName == null)
			{
				classNewFullName = classOldFullName;
			}
			// methods
			HashSet<string> setMethodNames = new HashSet<string>();
			foreach (StructMethod md in cl.GetMethods())
			{
				setMethodNames.Add(md.GetName());
			}
			VBStyleCollection<StructMethod, string> methods = cl.GetMethods();
			for (int i = 0; i < methods.Count; i++)
			{
				StructMethod mt = methods[i];
				string key = methods.GetKey(i);
				bool isPrivate = mt.HasModifier(ICodeConstants.Acc_Private);
				string name = mt.GetName();
				if (!cl.IsOwn() || mt.HasModifier(ICodeConstants.Acc_Native))
				{
					// external and native methods must not be renamed
					if (!isPrivate)
					{
						Sharpen.Collections.Put(names, key, name);
					}
				}
				else if (helper.ToBeRenamed(IIdentifierRenamer.Type.Element_Method, classOldFullName
					, name, mt.GetDescriptor()))
				{
					if (isPrivate || !names.ContainsKey(key))
					{
						do
						{
							name = helper.GetNextMethodName(classOldFullName, name, mt.GetDescriptor());
						}
						while (setMethodNames.Contains(name));
						if (!isPrivate)
						{
							Sharpen.Collections.Put(names, key, name);
						}
					}
					else
					{
						name = names.GetOrNull(key);
					}
					interceptor.AddName(classOldFullName + " " + mt.GetName() + " " + mt.GetDescriptor
						(), classNewFullName + " " + name + " " + BuildNewDescriptor(false, mt.GetDescriptor
						()));
				}
			}
			// external fields are not being renamed
			if (!cl.IsOwn())
			{
				return;
			}
			// fields
			// FIXME: should overloaded fields become the same name?
			HashSet<string> setFieldNames = new HashSet<string>();
			foreach (StructField fd in cl.GetFields())
			{
				setFieldNames.Add(fd.GetName());
			}
			foreach (StructField fd in cl.GetFields())
			{
				if (helper.ToBeRenamed(IIdentifierRenamer.Type.Element_Field, classOldFullName, fd
					.GetName(), fd.GetDescriptor()))
				{
					string newName;
					do
					{
						newName = helper.GetNextFieldName(classOldFullName, fd.GetName(), fd.GetDescriptor
							());
					}
					while (setFieldNames.Contains(newName));
					interceptor.AddName(classOldFullName + " " + fd.GetName() + " " + fd.GetDescriptor
						(), classNewFullName + " " + newName + " " + BuildNewDescriptor(true, fd.GetDescriptor
						()));
				}
			}
		}

		public virtual string BuildNewClassname(string className)
		{
			return interceptor.GetName(className);
		}

		private string BuildNewDescriptor(bool isField, string descriptor)
		{
			string newDescriptor;
			if (isField)
			{
				newDescriptor = FieldDescriptor.ParseDescriptor(descriptor).BuildNewDescriptor(this
					);
			}
			else
			{
				newDescriptor = MethodDescriptor.ParseDescriptor(descriptor).BuildNewDescriptor(this
					);
			}
			return newDescriptor != null ? newDescriptor : descriptor;
		}

		private static List<ClassWrapperNode> GetReversePostOrderListIterative(List<ClassWrapperNode
			> roots)
		{
			List<ClassWrapperNode> res = new List<ClassWrapperNode>();
			LinkedList<ClassWrapperNode> stackNode = new LinkedList<ClassWrapperNode>();
			LinkedList<int> stackIndex = new LinkedList<int>();
			HashSet<ClassWrapperNode> setVisited = new HashSet<ClassWrapperNode>();
			foreach (ClassWrapperNode root in roots)
			{
				stackNode.AddLast(root);
				stackIndex.AddLast(0);
			}
			while (!(stackNode.Count == 0))
			{
				ClassWrapperNode node = stackNode.Last.Value;
				int index = Sharpen.Collections.RemoveLast(stackIndex);
				setVisited.Add(node);
				List<ClassWrapperNode> lstSubs = node.GetSubclasses();
				for (; index < lstSubs.Count; index++)
				{
					ClassWrapperNode sub = lstSubs[index];
					if (!setVisited.Contains(sub))
					{
						stackIndex.AddLast(index + 1);
						stackNode.AddLast(sub);
						stackIndex.AddLast(0);
						break;
					}
				}
				if (index == lstSubs.Count)
				{
					res.Add(0, node);
					Sharpen.Collections.RemoveLast(stackNode);
				}
			}
			return res;
		}

		private void BuildInheritanceTree()
		{
			Dictionary<string, ClassWrapperNode> nodes = new Dictionary<string, ClassWrapperNode
				>();
			Dictionary<string, StructClass> classes = context.GetClasses();
			List<ClassWrapperNode> rootClasses = new List<ClassWrapperNode>();
			List<ClassWrapperNode> rootInterfaces = new List<ClassWrapperNode>();
			foreach (StructClass cl in classes.Values)
			{
				if (!cl.IsOwn())
				{
					continue;
				}
				LinkedList<StructClass> stack = new LinkedList<StructClass>();
				LinkedList<ClassWrapperNode> stackSubNodes = new LinkedList<ClassWrapperNode>();
				stack.AddLast(cl);
				stackSubNodes.AddLast((ClassWrapperNode) null);
				while (!(stack.Count == 0))
				{
					StructClass clStr = Sharpen.Collections.RemoveFirst(stack);
					ClassWrapperNode child = Sharpen.Collections.RemoveFirst(stackSubNodes);
					ClassWrapperNode node = nodes.GetOrNull(clStr.qualifiedName);
					bool isNewNode = (node == null);
					if (isNewNode)
					{
						Sharpen.Collections.Put(nodes, clStr.qualifiedName, node = new ClassWrapperNode(clStr
							));
					}
					if (child != null)
					{
						node.AddSubclass(child);
					}
					if (!isNewNode)
					{
						break;
					}
					else
					{
						bool isInterface = clStr.HasModifier(ICodeConstants.Acc_Interface);
						bool found_parent = false;
						if (isInterface)
						{
							foreach (string ifName in clStr.GetInterfaceNames())
							{
								StructClass clParent = classes.GetOrNull(ifName);
								if (clParent != null)
								{
									stack.AddLast(clParent);
									stackSubNodes.AddLast(node);
									found_parent = true;
								}
							}
						}
						else if (clStr.superClass != null)
						{
							// null iff java/lang/Object
							StructClass clParent = classes.GetOrNull(clStr.superClass.GetString());
							if (clParent != null)
							{
								stack.AddLast(clParent);
								stackSubNodes.AddLast(node);
								found_parent = true;
							}
						}
						if (!found_parent)
						{
							// no super class or interface
							(isInterface ? rootInterfaces : rootClasses).Add(node);
						}
					}
				}
			}
			this.rootClasses = rootClasses;
			this.rootInterfaces = rootInterfaces;
		}
	}
}
