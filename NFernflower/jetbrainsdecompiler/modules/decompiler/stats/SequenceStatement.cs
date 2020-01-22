// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class SequenceStatement : Statement
	{
		private SequenceStatement()
		{
			// *****************************************************************************
			// constructors
			// *****************************************************************************
			type = Statement.Type_Sequence;
		}

		public SequenceStatement(List<Statement> lst)
			: this()
		{
			lastBasicType = lst[lst.Count - 1].GetLastBasicType();
			foreach (var st in lst)
			{
				stats.AddWithKey(st, st.id);
			}
			first = stats[0];
		}

		private SequenceStatement(Statement head, Statement tail)
			: this(Sharpen.Arrays.AsList(head, tail))
		{
			List<StatEdge> lstSuccs = tail.GetSuccessorEdges(Statedge_Direct_All);
			if (!(lstSuccs.Count == 0))
			{
				StatEdge edge = lstSuccs[0];
				if (edge.GetType() == StatEdge.Type_Regular && edge.GetDestination() != head)
				{
					post = edge.GetDestination();
				}
			}
		}

		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public static Statement IsHead2Block(Statement head)
		{
			if (head.GetLastBasicType() != Statement.Lastbasictype_General)
			{
				return null;
			}
			// at most one outgoing edge
			StatEdge edge = null;
			List<StatEdge> lstSuccs = head.GetSuccessorEdges(Statedge_Direct_All);
			if (!(lstSuccs.Count == 0))
			{
				edge = lstSuccs[0];
			}
			if (edge != null && edge.GetType() == StatEdge.Type_Regular)
			{
				Statement stat = edge.GetDestination();
				if (stat != head && stat.GetPredecessorEdges(StatEdge.Type_Regular).Count == 1 &&
					 !stat.IsMonitorEnter())
				{
					if (stat.GetLastBasicType() == Statement.Lastbasictype_General)
					{
						if (DecHelper.CheckStatementExceptions(Sharpen.Arrays.AsList(head, stat)))
						{
							return new SequenceStatement(head, stat);
						}
					}
				}
			}
			return null;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer buf = new TextBuffer();
			bool islabeled = IsLabeled();
			buf.Append(ExprProcessor.ListToJava(varDefinitions, indent, tracer));
			if (islabeled)
			{
				buf.AppendIndent(indent++).Append("label").Append(this.id.ToString()).Append(": {"
					).AppendLineSeparator();
				tracer.IncrementCurrentSourceLine();
			}
			bool notempty = false;
			for (int i = 0; i < stats.Count; i++)
			{
				Statement st = stats[i];
				if (i > 0 && notempty)
				{
					buf.AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
				}
				TextBuffer str = ExprProcessor.JmpWrapper(st, indent, false, tracer);
				buf.Append(str);
				notempty = !str.ContainsOnlyWhitespaces();
			}
			if (islabeled)
			{
				buf.AppendIndent(indent - 1).Append("}").AppendLineSeparator();
				tracer.IncrementCurrentSourceLine();
			}
			return buf;
		}

		public override Statement GetSimpleCopy()
		{
			return new SequenceStatement();
		}
	}
}
