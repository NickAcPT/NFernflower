// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class DoStatement : Statement
	{
		public const int Loop_Do = 0;

		public const int Loop_Dowhile = 1;

		public const int Loop_While = 2;

		public const int Loop_For = 3;

		private int looptype;

		private readonly List<Exprent> initExprent = new List<Exprent>();

		private readonly List<Exprent> conditionExprent = new List<Exprent>();

		private readonly List<Exprent> incExprent = new List<Exprent>();

		private DoStatement()
		{
			// *****************************************************************************
			// constructors
			// *****************************************************************************
			type = Statement.Type_Do;
			looptype = Loop_Do;
			initExprent.Add(null);
			conditionExprent.Add(null);
			incExprent.Add(null);
		}

		private DoStatement(Statement head)
			: this()
		{
			first = head;
			stats.AddWithKey(first, first.id);
		}

		// post is always null!
		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public static Statement IsHead(Statement head)
		{
			if (head.GetLastBasicType() == Lastbasictype_General && !head.IsMonitorEnter())
			{
				// at most one outgoing edge
				StatEdge edge = null;
				List<StatEdge> lstSuccs = head.GetSuccessorEdges(Statedge_Direct_All);
				if (!(lstSuccs.Count == 0))
				{
					edge = lstSuccs[0];
				}
				// regular loop
				if (edge != null && edge.GetType() == StatEdge.Type_Regular && edge.GetDestination
					() == head)
				{
					return new DoStatement(head);
				}
				// continues
				if (head.type != Type_Do && (edge == null || edge.GetType() != StatEdge.Type_Regular
					) && head.GetContinueSet().Contains(head.GetBasichead()))
				{
					return new DoStatement(head);
				}
			}
			return null;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer buf = new TextBuffer();
			buf.Append(ExprProcessor.ListToJava(varDefinitions, indent, tracer));
			if (IsLabeled())
			{
				buf.AppendIndent(indent).Append("label").Append(this.id.ToString()).Append(":").AppendLineSeparator
					();
				tracer.IncrementCurrentSourceLine();
			}
			switch (looptype)
			{
				case Loop_Do:
				{
					buf.AppendIndent(indent).Append("while(true) {").AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
					buf.Append(ExprProcessor.JmpWrapper(first, indent + 1, false, tracer));
					buf.AppendIndent(indent).Append("}").AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
					break;
				}

				case Loop_Dowhile:
				{
					buf.AppendIndent(indent).Append("do {").AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
					buf.Append(ExprProcessor.JmpWrapper(first, indent + 1, false, tracer));
					buf.AppendIndent(indent).Append("} while(").Append(conditionExprent[0].ToJava(indent
						, tracer)).Append(");").AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
					break;
				}

				case Loop_While:
				{
					buf.AppendIndent(indent).Append("while(").Append(conditionExprent[0].ToJava(indent
						, tracer)).Append(") {").AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
					buf.Append(ExprProcessor.JmpWrapper(first, indent + 1, false, tracer));
					buf.AppendIndent(indent).Append("}").AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
					break;
				}

				case Loop_For:
				{
					buf.AppendIndent(indent).Append("for(");
					if (initExprent[0] != null)
					{
						buf.Append(initExprent[0].ToJava(indent, tracer));
					}
					buf.Append("; ").Append(conditionExprent[0].ToJava(indent, tracer)).Append("; ").
						Append(incExprent[0].ToJava(indent, tracer)).Append(") {").AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
					buf.Append(ExprProcessor.JmpWrapper(first, indent + 1, false, tracer));
					buf.AppendIndent(indent).Append("}").AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
					break;
				}
			}
			return buf;
		}

		public override List<object> GetSequentialObjects()
		{
			List<object> lst = new List<object>();
			switch (looptype)
			{
				case Loop_For:
				{
					if (GetInitExprent() != null)
					{
						lst.Add(GetInitExprent());
					}
					goto case Loop_While;
				}

				case Loop_While:
				{
					lst.Add(GetConditionExprent());
					break;
				}
			}
			lst.Add(first);
			switch (looptype)
			{
				case Loop_Dowhile:
				{
					lst.Add(GetConditionExprent());
					break;
				}

				case Loop_For:
				{
					lst.Add(GetIncExprent());
					break;
				}
			}
			return lst;
		}

		public override void ReplaceExprent(Exprent oldexpr, Exprent newexpr)
		{
			if (initExprent[0] == oldexpr)
			{
				initExprent[0] = newexpr;
			}
			if (conditionExprent[0] == oldexpr)
			{
				conditionExprent[0] = newexpr;
			}
			if (incExprent[0] == oldexpr)
			{
				incExprent[0] = newexpr;
			}
		}

		public override Statement GetSimpleCopy()
		{
			return new DoStatement();
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual List<Exprent> GetInitExprentList()
		{
			return initExprent;
		}

		public virtual List<Exprent> GetConditionExprentList()
		{
			return conditionExprent;
		}

		public virtual List<Exprent> GetIncExprentList()
		{
			return incExprent;
		}

		public virtual Exprent GetConditionExprent()
		{
			return conditionExprent[0];
		}

		public virtual void SetConditionExprent(Exprent conditionExprent)
		{
			this.conditionExprent[0] = conditionExprent;
		}

		public virtual Exprent GetIncExprent()
		{
			return incExprent[0];
		}

		public virtual void SetIncExprent(Exprent incExprent)
		{
			this.incExprent[0] = incExprent;
		}

		public virtual Exprent GetInitExprent()
		{
			return initExprent[0];
		}

		public virtual void SetInitExprent(Exprent initExprent)
		{
			this.initExprent[0] = initExprent;
		}

		public virtual int GetLooptype()
		{
			return looptype;
		}

		public virtual void SetLooptype(int looptype)
		{
			this.looptype = looptype;
		}
	}
}
