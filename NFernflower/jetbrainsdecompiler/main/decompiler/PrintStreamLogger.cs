// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.IO;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main.Decompiler
{
	public class PrintStreamLogger : IFernflowerLogger
	{
		private readonly TextWriter stream;

		private int indent;

		public PrintStreamLogger(TextWriter printStream)
		{
			stream = printStream;
			indent = 0;
		}

		public override void WriteMessage(string message, IFernflowerLogger.Severity severity
			)
		{
			if (Accepts(severity))
			{
				stream.WriteLine(severity.prefix + TextUtil.GetIndentString(indent) + message);
			}
		}

		public override void WriteMessage(string message, IFernflowerLogger.Severity severity
			, Exception t)
		{
			if (Accepts(severity))
			{
				WriteMessage(message, severity);
				Sharpen.Runtime.PrintStackTrace(t, stream);
			}
		}

		public override void StartReadingClass(string className)
		{
			if (Accepts(IFernflowerLogger.Severity.Info))
			{
				WriteMessage("Decompiling class " + className, IFernflowerLogger.Severity.Info);
				++indent;
			}
		}

		public override void EndReadingClass()
		{
			if (Accepts(IFernflowerLogger.Severity.Info))
			{
				--indent;
				WriteMessage("... done", IFernflowerLogger.Severity.Info);
			}
		}

		public override void StartClass(string className)
		{
			if (Accepts(IFernflowerLogger.Severity.Info))
			{
				WriteMessage("Processing class " + className, IFernflowerLogger.Severity.Trace);
				++indent;
			}
		}

		public override void EndClass()
		{
			if (Accepts(IFernflowerLogger.Severity.Info))
			{
				--indent;
				WriteMessage("... proceeded", IFernflowerLogger.Severity.Trace);
			}
		}

		public override void StartMethod(string methodName)
		{
			if (Accepts(IFernflowerLogger.Severity.Info))
			{
				WriteMessage("Processing method " + methodName, IFernflowerLogger.Severity.Trace);
				++indent;
			}
		}

		public override void EndMethod()
		{
			if (Accepts(IFernflowerLogger.Severity.Info))
			{
				--indent;
				WriteMessage("... proceeded", IFernflowerLogger.Severity.Trace);
			}
		}

		public override void StartWriteClass(string className)
		{
			if (Accepts(IFernflowerLogger.Severity.Info))
			{
				WriteMessage("Writing class " + className, IFernflowerLogger.Severity.Trace);
				++indent;
			}
		}

		public override void EndWriteClass()
		{
			if (Accepts(IFernflowerLogger.Severity.Info))
			{
				--indent;
				WriteMessage("... written", IFernflowerLogger.Severity.Trace);
			}
		}
	}
}
