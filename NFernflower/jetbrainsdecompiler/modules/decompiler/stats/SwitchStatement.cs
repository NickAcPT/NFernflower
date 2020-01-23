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
	public class SwitchStatement : Statement
	{
		private List<Statement> caseStatements = new List<Statement>();

		private List<List<StatEdge>> caseEdges = new List<List<StatEdge>>();

		private List<List<Exprent>> caseValues = new List<List<Exprent>>();

		private StatEdge default_edge;

		private readonly List<Exprent> headexprent = new List<Exprent>(1);

		private SwitchStatement()
		{
			// *****************************************************************************
			// private fields
			// *****************************************************************************
			// *****************************************************************************
			// constructors
			// *****************************************************************************
			type = Type_Switch;
			headexprent.Add(null);
		}

		private SwitchStatement(Statement head, Statement poststat)
			: this()
		{
			first = head;
			stats.AddWithKey(head, head.id);
			// find post node
			HashSet<Statement> lstNodes = new HashSet<Statement>(head.GetNeighbours(StatEdge.
				Type_Regular, Direction_Forward));
			// cluster nodes
			if (poststat != null)
			{
				post = poststat;
				lstNodes.Remove(post);
			}
			default_edge = head.GetSuccessorEdges(Statement.Statedge_Direct_All)[0];
			foreach (Statement st in lstNodes)
			{
				stats.AddWithKey(st, st.id);
			}
		}

		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public static Statement IsHead(Statement head)
		{
			if (head.type == Statement.Type_Basicblock && head.GetLastBasicType() == Statement
				.Lastbasictype_Switch)
			{
				List<Statement> lst = new List<Statement>();
				if (DecHelper.IsChoiceStatement(head, lst))
				{
					Statement post = lst.RemoveAtReturningValue(0);
					foreach (Statement st in lst)
					{
						if (st.IsMonitorEnter())
						{
							return null;
						}
					}
					if (DecHelper.CheckStatementExceptions(lst))
					{
						return new SwitchStatement(head, post);
					}
				}
			}
			return null;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			SwitchHelper.Simplify(this);
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
			VarType switch_type = headexprent[0].GetExprType();
			for (int i = 0; i < caseStatements.Count; i++)
			{
				Statement stat = caseStatements[i];
				List<StatEdge> edges = caseEdges[i];
				List<Exprent> values = caseValues[i];
				for (int j = 0; j < edges.Count; j++)
				{
					if (edges[j] == default_edge)
					{
						buf.AppendIndent(indent).Append("default:").AppendLineSeparator();
					}
					else
					{
						buf.AppendIndent(indent).Append("case ");
						Exprent value = values[j];
						if (value is ConstExprent)
						{
							value = value.Copy();
							((ConstExprent)value).SetConstType(switch_type);
						}
						if (value is FieldExprent && ((FieldExprent)value).IsStatic())
						{
							// enum values
							buf.Append(((FieldExprent)value).GetName());
						}
						else
						{
							buf.Append(value.ToJava(indent, tracer));
						}
						buf.Append(":").AppendLineSeparator();
					}
					tracer.IncrementCurrentSourceLine();
				}
				buf.Append(ExprProcessor.JmpWrapper(stat, indent + 1, false, tracer));
			}
			buf.AppendIndent(indent).Append("}").AppendLineSeparator();
			tracer.IncrementCurrentSourceLine();
			return buf;
		}

		public override void InitExprents()
		{
			SwitchExprent swexpr = (SwitchExprent)first.GetExprents().RemoveAtReturningValue(
				first.GetExprents().Count - 1);
			swexpr.SetCaseValues(caseValues);
			headexprent[0] = swexpr;
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
			for (int i = 0; i < caseStatements.Count; i++)
			{
				if (caseStatements[i] == oldstat)
				{
					caseStatements[i] = newstat;
				}
			}
			base.ReplaceStatement(oldstat, newstat);
		}

		public override Statement GetSimpleCopy()
		{
			return new SwitchStatement();
		}

		public override void InitSimpleCopy()
		{
			first = stats[0];
			default_edge = first.GetSuccessorEdges(Statement.Statedge_Direct_All)[0];
			SortEdgesAndNodes();
		}

		// *****************************************************************************
		// private methods
		// *****************************************************************************
		public virtual void SortEdgesAndNodes()
		{
			Dictionary<StatEdge, int> mapEdgeIndex = new Dictionary<StatEdge, int>();
			List<StatEdge> lstFirstSuccs = first.GetSuccessorEdges(Statedge_Direct_All);
			for (int i = 0; i < lstFirstSuccs.Count; i++)
			{
				Sharpen.Collections.Put(mapEdgeIndex, lstFirstSuccs[i], i == 0 ? lstFirstSuccs.Count
					 : i);
			}
			// case values
			BasicBlockStatement bbstat = (BasicBlockStatement)first;
			int[] values = ((SwitchInstruction)bbstat.GetBlock().GetLastInstruction()).GetValues
				();
			List<Statement> nodes = new List<Statement>(stats.Count - 1);
			List<List<int?>> edges = new List<List<int?>>(stats.Count - 1);
			// collect regular edges
			for (int i = 1; i < stats.Count; i++)
			{
				Statement stat = stats[i];
				List<int?> lst = new List<int?>();
				foreach (StatEdge edge in stat.GetPredecessorEdges(StatEdge.Type_Regular))
				{
					if (edge.GetSource() == first)
					{
						lst.Add(mapEdgeIndex.GetOrNullable(edge));
					}
				}
				lst.Sort();
				nodes.Add(stat);
				edges.Add(lst);
			}
			// collect exit edges
			List<StatEdge> lstExitEdges = first.GetSuccessorEdges(StatEdge.Type_Break | StatEdge
				.Type_Continue);
			while (!(lstExitEdges.Count == 0))
			{
				StatEdge edge = lstExitEdges[0];
				List<int?> lst = new List<int?>();
				for (int i = lstExitEdges.Count - 1; i >= 0; i--)
				{
					StatEdge edgeTemp = lstExitEdges[i];
					if (edgeTemp.GetDestination() == edge.GetDestination() && edgeTemp.GetType() == edge
						.GetType())
					{
						lst.Add(mapEdgeIndex.GetOrNullable(edgeTemp));
						lstExitEdges.RemoveAtReturningValue(i);
					}
				}
				lst.Sort();
				nodes.Add(null);
				edges.Add(lst);
			}
			// sort edges (bubblesort)
			for (int i = 0; i < edges.Count - 1; i++)
			{
				for (int j = edges.Count - 1; j > i; j--)
				{
					if (edges[j - 1][0] > edges[j][0])
					{
						edges[j] = edges[j - 1] = edges[j];
						nodes[j] = nodes[j - 1] = nodes[j];
					}
				}
			}
			// sort statement cliques
			for (int index = 0; index < nodes.Count; index++)
			{
				Statement stat = nodes[index];
				if (stat != null)
				{
					HashSet<Statement> setPreds = new HashSet<Statement>(stat.GetNeighbours(StatEdge.
						Type_Regular, Direction_Backward));
					setPreds.Remove(first);
					if (!(setPreds.Count == 0))
					{
						Statement pred = new Sharpen.EnumeratorAdapter<Statement>(setPreds.GetEnumerator()).Next();
						// assumption: at most one predecessor node besides the head. May not hold true for obfuscated code.
						for (int j = 0; j < nodes.Count; j++)
						{
							if (j != (index - 1) && nodes[j] == pred)
							{
								nodes.Add(j + 1, stat);
								edges.Add(j + 1, edges[index]);
								if (j > index)
								{
									nodes.RemoveAtReturningValue(index);
									edges.RemoveAtReturningValue(index);
									index--;
								}
								else
								{
									nodes.RemoveAtReturningValue(index + 1);
									edges.RemoveAtReturningValue(index + 1);
								}
								break;
							}
						}
					}
				}
			}
			// translate indices back into edges
			List<List<StatEdge>> lstEdges = new List<List<StatEdge>>(edges.Count);
			List<List<Exprent>> lstValues = new List<List<Exprent>>(edges.Count);
			foreach (List<int?> lst in edges)
			{
				List<StatEdge> lste = new List<StatEdge>(lst.Count);
				List<Exprent> lstv = new List<Exprent>(lst.Count);
				List<StatEdge> lstSuccs = first.GetSuccessorEdges(Statedge_Direct_All);
				foreach (int? @in in lst)
				{
					int? index = @in == lstSuccs.Count ? 0 : @in; 
					if (!index.HasValue) continue;
					lste.Add(lstSuccs[index.Value]);
					lstv.Add(index == 0 ? null : new ConstExprent(values[index.Value - 1], false, null));
				}
				lstEdges.Add(lste);
				lstValues.Add(lstv);
			}
			// replace null statements with dummy basic blocks
			for (int i = 0; i < nodes.Count; i++)
			{
				if (nodes[i] == null)
				{
					BasicBlockStatement bstat = new BasicBlockStatement(new BasicBlock(DecompilerContext
						.GetCounterContainer().GetCounterAndIncrement(CounterContainer.Statement_Counter
						)));
					StatEdge sample_edge = lstEdges[i][0];
					bstat.AddSuccessor(new StatEdge(sample_edge.GetType(), bstat, sample_edge.GetDestination
						(), sample_edge.closure));
					foreach (StatEdge edge in lstEdges[i])
					{
						edge.GetSource().ChangeEdgeType(Direction_Forward, edge, StatEdge.Type_Regular);
						edge.closure.GetLabelEdges().Remove(edge);
						edge.GetDestination().RemovePredecessor(edge);
						edge.GetSource().ChangeEdgeNode(Direction_Forward, edge, bstat);
						bstat.AddPredecessor(edge);
					}
					nodes[i] = bstat;
					stats.AddWithKey(bstat, bstat.id);
					bstat.SetParent(this);
				}
			}
			caseStatements = nodes;
			caseEdges = lstEdges;
			caseValues = lstValues;
		}

		public virtual List<Exprent> GetHeadexprentList()
		{
			return headexprent;
		}

		public virtual Exprent GetHeadexprent()
		{
			return headexprent[0];
		}

		public virtual List<List<StatEdge>> GetCaseEdges()
		{
			return caseEdges;
		}

		public virtual List<Statement> GetCaseStatements()
		{
			return caseStatements;
		}

		public virtual StatEdge GetDefault_edge()
		{
			return default_edge;
		}

		public virtual List<List<Exprent>> GetCaseValues()
		{
			return caseValues;
		}
	}
}
