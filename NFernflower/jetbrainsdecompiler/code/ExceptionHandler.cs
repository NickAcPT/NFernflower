// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Main;
using Sharpen;

namespace JetBrainsDecompiler.Code
{
	public class ExceptionHandler
	{
		public int from = 0;

		public int to = 0;

		public int handler = 0;

		public int from_instr = 0;

		public int to_instr = 0;

		public int handler_instr = 0;

		public string exceptionClass = null;

		public override string ToString()
		{
			string new_line_separator = DecompilerContext.GetNewLineSeparator();
			return "from: " + from + " to: " + to + " handler: " + handler + new_line_separator
				 + "from_instr: " + from_instr + " to_instr: " + to_instr + " handler_instr: " +
				 handler_instr + new_line_separator + "exceptionClass: " + exceptionClass + new_line_separator;
		}
	}
}
