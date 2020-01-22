// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class RootStatement : Statement
	{
		private readonly DummyExitStatement dummyExit;

		public RootStatement(Statement head, DummyExitStatement dummyExit)
		{
			type = Statement.Type_Root;
			first = head;
			this.dummyExit = dummyExit;
			stats.AddWithKey(first, first.id);
			first.SetParent(this);
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			return ExprProcessor.ListToJava(varDefinitions, indent, tracer).Append(first.ToJava
				(indent, tracer));
		}

		public virtual DummyExitStatement GetDummyExit()
		{
			return dummyExit;
		}
	}
}
