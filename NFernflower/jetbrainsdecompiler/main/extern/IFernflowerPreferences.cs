// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main.Extern
{
	public abstract class IFernflowerPreferences
	{
		public const string Remove_Bridge = "rbr";

		public const string Remove_Synthetic = "rsy";

		public const string Decompile_Inner = "din";

		public const string Decompile_Class_1_4 = "dc4";

		public const string Decompile_Assertions = "das";

		public const string Hide_Empty_Super = "hes";

		public const string Hide_Default_Constructor = "hdc";

		public const string Decompile_Generic_Signatures = "dgs";

		public const string No_Exceptions_Return = "ner";

		public const string Ensure_Synchronized_Monitor = "esm";

		public const string Decompile_Enum = "den";

		public const string Remove_Get_Class_New = "rgn";

		public const string Literals_As_Is = "lit";

		public const string Boolean_True_One = "bto";

		public const string Ascii_String_Characters = "asc";

		public const string Synthetic_Not_Set = "nns";

		public const string Undefined_Param_Type_Object = "uto";

		public const string Use_Debug_Var_Names = "udv";

		public const string Use_Method_Parameters = "ump";

		public const string Remove_Empty_Ranges = "rer";

		public const string Finally_Deinline = "fdi";

		public const string Idea_Not_Null_Annotation = "inn";

		public const string Lambda_To_Anonymous_Class = "lac";

		public const string Bytecode_Source_Mapping = "bsm";

		public const string Ignore_Invalid_Bytecode = "iib";

		public const string Verify_Anonymous_Classes = "vac";

		public const string Log_Level = "log";

		public const string Max_Processing_Method = "mpm";

		public const string Rename_Entities = "ren";

		public const string User_Renamer_Class = "urc";

		public const string New_Line_Separator = "nls";

		public const string Indent_String = "ind";

		public const string Banner = "ban";

		public const string Dump_Original_Lines = "__dump_original_lines__";

		public const string Unit_Test_Mode = "__unit_test_mode__";

		public const string Line_Separator_Win = "\r\n";

		public const string Line_Separator_Unx = "\n";

		public static Dictionary<string, object> Defaults = new Dictionary<string, object>
			{
				{Remove_Bridge, "1"},
				{Remove_Synthetic, "0"},
				{Decompile_Inner, "1"},
				{Decompile_Class_1_4, "1"},
				{Decompile_Assertions, "1"},
				{Hide_Empty_Super, "1"},
				{Hide_Default_Constructor, "1"},
				{Decompile_Generic_Signatures, "0"},
				{No_Exceptions_Return, "1"},
				{Ensure_Synchronized_Monitor, "1"},
				{Decompile_Enum, "1"},
				{Remove_Get_Class_New, "1"},
				{Literals_As_Is, "0"},
				{Boolean_True_One, "1"},
				{Ascii_String_Characters, "0"},
				{Synthetic_Not_Set, "0"},
				{Undefined_Param_Type_Object, "1"},
				{Use_Debug_Var_Names, "1"},
				{Use_Method_Parameters, "1"},
				{Remove_Empty_Ranges, "1"},
				{Finally_Deinline, "1"},
				{Idea_Not_Null_Annotation, "1"},
				{Lambda_To_Anonymous_Class, "0"},
				{Bytecode_Source_Mapping, "0"},
				{Ignore_Invalid_Bytecode, "0"},
				{Verify_Anonymous_Classes, "0"},
				{Log_Level, IFernflowerLogger.Severity.Info.name()},
				{Max_Processing_Method, "0"},
				{Rename_Entities, "0"},
				{New_Line_Separator, (InterpreterUtil.Is_Windows ? "0" : "1")},
				{Indent_String, "   "},
				{Banner, ""},
				{Unit_Test_Mode, "0"},
				{Dump_Original_Lines, "0"}
			};
	}
}

