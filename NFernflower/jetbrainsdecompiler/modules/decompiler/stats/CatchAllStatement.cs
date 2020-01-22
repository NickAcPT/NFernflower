// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class CatchAllStatement : Statement
	{
		private Statement handler;

		private bool isFinally__;

		private VarExprent monitor;

		private readonly List<VarExprent> vars = new List<VarExprent>();

		private CatchAllStatement()
		{
			// *****************************************************************************
			// constructors
			// *****************************************************************************
			type = Statement.Type_Catchall;
		}

		private CatchAllStatement(Statement head, Statement handler)
			: this()
		{
			first = head;
			stats.AddWithKey(head, head.id);
			this.handler = handler;
			stats.AddWithKey(handler, handler.id);
			List<StatEdge> lstSuccs = head.GetSuccessorEdges(Statedge_Direct_All);
			if (!(lstSuccs.Count == 0))
			{
				StatEdge edge = lstSuccs[0];
				if (edge.GetType() == StatEdge.Type_Regular)
				{
					post = edge.GetDestination();
				}
			}
			vars.Add(new VarExprent(DecompilerContext.GetCounterContainer().GetCounterAndIncrement
				(CounterContainer.Var_Counter), new VarType(ICodeConstants.Type_Object, 0, "java/lang/Throwable"
				), DecompilerContext.GetVarProcessor()));
		}

		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public static Statement IsHead(Statement head)
		{
			if (head.GetLastBasicType() != Statement.Lastbasictype_General)
			{
				return null;
			}
			HashSet<Statement> setHandlers = DecHelper.GetUniquePredExceptions(head);
			if (setHandlers.Count != 1)
			{
				return null;
			}
			foreach (StatEdge edge in head.GetSuccessorEdges(StatEdge.Type_Exception))
			{
				Statement exc = edge.GetDestination();
				if (edge.GetExceptions() == null && exc.GetLastBasicType() == Lastbasictype_General
					 && setHandlers.Contains(exc))
				{
					List<StatEdge> lstSuccs = exc.GetSuccessorEdges(Statedge_Direct_All);
					if ((lstSuccs.Count == 0) || lstSuccs[0].GetType() != StatEdge.Type_Regular)
					{
						if (head.IsMonitorEnter() || exc.IsMonitorEnter())
						{
							return null;
						}
						if (DecHelper.CheckStatementExceptions(Sharpen.Arrays.AsList(head, exc)))
						{
							return new CatchAllStatement(head, exc);
						}
					}
				}
			}
			return null;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			string new_line_separator = DecompilerContext.GetNewLineSeparator();
			TextBuffer buf = new TextBuffer();
			buf.Append(ExprProcessor.ListToJava(varDefinitions, indent, tracer));
			bool labeled = IsLabeled();
			if (labeled)
			{
				buf.AppendIndent(indent).Append("label").Append(this.id.ToString()).Append(":").AppendLineSeparator
					();
				tracer.IncrementCurrentSourceLine();
			}
			List<StatEdge> lstSuccs = first.GetSuccessorEdges(Statedge_Direct_All);
			if (first.type == Type_Trycatch && (first.varDefinitions.Count == 0) && isFinally__
				 && !labeled && !first.IsLabeled() && ((lstSuccs.Count == 0) || !lstSuccs[0].@explicit
				))
			{
				TextBuffer content = ExprProcessor.JmpWrapper(first, indent, true, tracer);
				content.SetLength(content.Length() - new_line_separator.Length);
				tracer.IncrementCurrentSourceLine(-1);
				buf.Append(content);
			}
			else
			{
				buf.AppendIndent(indent).Append("try {").AppendLineSeparator();
				tracer.IncrementCurrentSourceLine();
				buf.Append(ExprProcessor.JmpWrapper(first, indent + 1, true, tracer));
				buf.AppendIndent(indent).Append("}");
			}
			buf.Append(isFinally__ ? " finally" : " catch (" + vars[0].ToJava(indent, tracer)
				 + ")").Append(" {").AppendLineSeparator();
			tracer.IncrementCurrentSourceLine();
			if (monitor != null)
			{
				buf.AppendIndent(indent + 1).Append("if (").Append(monitor.ToJava(indent, tracer)
					).Append(") {").AppendLineSeparator();
				tracer.IncrementCurrentSourceLine();
			}
			buf.Append(ExprProcessor.JmpWrapper(handler, indent + 1 + (monitor != null ? 1 : 
				0), true, tracer));
			if (monitor != null)
			{
				buf.AppendIndent(indent + 1).Append("}").AppendLineSeparator();
				tracer.IncrementCurrentSourceLine();
			}
			buf.AppendIndent(indent).Append("}").AppendLineSeparator();
			tracer.IncrementCurrentSourceLine();
			return buf;
		}

		public override void ReplaceStatement(Statement oldstat, Statement newstat)
		{
			if (handler == oldstat)
			{
				handler = newstat;
			}
			base.ReplaceStatement(oldstat, newstat);
		}

		public override Statement GetSimpleCopy()
		{
			CatchAllStatement cas = new CatchAllStatement();
			cas.isFinally__ = this.isFinally__;
			if (this.monitor != null)
			{
				cas.monitor = new VarExprent(DecompilerContext.GetCounterContainer().GetCounterAndIncrement
					(CounterContainer.Var_Counter), VarType.Vartype_Int, DecompilerContext.GetVarProcessor
					());
			}
			if (!(this.vars.Count == 0))
			{
				cas.vars.Add(new VarExprent(DecompilerContext.GetCounterContainer().GetCounterAndIncrement
					(CounterContainer.Var_Counter), new VarType(ICodeConstants.Type_Object, 0, "java/lang/Throwable"
					), DecompilerContext.GetVarProcessor()));
			}
			return cas;
		}

		public override void InitSimpleCopy()
		{
			first = stats[0];
			handler = stats[1];
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual Statement GetHandler()
		{
			return handler;
		}

		public virtual bool IsFinally()
		{
			return isFinally__;
		}

		public virtual void SetFinally(bool isFinally)
		{
			this.isFinally__ = isFinally;
		}

		public virtual VarExprent GetMonitor()
		{
			return monitor;
		}

		public virtual void SetMonitor(VarExprent monitor)
		{
			this.monitor = monitor;
		}

		public virtual List<VarExprent> GetVars()
		{
			return vars;
		}
	}
}
