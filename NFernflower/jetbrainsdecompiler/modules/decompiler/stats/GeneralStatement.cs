// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class GeneralStatement : Statement
	{
		private GeneralStatement()
		{
			// *****************************************************************************
			// constructors
			// *****************************************************************************
			type = Statement.Type_General;
		}

		public GeneralStatement(Statement head, ICollection<Statement> statements, Statement
			 post)
			: this()
		{
			first = head;
			stats.AddWithKey(head, head.id);
			HashSet<Statement> set = new HashSet<Statement>(statements);
			set.Remove(head);
			foreach (Statement st in set)
			{
				stats.AddWithKey(st, st.id);
			}
			this.post = post;
		}

		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer buf = new TextBuffer();
			if (IsLabeled())
			{
				buf.AppendIndent(indent).Append("label").Append(this.id.ToString()).Append(":").AppendLineSeparator
					();
			}
			buf.AppendIndent(indent).Append("abstract statement {").AppendLineSeparator();
			foreach (Statement stat in stats)
			{
				buf.Append(stat.ToJava(indent + 1, tracer));
			}
			buf.AppendIndent(indent).Append("}");
			return buf;
		}
	}
}
