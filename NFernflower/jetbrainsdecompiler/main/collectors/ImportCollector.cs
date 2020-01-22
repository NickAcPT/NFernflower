// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections;
using System.Collections.Generic;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main.Collectors
{
	public class ImportCollector
	{
		private const string Java_Lang_Package = "java.lang";

		private readonly IDictionary<string, string> mapSimpleNames = new Dictionary<string
			, string>();

		private readonly HashSet<string> setNotImportedNames = new HashSet<string>();

		private readonly HashSet<string> setFieldNames = new HashSet<string>();

		private readonly HashSet<string> setInnerClassNames = new HashSet<string>();

		private readonly string currentPackageSlash;

		private readonly string currentPackagePoint;

		public ImportCollector(ClassesProcessor.ClassNode root)
		{
			// set of field names in this class and all its predecessors.
			string clName = root.classStruct.qualifiedName;
			int index = clName.LastIndexOf('/');
			if (index >= 0)
			{
				string packageName = Sharpen.Runtime.Substring(clName, 0, index);
				currentPackageSlash = packageName + '/';
				currentPackagePoint = packageName.Replace('/', '.');
			}
			else
			{
				currentPackageSlash = string.Empty;
				currentPackagePoint = string.Empty;
			}
			IDictionary<string, StructClass> classes = DecompilerContext.GetStructContext().GetClasses
				();
			LinkedList<string> queue = new LinkedList<string>();
			StructClass currentClass = root.classStruct;
			while (currentClass != null)
			{
				if (currentClass.superClass != null)
				{
					queue.Add(currentClass.superClass.GetString());
				}
				Java.Util.Collections.AddAll(queue, currentClass.GetInterfaceNames());
				// all field names for the current class ..
				foreach (StructField f in currentClass.GetFields())
				{
					setFieldNames.Add(f.GetName());
				}
				// .. all inner classes for the current class ..
				StructInnerClassesAttribute attribute = currentClass.GetAttribute(StructGeneralAttribute
					.Attribute_Inner_Classes);
				if (attribute != null)
				{
					foreach (StructInnerClassesAttribute.Entry entry in attribute.GetEntries())
					{
						if (entry.enclosingName != null && entry.enclosingName.Equals(currentClass.qualifiedName
							))
						{
							setInnerClassNames.Add(entry.simpleName);
						}
					}
				}
				// .. and traverse through parent.
				currentClass = !(queue.Count == 0) ? classes.GetOrNull(Sharpen.Collections.RemoveFirst
					(queue)) : null;
				while (currentClass == null && !(queue.Count == 0))
				{
					currentClass = classes.GetOrNull(Sharpen.Collections.RemoveFirst(queue));
				}
			}
		}

		/// <summary>
		/// Check whether the package-less name ClassName is shaded by variable in a context of
		/// the decompiled class
		/// </summary>
		/// <param name="classToName">- pkg.name.ClassName - class to find shortname for</param>
		/// <returns>ClassName if the name is not shaded by local field, pkg.name.ClassName otherwise
		/// 	</returns>
		public virtual string GetShortNameInClassContext(string classToName)
		{
			string shortName = GetShortName(classToName);
			if (setFieldNames.Contains(shortName))
			{
				return classToName;
			}
			else
			{
				return shortName;
			}
		}

		public virtual string GetShortName(string fullName)
		{
			return GetShortName(fullName, true);
		}

		public virtual string GetShortName(string fullName, bool imported)
		{
			ClassesProcessor.ClassNode node = DecompilerContext.GetClassProcessor().GetMapRootClasses
				().GetOrNull(fullName.Replace('.', '/'));
			//todo[r.sh] anonymous classes?
			string result = null;
			if (node != null && node.classStruct.IsOwn())
			{
				result = node.simpleName;
				while (node.parent != null && node.type == ClassesProcessor.ClassNode.Class_Member
					)
				{
					//noinspection StringConcatenationInLoop
					result = node.parent.simpleName + '.' + result;
					node = node.parent;
				}
				if (node.type == ClassesProcessor.ClassNode.Class_Root)
				{
					fullName = node.classStruct.qualifiedName;
					fullName = fullName.Replace('/', '.');
				}
				else
				{
					return result;
				}
			}
			else
			{
				fullName = fullName.Replace('$', '.');
			}
			string shortName = fullName;
			string packageName = string.Empty;
			int lastDot = fullName.LastIndexOf('.');
			if (lastDot >= 0)
			{
				shortName = Sharpen.Runtime.Substring(fullName, lastDot + 1);
				packageName = Sharpen.Runtime.Substring(fullName, 0, lastDot);
			}
			StructContext context = DecompilerContext.GetStructContext();
			// check for another class which could 'shadow' this one. Three cases:
			// 1) class with the same short name in the current package
			// 2) class with the same short name in the default package
			// 3) inner class with the same short name in the current class, a super class, or an implemented interface
			bool existsDefaultClass = (context.GetClass(currentPackageSlash + shortName) != null
				 && !packageName.Equals(currentPackagePoint)) || (context.GetClass(shortName) !=
				 null && !(currentPackagePoint.Length == 0)) || setInnerClassNames.Contains(shortName
				);
			// current package
			// default package
			// inner class
			if (existsDefaultClass || (mapSimpleNames.ContainsKey(shortName) && !packageName.
				Equals(mapSimpleNames.GetOrNull(shortName))))
			{
				//  don't return full name because if the class is a inner class, full name refers to the parent full name, not the child full name
				return result == null ? fullName : (packageName + "." + result);
			}
			else if (!mapSimpleNames.ContainsKey(shortName))
			{
				Sharpen.Collections.Put(mapSimpleNames, shortName, packageName);
				if (!imported)
				{
					setNotImportedNames.Add(shortName);
				}
			}
			return result == null ? shortName : result;
		}

		public virtual int WriteImports(TextBuffer buffer)
		{
			int importLinesWritten = 0;
			List<string> imports = PackImports();
			foreach (string s in imports)
			{
				buffer.Append("import ");
				buffer.Append(s);
				buffer.Append(';');
				buffer.AppendLineSeparator();
				importLinesWritten++;
			}
			return importLinesWritten;
		}

		private List<string> PackImports()
		{
			return mapSimpleNames.Stream().Filter((KeyValuePair<string, string> ent) => !setNotImportedNames
				.Contains(ent.Key) && !(ent.Value.Length == 0) && !Java_Lang_Package.Equals(ent.
				Value) && !ent.Value.Equals(currentPackagePoint)).Sorted(DictionaryEntry.ComparingByValue
				<string, string>().ThenComparing(DictionaryEntry.ComparingByKey())).Map((KeyValuePair
				<string, string> ent) => ent.Value + "." + ent.Key).Collect(Java.Util.Stream.Collectors
				.ToList());
		}
		// exclude the current class or one of the nested ones
		// empty, java.lang and the current packages
	}
}
