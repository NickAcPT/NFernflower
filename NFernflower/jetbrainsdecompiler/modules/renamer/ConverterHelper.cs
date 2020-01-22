// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Java.Util;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main.Extern;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Renamer
{
	public class ConverterHelper : IIdentifierRenamer
	{
		private static readonly HashSet<string> Keywords = new HashSet<string>(Sharpen.Arrays.AsList
			("abstract", "do", "if", "package", "synchronized", "boolean", "double", "implements"
			, "private", "this", "break", "else", "import", "protected", "throw", "byte", "extends"
			, "instanceof", "public", "throws", "case", "false", "int", "return", "transient"
			, "catch", "final", "interface", "short", "true", "char", "finally", "long", "static"
			, "try", "class", "float", "native", "strictfp", "void", "const", "for", "new", 
			"super", "volatile", "continue", "goto", "null", "switch", "while", "default", "assert"
			, "enum"));

		private static readonly HashSet<string> Reserved_Windows_Namespace = new HashSet<
			string>(Sharpen.Arrays.AsList("con", "prn", "aux", "nul", "com1", "com2", "com3"
			, "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4"
			, "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"));

		private int classCounter = 0;

		private int fieldCounter = 0;

		private int methodCounter = 0;

		private readonly HashSet<string> setNonStandardClassNames = new HashSet<string>();

		public override bool ToBeRenamed(IIdentifierRenamer.Type elementType, string className
			, string element, string descriptor)
		{
			string value = elementType == IIdentifierRenamer.Type.Element_Class ? className : 
				element;
			return value == null || value.Length <= 2 || !IsValidIdentifier(elementType == IIdentifierRenamer.Type
				.Element_Method, value) || Keywords.Contains(value) || elementType == IIdentifierRenamer.Type
				.Element_Class && (Reserved_Windows_Namespace.Contains(value.ToLower(
				)) || value.Length > 255 - ".class".Length);
		}

		/// <summary>
		/// Return
		/// <see langword="true"/>
		/// if, and only if identifier passed is compliant to JLS9 section 3.8 AND DOES NOT CONTAINS so-called "ignorable" characters.
		/// Ignorable characters are removed by javac silently during compilation and thus may appear only in specially crafted obfuscated classes.
		/// For more information about "ignorable" characters see <a href="https://bugs.openjdk.java.net/browse/JDK-7144981">JDK-7144981</a>.
		/// </summary>
		/// <param name="identifier">Identifier to be checked</param>
		/// <returns>
		/// 
		/// <see langword="true"/>
		/// in case
		/// <paramref name="identifier"/>
		/// passed can be used as an identifier;
		/// <see langword="false"/>
		/// otherwise.
		/// </returns>
		private static bool IsValidIdentifier(bool isMethod, string identifier)
		{
			System.Diagnostics.Debug.Assert(identifier != null, "Null identifier passed to the isValidIdentifier() method."
				);
			System.Diagnostics.Debug.Assert(!(identifier.Length == 0), "Empty identifier passed to the isValidIdentifier() method."
				);
			if (isMethod && (identifier.Equals(ICodeConstants.Init_Name) || identifier.Equals
				(ICodeConstants.Clinit_Name)))
			{
				return true;
			}
			if (!Runtime.IsJavaIdentifierPart(identifier[0]))
			{
				return false;
			}
			char[] chars = identifier.ToCharArray();
			for (int i = 1; i < chars.Length; i++)
			{
				char ch = chars[i];
				if ((!Runtime.IsJavaIdentifierPart(ch))/* || char.IsIdentifierIgnorable(ch)*/)
				{
					return false;
				}
			}
			return true;
		}

		// TODO: consider possible conflicts with not renamed classes, fields and methods!
		// We should get all relevant information here.
		public override string GetNextClassName(string fullName, string shortName)
		{
			if (shortName == null)
			{
				return "class_" + (classCounter++);
			}
			int index = 0;
			while (index < shortName.Length && char.IsDigit(shortName[index]))
			{
				index++;
			}
			if (index == 0 || index == shortName.Length)
			{
				return "class_" + (classCounter++);
			}
			else
			{
				string name = Sharpen.Runtime.Substring(shortName, index);
				if (setNonStandardClassNames.Contains(name))
				{
					return "Inner" + name + "_" + (classCounter++);
				}
				else
				{
					setNonStandardClassNames.Add(name);
					return "Inner" + name;
				}
			}
		}

		public override string GetNextFieldName(string className, string field, string descriptor
			)
		{
			return "field_" + (fieldCounter++);
		}

		public override string GetNextMethodName(string className, string method, string 
			descriptor)
		{
			return "method_" + (methodCounter++);
		}

		// *****************************************************************************
		// static methods
		// *****************************************************************************
		public static string GetSimpleClassName(string fullName)
		{
			return Sharpen.Runtime.Substring(fullName, fullName.LastIndexOf('/') + 1);
		}

		public static string ReplaceSimpleClassName(string fullName, string newName)
		{
			return Sharpen.Runtime.Substring(fullName, 0, fullName.LastIndexOf('/') + 1) + newName;
		}
	}
}
