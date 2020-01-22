// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Code.Cfg;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class SynchronizedStatement : Statement
	{
		private Statement body;

		private readonly List<Exprent> headexprent = new List<Exprent>(1);

		public SynchronizedStatement()
		{
			// *****************************************************************************
			// constructors
			// *****************************************************************************
			type = Type_Syncronized;
			headexprent.Add(null);
		}

		public SynchronizedStatement(Statement head, Statement body, Statement exc)
			: this()
		{
			first = head;
			stats.AddWithKey(head, head.id);
			this.body = body;
			stats.AddWithKey(body, body.id);
			stats.AddWithKey(exc, exc.id);
			List<StatEdge> lstSuccs = body.GetSuccessorEdges(Statedge_Direct_All);
			if (!(lstSuccs.Count == 0))
			{
				StatEdge edge = lstSuccs[0];
				if (edge.GetType() == StatEdge.Type_Regular)
				{
					post = edge.GetDestination();
				}
			}
		}

		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer buf = new TextBuffer();
			buf.Append(ExprProcessor.ListToJava(varDefinitions, indent, tracer));
			buf.Append(first.ToJava(indent, tracer));
			if (IsLabeled())
			{
				buf.AppendIndent(indent).Append("label").Append(this.id.ToString()).Append(":").AppendLineSeparator
					();
				tracer.IncrementCurrentSourceLine();
			}
			buf.AppendIndent(indent).Append(headexprent[0].ToJava(indent, tracer)).Append(" {"
				).AppendLineSeparator();
			tracer.IncrementCurrentSourceLine();
			buf.Append(ExprProcessor.JmpWrapper(body, indent + 1, true, tracer));
			buf.AppendIndent(indent).Append("}").AppendLineSeparator();
			MapMonitorExitInstr(tracer);
			tracer.IncrementCurrentSourceLine();
			return buf;
		}

		private void MapMonitorExitInstr(BytecodeMappingTracer tracer)
		{
			BasicBlock block = body.GetBasichead().GetBlock();
			if (!block.GetSeq().IsEmpty() && block.GetLastInstruction().opcode == ICodeConstants
				.opc_monitorexit)
			{
				int offset = block.GetOldOffset(block.Size() - 1);
				if (offset > -1)
				{
					tracer.AddMapping(offset);
				}
			}
		}

		public override void InitExprents()
		{
			headexprent[0] = first.GetExprents().RemoveAtReturningValue(first.GetExprents().Count
				 - 1);
		}

		public override List<object> GetSequentialObjects()
		{
			List<object> lst = new List<object>(stats);
			lst.Add(1, headexprent[0]);
			return lst;
		}

		public override void ReplaceExprent(Exprent oldexpr, Exprent newexpr)
		{
			if (headexprent[0] == oldexpr)
			{
				headexprent[0] = newexpr;
			}
		}

		public override void ReplaceStatement(Statement oldstat, Statement newstat)
		{
			if (body == oldstat)
			{
				body = newstat;
			}
			base.ReplaceStatement(oldstat, newstat);
		}

		public virtual void RemoveExc()
		{
			Statement exc = stats[2];
			SequenceHelper.DestroyStatementContent(exc, true);
			stats.RemoveWithKey(exc.id);
		}

		public override Statement GetSimpleCopy()
		{
			return new SynchronizedStatement();
		}

		public override void InitSimpleCopy()
		{
			first = stats[0];
			body = stats[1];
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual Statement GetBody()
		{
			return body;
		}

		public virtual List<Exprent> GetHeadexprentList()
		{
			return headexprent;
		}
	}
}
