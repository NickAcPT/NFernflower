// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Code.Cfg;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class CatchStatement : Statement
	{
		private readonly List<List<string>> exctstrings = new List<List<string>>();

		private readonly List<VarExprent> vars = new List<VarExprent>();

		private CatchStatement()
		{
			// *****************************************************************************
			// constructors
			// *****************************************************************************
			type = Type_Trycatch;
		}

		private CatchStatement(Statement head, Statement next, HashSet<Statement> setHandlers
			)
			: this()
		{
			first = head;
			stats.AddWithKey(first, first.id);
			foreach (StatEdge edge in head.GetSuccessorEdges(StatEdge.Type_Exception))
			{
				Statement stat = edge.GetDestination();
				if (setHandlers.Contains(stat))
				{
					stats.AddWithKey(stat, stat.id);
					exctstrings.Add(new List<string>(edge.GetExceptions()));
					vars.Add(new VarExprent(DecompilerContext.GetCounterContainer().GetCounterAndIncrement
						(CounterContainer.Var_Counter), new VarType(ICodeConstants.Type_Object, 0, edge.
						GetExceptions()[0]), DecompilerContext.GetVarProcessor()));
				}
			}
			// FIXME: for now simply the first type. Should get the first common superclass when possible.
			if (next != null)
			{
				post = next;
			}
		}

		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public static Statement IsHead(Statement head)
		{
			if (head.GetLastBasicType() != Lastbasictype_General)
			{
				return null;
			}
			HashSet<Statement> setHandlers = DecHelper.GetUniquePredExceptions(head);
			if (!(setHandlers.Count == 0))
			{
				int hnextcount = 0;
				// either no statements with connection to next, or more than 1
				Statement next = null;
				List<StatEdge> lstHeadSuccs = head.GetSuccessorEdges(Statedge_Direct_All);
				if (!(lstHeadSuccs.Count == 0) && lstHeadSuccs[0].GetType() == StatEdge.Type_Regular)
				{
					next = lstHeadSuccs[0].GetDestination();
					hnextcount = 2;
				}
				foreach (StatEdge edge in head.GetSuccessorEdges(StatEdge.Type_Exception))
				{
					Statement stat = edge.GetDestination();
					bool handlerok = true;
					if (edge.GetExceptions() != null && setHandlers.Contains(stat))
					{
						if (stat.GetLastBasicType() != Lastbasictype_General)
						{
							handlerok = false;
						}
						else
						{
							List<StatEdge> lstStatSuccs = stat.GetSuccessorEdges(Statedge_Direct_All);
							if (!(lstStatSuccs.Count == 0) && lstStatSuccs[0].GetType() == StatEdge.Type_Regular)
							{
								Statement statn = lstStatSuccs[0].GetDestination();
								if (next == null)
								{
									next = statn;
								}
								else if (next != statn)
								{
									handlerok = false;
								}
								if (handlerok)
								{
									hnextcount++;
								}
							}
						}
					}
					else
					{
						handlerok = false;
					}
					if (!handlerok)
					{
						setHandlers.Remove(stat);
					}
				}
				if (hnextcount != 1 && !(setHandlers.Count == 0))
				{
					List<Statement> lst = new List<Statement>();
					lst.Add(head);
					Sharpen.Collections.AddAll(lst, setHandlers);
					foreach (Statement st in lst)
					{
						if (st.IsMonitorEnter())
						{
							return null;
						}
					}
					if (DecHelper.CheckStatementExceptions(lst))
					{
						return new CatchStatement(head, next, setHandlers);
					}
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
			buf.AppendIndent(indent).Append("try {").AppendLineSeparator();
			tracer.IncrementCurrentSourceLine();
			buf.Append(ExprProcessor.JmpWrapper(first, indent + 1, true, tracer));
			buf.AppendIndent(indent).Append("}");
			for (int i = 1; i < stats.Count; i++)
			{
				Statement stat = stats[i];
				// map first instruction storing the exception to the catch statement
				BasicBlock block = stat.GetBasichead().GetBlock();
				if (!block.GetSeq().IsEmpty() && block.GetInstruction(0).opcode == ICodeConstants
					.opc_astore)
				{
					int offset = block.GetOldOffset(0);
					if (offset > -1)
					{
						tracer.AddMapping(offset);
					}
				}
				buf.Append(" catch (");
				List<string> exception_types = exctstrings[i - 1];
				if (exception_types.Count > 1)
				{
					// multi-catch, Java 7 style
					for (int exc_index = 1; exc_index < exception_types.Count; ++exc_index)
					{
						VarType exc_type = new VarType(ICodeConstants.Type_Object, 0, exception_types[exc_index
							]);
						string exc_type_name = ExprProcessor.GetCastTypeName(exc_type);
						buf.Append(exc_type_name).Append(" | ");
					}
				}
				buf.Append(vars[i - 1].ToJava(indent, tracer));
				buf.Append(") {").AppendLineSeparator();
				tracer.IncrementCurrentSourceLine();
				buf.Append(ExprProcessor.JmpWrapper(stat, indent + 1, false, tracer)).AppendIndent
					(indent).Append("}");
			}
			buf.AppendLineSeparator();
			tracer.IncrementCurrentSourceLine();
			return buf;
		}

		public override Statement GetSimpleCopy()
		{
			CatchStatement cs = new CatchStatement();
			foreach (List<string> exc in this.exctstrings)
			{
				cs.exctstrings.Add(new List<string>(exc));
				cs.vars.Add(new VarExprent(DecompilerContext.GetCounterContainer().GetCounterAndIncrement
					(CounterContainer.Var_Counter), new VarType(ICodeConstants.Type_Object, 0, exc[0
					]), DecompilerContext.GetVarProcessor()));
			}
			return cs;
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual List<VarExprent> GetVars()
		{
			return vars;
		}
	}
}
