// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using Sharpen;

namespace JetBrainsDecompiler.Main.Extern
{
	public abstract class IFernflowerLogger
	{
		[System.Serializable]
		public sealed class Severity : Sharpen.EnumBase
		{
			public static readonly IFernflowerLogger.Severity Trace = new IFernflowerLogger.Severity
				(0, "TRACE", "TRACE: ");

			public static readonly IFernflowerLogger.Severity Info = new IFernflowerLogger.Severity
				(1, "INFO", "INFO:  ");

			public static readonly IFernflowerLogger.Severity Warn = new IFernflowerLogger.Severity
				(2, "WARN", "WARN:  ");

			public static readonly IFernflowerLogger.Severity Error = new IFernflowerLogger.Severity
				(3, "ERROR", "ERROR: ");

			public readonly string prefix;

			private Severity(int ordinal, string name, string prefix)
				: base(ordinal, name)
			{
				this.prefix = prefix;
			}

			public static Severity[] Values()
			{
				return new Severity[] { Trace, Info, Warn, Error };
			}

			static Severity()
			{
				RegisterValues<Severity>(Values());
			}
		}

		private IFernflowerLogger.Severity severity = IFernflowerLogger.Severity.Info;

		public virtual bool Accepts(IFernflowerLogger.Severity severity)
		{
			return severity.Ordinal() >= this.severity.Ordinal();
		}

		public virtual void SetSeverity(IFernflowerLogger.Severity severity)
		{
			this.severity = severity;
		}

		public abstract void WriteMessage(string message, IFernflowerLogger.Severity severity
			);

		public abstract void WriteMessage(string message, IFernflowerLogger.Severity severity
			, Exception t);

		public virtual void WriteMessage(string message, Exception t)
		{
			WriteMessage(message, IFernflowerLogger.Severity.Error, t);
		}

		public virtual void StartReadingClass(string className)
		{
		}

		public virtual void EndReadingClass()
		{
		}

		public virtual void StartClass(string className)
		{
		}

		public virtual void EndClass()
		{
		}

		public virtual void StartMethod(string methodName)
		{
		}

		public virtual void EndMethod()
		{
		}

		public virtual void StartWriteClass(string className)
		{
		}

		public virtual void EndWriteClass()
		{
		}
	}
}
