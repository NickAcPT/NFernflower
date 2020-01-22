// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Sharpen;

namespace JetBrainsDecompiler.Code
{
	public class ExceptionTable
	{
		public static readonly ExceptionTable Empty = new ExceptionTable(new System.Collections.Generic.List<
			ExceptionHandler>());

		private readonly List<ExceptionHandler> handlers;

		public ExceptionTable(List<ExceptionHandler> handlers)
		{
			this.handlers = handlers;
		}

		public virtual List<ExceptionHandler> GetHandlers()
		{
			return handlers;
		}
	}
}
