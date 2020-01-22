/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System.Collections.Generic;
using System.Text;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Modules.Decompiler;
using Sharpen;

namespace JetBrainsDecompiler.Util
{
	public class TextUtil
	{
		private static readonly HashSet<string> Keywords = new HashSet<string>(Sharpen.Arrays.AsList
			("abstract", "default", "if", "private", "this", "boolean", "do", "implements", 
			"protected", "throw", "break", "double", "import", "public", "throws", "byte", "else"
			, "instanceof", "return", "transient", "case", "extends", "int", "short", "try", 
			"catch", "final", "interface", "static", "void", "char", "finally", "long", "strictfp"
			, "volatile", "class", "float", "native", "super", "while", "const", "for", "new"
			, "switch", "continue", "goto", "package", "synchronized", "true", "false", "null"
			, "assert"));

		public static void WriteQualifiedSuper(TextBuffer buf, string qualifier)
		{
			ClassesProcessor.ClassNode classNode = (ClassesProcessor.ClassNode)DecompilerContext
				.GetProperty(DecompilerContext.Current_Class_Node);
			if (!qualifier.Equals(classNode.classStruct.qualifiedName))
			{
				buf.Append(DecompilerContext.GetImportCollector().GetShortName(ExprProcessor.BuildJavaClassName
					(qualifier))).Append('.');
			}
			buf.Append("super");
		}

		public static string GetIndentString(int length)
		{
			if (length == 0)
			{
				return string.Empty;
			}
			StringBuilder buf = new StringBuilder();
			string indent = (string)DecompilerContext.GetProperty(IIFernflowerPreferences.Indent_String
				);
			Append(buf, indent, length);
			return buf.ToString();
		}

		public static void Append(StringBuilder buf, string @string, int times)
		{
			while (times-- > 0)
			{
				buf.Append(@string);
			}
		}

		public static bool IsPrintableUnicode(char c)
		{
			int t = char.GetType(c);
			return t != char.Unassigned && t != char.Line_Separator && t != char.Paragraph_Separator
				 && t != char.Control && t != char.Format && t != char.Private_Use && t != char.
				Surrogate;
		}

		public static string CharToUnicodeLiteral(int value)
		{
			string sTemp = int.ToHexString(value);
			sTemp = Sharpen.Runtime.Substring(("0000" + sTemp), sTemp.Length);
			return "\\u" + sTemp;
		}

		public static bool IsValidIdentifier(string id, int version)
		{
			return IsJavaIdentifier(id) && !IsKeyword(id, version);
		}

		private static bool IsJavaIdentifier(string id)
		{
			if ((id.Length == 0) || !char.IsJavaIdentifierStart(id[0]))
			{
				return false;
			}
			for (int i = 1; i < id.Length; i++)
			{
				if (!char.IsJavaIdentifierPart(id[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool IsKeyword(string id, int version)
		{
			return Keywords.Contains(id) || version >= ICodeConstants.Bytecode_Java_5 && "enum"
				.Equals(id);
		}

		public static string GetInstructionName(int opcode)
		{
			return opcodeNames[opcode];
		}

		private static readonly string[] opcodeNames = new string[] { "nop", "aconst_null"
			, "iconst_m1", "iconst_0", "iconst_1", "iconst_2", "iconst_3", "iconst_4", "iconst_5"
			, "lconst_0", "lconst_1", "fconst_0", "fconst_1", "fconst_2", "dconst_0", "dconst_1"
			, "bipush", "sipush", "ldc", "ldc_w", "ldc2_w", "iload", "lload", "fload", "dload"
			, "aload", "iload_0", "iload_1", "iload_2", "iload_3", "lload_0", "lload_1", "lload_2"
			, "lload_3", "fload_0", "fload_1", "fload_2", "fload_3", "dload_0", "dload_1", "dload_2"
			, "dload_3", "aload_0", "aload_1", "aload_2", "aload_3", "iaload", "laload", "faload"
			, "daload", "aaload", "baload", "caload", "saload", "istore", "lstore", "fstore"
			, "dstore", "astore", "istore_0", "istore_1", "istore_2", "istore_3", "lstore_0"
			, "lstore_1", "lstore_2", "lstore_3", "fstore_0", "fstore_1", "fstore_2", "fstore_3"
			, "dstore_0", "dstore_1", "dstore_2", "dstore_3", "astore_0", "astore_1", "astore_2"
			, "astore_3", "iastore", "lastore", "fastore", "dastore", "aastore", "bastore", 
			"castore", "sastore", "pop", "pop2", "dup", "dup_x1", "dup_x2", "dup2", "dup2_x1"
			, "dup2_x2", "swap", "iadd", "ladd", "fadd", "dadd", "isub", "lsub", "fsub", "dsub"
			, "imul", "lmul", "fmul", "dmul", "idiv", "ldiv", "fdiv", "ddiv", "irem", "lrem"
			, "frem", "drem", "ineg", "lneg", "fneg", "dneg", "ishl", "lshl", "ishr", "lshr"
			, "iushr", "lushr", "iand", "land", "ior", "lor", "ixor", "lxor", "iinc", "i2l", 
			"i2f", "i2d", "l2i", "l2f", "l2d", "f2i", "f2l", "f2d", "d2i", "d2l", "d2f", "i2b"
			, "i2c", "i2s", "lcmp", "fcmpl", "fcmpg", "dcmpl", "dcmpg", "ifeq", "ifne", "iflt"
			, "ifge", "ifgt", "ifle", "if_icmpeq", "if_icmpne", "if_icmplt", "if_icmpge", "if_icmpgt"
			, "if_icmple", "if_acmpeq", "if_acmpne", "goto", "jsr", "ret", "tableswitch", "lookupswitch"
			, "ireturn", "lreturn", "freturn", "dreturn", "areturn", "return", "getstatic", 
			"putstatic", "getfield", "putfield", "invokevirtual", "invokespecial", "invokestatic"
			, "invokeinterface", "invokedynamic", "new", "newarray", "anewarray", "arraylength"
			, "athrow", "checkcast", "instanceof", "monitorenter", "monitorexit", "wide", "multianewarray"
			, "ifnull", "ifnonnull", "goto_w", "jsr_w" };
		// since Java 7
	}
}
