// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Struct.Match;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class IfStatement : Statement
	{
		public const int Iftype_If = 0;

		public const int Iftype_Ifelse = 1;

		public int iftype;

		private Statement ifstat;

		private Statement elsestat;

		private StatEdge ifedge;

		private StatEdge elseedge;

		private bool negated = false;

		private readonly List<Exprent> headexprent = new List<Exprent>(1);

		private IfStatement()
		{
			// *****************************************************************************
			// private fields
			// *****************************************************************************
			// contains IfExprent
			// *****************************************************************************
			// constructors
			// *****************************************************************************
			type = Type_If;
			headexprent.Add(null);
		}

		private IfStatement(Statement head, int regedges, Statement postst)
			: this()
		{
			first = head;
			stats.AddWithKey(head, head.id);
			List<StatEdge> lstHeadSuccs = head.GetSuccessorEdges(Statedge_Direct_All);
			switch (regedges)
			{
				case 0:
				{
					ifstat = null;
					elsestat = null;
					break;
				}

				case 1:
				{
					ifstat = null;
					elsestat = null;
					StatEdge edgeif = lstHeadSuccs[1];
					if (edgeif.GetType() != StatEdge.Type_Regular)
					{
						post = lstHeadSuccs[0].GetDestination();
					}
					else
					{
						post = edgeif.GetDestination();
						negated = true;
					}
					break;
				}

				case 2:
				{
					elsestat = lstHeadSuccs[0].GetDestination();
					ifstat = lstHeadSuccs[1].GetDestination();
					List<StatEdge> lstSucc = ifstat.GetSuccessorEdges(StatEdge.Type_Regular);
					List<StatEdge> lstSucc1 = elsestat.GetSuccessorEdges(StatEdge.Type_Regular);
					if (ifstat.GetPredecessorEdges(StatEdge.Type_Regular).Count > 1 || lstSucc.Count 
						> 1)
					{
						post = ifstat;
					}
					else if (elsestat.GetPredecessorEdges(StatEdge.Type_Regular).Count > 1 || lstSucc1
						.Count > 1)
					{
						post = elsestat;
					}
					else if (lstSucc.Count == 0)
					{
						post = elsestat;
					}
					else if (lstSucc1.Count == 0)
					{
						post = ifstat;
					}
					if (ifstat == post)
					{
						if (elsestat != post)
						{
							ifstat = elsestat;
							negated = true;
						}
						else
						{
							ifstat = null;
						}
						elsestat = null;
					}
					else if (elsestat == post)
					{
						elsestat = null;
					}
					else
					{
						post = postst;
					}
					if (elsestat == null)
					{
						regedges = 1;
					}
					break;
				}
			}
			// if without else
			ifedge = lstHeadSuccs[negated ? 0 : 1];
			elseedge = (regedges == 2) ? lstHeadSuccs[negated ? 1 : 0] : null;
			iftype = (regedges == 2) ? Iftype_Ifelse : Iftype_If;
			if (iftype == Iftype_If)
			{
				if (regedges == 0)
				{
					StatEdge edge = lstHeadSuccs[0];
					head.RemoveSuccessor(edge);
					edge.SetSource(this);
					this.AddSuccessor(edge);
				}
				else if (regedges == 1)
				{
					StatEdge edge = lstHeadSuccs[negated ? 1 : 0];
					head.RemoveSuccessor(edge);
				}
			}
			if (ifstat != null)
			{
				stats.AddWithKey(ifstat, ifstat.id);
			}
			if (elsestat != null)
			{
				stats.AddWithKey(elsestat, elsestat.id);
			}
			if (post == head)
			{
				post = this;
			}
		}

		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public static Statement IsHead(Statement head)
		{
			if (head.type == Type_Basicblock && head.GetLastBasicType() == Lastbasictype_If)
			{
				int regsize = head.GetSuccessorEdges(StatEdge.Type_Regular).Count;
				Statement p = null;
				bool ok = (regsize < 2);
				if (!ok)
				{
					List<Statement> lst = new List<Statement>();
					if (DecHelper.IsChoiceStatement(head, lst))
					{
						p = lst.RemoveAtReturningValue(0);
						foreach (Statement st in lst)
						{
							if (st.IsMonitorEnter())
							{
								return null;
							}
						}
						ok = DecHelper.CheckStatementExceptions(lst);
					}
				}
				if (ok)
				{
					return new IfStatement(head, regsize, p);
				}
			}
			return null;
		}

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
			if (ifstat == null)
			{
				bool semicolon = false;
				if (ifedge.@explicit)
				{
					semicolon = true;
					if (ifedge.GetType() == StatEdge.Type_Break)
					{
						// break
						buf.AppendIndent(indent + 1).Append("break");
					}
					else
					{
						// continue
						buf.AppendIndent(indent + 1).Append("continue");
					}
					if (ifedge.labeled)
					{
						buf.Append(" label").Append(ifedge.closure.id.ToString());
					}
				}
				if (semicolon)
				{
					buf.Append(";").AppendLineSeparator();
					tracer.IncrementCurrentSourceLine();
				}
			}
			else
			{
				buf.Append(ExprProcessor.JmpWrapper(ifstat, indent + 1, true, tracer));
			}
			bool elseif = false;
			if (elsestat != null)
			{
				if (elsestat.type == Statement.Type_If && (elsestat.varDefinitions.Count == 0) &&
					 (elsestat.GetFirst().GetExprents().Count == 0) && !elsestat.IsLabeled() && ((elsestat
					.GetSuccessorEdges(Statedge_Direct_All).Count == 0) || !elsestat.GetSuccessorEdges
					(Statedge_Direct_All)[0].@explicit))
				{
					// else if
					buf.AppendIndent(indent).Append("} else ");
					TextBuffer content = ExprProcessor.JmpWrapper(elsestat, indent, false, tracer);
					content.SetStart(TextUtil.GetIndentString(indent).Length);
					buf.Append(content);
					elseif = true;
				}
				else
				{
					BytecodeMappingTracer else_tracer = new BytecodeMappingTracer(tracer.GetCurrentSourceLine
						() + 1);
					TextBuffer content = ExprProcessor.JmpWrapper(elsestat, indent + 1, false, else_tracer
						);
					if (content.Length() > 0)
					{
						buf.AppendIndent(indent).Append("} else {").AppendLineSeparator();
						tracer.SetCurrentSourceLine(else_tracer.GetCurrentSourceLine());
						tracer.AddTracer(else_tracer);
						buf.Append(content);
					}
				}
			}
			if (!elseif)
			{
				buf.AppendIndent(indent).Append("}").AppendLineSeparator();
				tracer.IncrementCurrentSourceLine();
			}
			return buf;
		}

		public override void InitExprents()
		{
			IfExprent ifexpr = (IfExprent)first.GetExprents().RemoveAtReturningValue(first.GetExprents
				().Count - 1);
			if (negated)
			{
				ifexpr = (IfExprent)ifexpr.Copy();
				ifexpr.NegateIf();
			}
			headexprent[0] = ifexpr;
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
			base.ReplaceStatement(oldstat, newstat);
			if (ifstat == oldstat)
			{
				ifstat = newstat;
			}
			if (elsestat == oldstat)
			{
				elsestat = newstat;
			}
			List<StatEdge> lstSuccs = first.GetSuccessorEdges(Statedge_Direct_All);
			if (iftype == Iftype_If)
			{
				ifedge = lstSuccs[0];
				elseedge = null;
			}
			else
			{
				StatEdge edge0 = lstSuccs[0];
				StatEdge edge1 = lstSuccs[1];
				if (edge0.GetDestination() == ifstat)
				{
					ifedge = edge0;
					elseedge = edge1;
				}
				else
				{
					ifedge = edge1;
					elseedge = edge0;
				}
			}
		}

		public override Statement GetSimpleCopy()
		{
			IfStatement @is = new IfStatement();
			@is.iftype = this.iftype;
			@is.negated = this.negated;
			return @is;
		}

		public override void InitSimpleCopy()
		{
			first = stats[0];
			List<StatEdge> lstSuccs = first.GetSuccessorEdges(Statedge_Direct_All);
			ifedge = lstSuccs[(iftype == Iftype_If || negated) ? 0 : 1];
			if (stats.Count > 1)
			{
				ifstat = stats[1];
			}
			if (iftype == Iftype_Ifelse)
			{
				elseedge = lstSuccs[negated ? 1 : 0];
				elsestat = stats[2];
			}
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual Statement GetElsestat()
		{
			return elsestat;
		}

		public virtual void SetElsestat(Statement elsestat)
		{
			this.elsestat = elsestat;
		}

		public virtual Statement GetIfstat()
		{
			return ifstat;
		}

		public virtual void SetIfstat(Statement ifstat)
		{
			this.ifstat = ifstat;
		}

		public virtual bool IsNegated()
		{
			return negated;
		}

		public virtual void SetNegated(bool negated)
		{
			this.negated = negated;
		}

		public virtual List<Exprent> GetHeadexprentList()
		{
			return headexprent;
		}

		public virtual IfExprent GetHeadexprent()
		{
			return (IfExprent)headexprent[0];
		}

		public virtual void SetElseEdge(StatEdge elseedge)
		{
			this.elseedge = elseedge;
		}

		public virtual void SetIfEdge(StatEdge ifedge)
		{
			this.ifedge = ifedge;
		}

		public virtual StatEdge GetIfEdge()
		{
			return ifedge;
		}

		public virtual StatEdge GetElseEdge()
		{
			return elseedge;
		}

		// *****************************************************************************
		// IMatchable implementation
		// *****************************************************************************
		public override IIMatchable FindObject(MatchNode matchNode, int index)
		{
			IIMatchable @object = base.FindObject(matchNode, index);
			if (@object != null)
			{
				return @object;
			}
			if (matchNode.GetType() == MatchNode.Matchnode_Exprent)
			{
				string position = (string)matchNode.GetRuleValue(IMatchable.MatchProperties.Exprent_Position
					);
				if ("head".Equals(position))
				{
					return GetHeadexprent();
				}
			}
			return null;
		}

		public override bool Match(MatchNode matchNode, MatchEngine engine)
		{
			if (!base.Match(matchNode, engine))
			{
				return false;
			}
			int type = (int)matchNode.GetRuleValue(IMatchable.MatchProperties.Statement_Iftype
				);
			return type == null || this.iftype == type;
		}
	}
}
