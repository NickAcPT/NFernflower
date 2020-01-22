// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrainsDecompiler.Main;
using Sharpen;

namespace JetBrainsDecompiler.Code.Cfg
{
	public class ExceptionRangeCFG
	{
		private readonly List<BasicBlock> protectedRange;

		private BasicBlock handler;

		private List<string> exceptionTypes;

		public ExceptionRangeCFG(List<BasicBlock> protectedRange, BasicBlock handler, IList
			<string> exceptionType)
		{
			// FIXME: replace with set
			this.protectedRange = protectedRange;
			this.handler = handler;
			if (exceptionType != null)
			{
				this.exceptionTypes = new List<string>(exceptionType);
			}
		}

		public virtual bool IsCircular()
		{
			return protectedRange.Contains(handler);
		}

		public override string ToString()
		{
			string new_line_separator = DecompilerContext.GetNewLineSeparator();
			StringBuilder buf = new StringBuilder();
			buf.Append("exceptionType:");
			foreach (string exception_type in exceptionTypes)
			{
				buf.Append(" ").Append(exception_type);
			}
			buf.Append(new_line_separator);
			buf.Append("handler: ").Append(handler.id).Append(new_line_separator);
			buf.Append("range: ");
			foreach (BasicBlock block in protectedRange)
			{
				buf.Append(block.id).Append(" ");
			}
			buf.Append(new_line_separator);
			return buf.ToString();
		}

		public virtual BasicBlock GetHandler()
		{
			return handler;
		}

		public virtual void SetHandler(BasicBlock handler)
		{
			this.handler = handler;
		}

		public virtual List<BasicBlock> GetProtectedRange()
		{
			return protectedRange;
		}

		public virtual List<string> GetExceptionTypes()
		{
			return this.exceptionTypes;
		}

		public virtual void AddExceptionType(string exceptionType)
		{
			if (this.exceptionTypes == null)
			{
				return;
			}
			if (exceptionType == null)
			{
				this.exceptionTypes = null;
			}
			else
			{
				this.exceptionTypes.Add(exceptionType);
			}
		}

		public virtual string GetUniqueExceptionsString()
		{
			return exceptionTypes != null ? string.Join(':', exceptionTypes.Distinct()) : null;
		}
	}
}
