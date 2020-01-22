/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System;
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Struct.Match;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Stats
{
	public class Statement : IIMatchable
	{
		public const int Statedge_All = unchecked((int)(0x80000000));

		public const int Statedge_Direct_All = unchecked((int)(0x40000000));

		public const int Direction_Backward = 0;

		public const int Direction_Forward = 1;

		public const int Type_General = 0;

		public const int Type_If = 2;

		public const int Type_Do = 5;

		public const int Type_Switch = 6;

		public const int Type_Trycatch = 7;

		public const int Type_Basicblock = 8;

		public const int Type_Syncronized = 10;

		public const int Type_Placeholder = 11;

		public const int Type_Catchall = 12;

		public const int Type_Root = 13;

		public const int Type_Dummyexit = 14;

		public const int Type_Sequence = 15;

		public const int Lastbasictype_If = 0;

		public const int Lastbasictype_Switch = 1;

		public const int Lastbasictype_General = 2;

		public int type;

		public int id;

		private readonly IDictionary<int, List<StatEdge>> mapSuccEdges = new Dictionary<
			int, List<StatEdge>>();

		private readonly IDictionary<int, List<StatEdge>> mapPredEdges = new Dictionary<
			int, List<StatEdge>>();

		private readonly IDictionary<int, List<Statement>> mapSuccStates = new Dictionary
			<int, List<Statement>>();

		private readonly IDictionary<int, List<Statement>> mapPredStates = new Dictionary
			<int, List<Statement>>();

		protected internal readonly VBStyleCollection<Statement, int> stats = new VBStyleCollection
			<Statement, int>();

		protected internal Statement parent;

		protected internal Statement first;

		protected internal List<Exprent> exprents;

		protected internal readonly HashSet<StatEdge> labelEdges = new HashSet<StatEdge>(
			);

		protected internal readonly List<Exprent> varDefinitions = new List<Exprent>();

		private bool copied = false;

		protected internal Statement post;

		protected internal int lastBasicType = Lastbasictype_General;

		protected internal bool isMonitorEnter__;

		protected internal bool containsMonitorExit;

		protected internal HashSet<Statement> continueSet = new HashSet<Statement>();

		//public static final int TYPE_FINALLY = 9;
		// *****************************************************************************
		// public fields
		// *****************************************************************************
		// *****************************************************************************
		// private fields
		// *****************************************************************************
		// statement as graph
		// copied statement, s. deobfuscating of irreducible CFGs
		// relevant for the first stage of processing only
		// set to null after initializing of the statement structure
		// *****************************************************************************
		// initializers
		// *****************************************************************************
		// set statement id
		// *****************************************************************************
		// public methods
		// *****************************************************************************
		public virtual void ClearTempInformation()
		{
			post = null;
			continueSet = null;
			copied = false;
			// FIXME: used in FlattenStatementsHelper.flattenStatement()! check and remove
			//lastBasicType = LASTBASICTYPE_GENERAL;
			isMonitorEnter__ = false;
			containsMonitorExit = false;
			ProcessMap(mapSuccEdges);
			ProcessMap(mapPredEdges);
			ProcessMap(mapSuccStates);
			ProcessMap(mapPredStates);
		}

		private static void ProcessMap<T>(IDictionary<int, List<T>> map)
		{
			Sharpen.Collections.Remove(map, StatEdge.Type_Exception);
			List<T> lst = map.GetOrNull(Statedge_Direct_All);
			if (lst != null)
			{
				Sharpen.Collections.Put(map, Statedge_All, new List<T>(lst));
			}
			else
			{
				Sharpen.Collections.Remove(map, Statedge_All);
			}
		}

		public virtual void CollapseNodesToStatement(Statement stat)
		{
			Statement head = stat.GetFirst();
			Statement post = stat.GetPost();
			VBStyleCollection<Statement, int> setNodes = stat.GetStats();
			// post edges
			if (post != null)
			{
				foreach (StatEdge edge in post.GetEdges(Statedge_Direct_All, Direction_Backward))
				{
					if (stat.ContainsStatementStrict(edge.GetSource()))
					{
						edge.GetSource().ChangeEdgeType(Direction_Forward, edge, StatEdge.Type_Break);
						stat.AddLabeledEdge(edge);
					}
				}
			}
			// regular head edges
			foreach (StatEdge prededge in head.GetAllPredecessorEdges())
			{
				if (prededge.GetType() != StatEdge.Type_Exception && stat.ContainsStatementStrict
					(prededge.GetSource()))
				{
					prededge.GetSource().ChangeEdgeType(Direction_Forward, prededge, StatEdge.Type_Continue
						);
					stat.AddLabeledEdge(prededge);
				}
				head.RemovePredecessor(prededge);
				prededge.GetSource().ChangeEdgeNode(Direction_Forward, prededge, stat);
				stat.AddPredecessor(prededge);
			}
			if (setNodes.ContainsKey(first.id))
			{
				first = stat;
			}
			// exception edges
			HashSet<Statement> setHandlers = new HashSet<Statement>(head.GetNeighbours(StatEdge
				.Type_Exception, Direction_Forward));
			foreach (Statement node in setNodes)
			{
				setHandlers.RetainAll(node.GetNeighbours(StatEdge.Type_Exception, Direction_Forward
					));
			}
			if (!(setHandlers.Count == 0))
			{
				foreach (StatEdge edge in head.GetEdges(StatEdge.Type_Exception, Direction_Forward
					))
				{
					Statement handler = edge.GetDestination();
					if (setHandlers.Contains(handler))
					{
						if (!setNodes.ContainsKey(handler.id))
						{
							stat.AddSuccessor(new StatEdge(stat, handler, edge.GetExceptions()));
						}
					}
				}
				foreach (Statement node in setNodes)
				{
					foreach (StatEdge edge in node.GetEdges(StatEdge.Type_Exception, Direction_Forward
						))
					{
						if (setHandlers.Contains(edge.GetDestination()))
						{
							node.RemoveSuccessor(edge);
						}
					}
				}
			}
			if (post != null && !stat.GetNeighbours(StatEdge.Type_Exception, Direction_Forward
				).Contains(post))
			{
				// TODO: second condition redundant?
				stat.AddSuccessor(new StatEdge(StatEdge.Type_Regular, stat, post));
			}
			// adjust statement collection
			foreach (Statement st in setNodes)
			{
				stats.RemoveWithKey(st.id);
			}
			stats.AddWithKey(stat, stat.id);
			stat.SetAllParent();
			stat.SetParent(this);
			stat.BuildContinueSet();
			// monitorenter and monitorexit
			stat.BuildMonitorFlags();
			if (stat.type == Type_Switch)
			{
				// special case switch, sorting leaf nodes
				((SwitchStatement)stat).SortEdgesAndNodes();
			}
		}

		public virtual void SetAllParent()
		{
			foreach (Statement st in stats)
			{
				st.SetParent(this);
			}
		}

		public virtual void AddLabeledEdge(StatEdge edge)
		{
			if (edge.closure != null)
			{
				edge.closure.GetLabelEdges().Remove(edge);
			}
			edge.closure = this;
			this.GetLabelEdges().Add(edge);
		}

		private void AddEdgeDirectInternal(int direction, StatEdge edge, int edgetype)
		{
			IDictionary<int, List<StatEdge>> mapEdges = direction == Direction_Backward ? mapPredEdges
				 : mapSuccEdges;
			IDictionary<int, List<Statement>> mapStates = direction == Direction_Backward ? 
				mapPredStates : mapSuccStates;
			mapEdges.ComputeIfAbsent(edgetype, (int k) => new List<StatEdge>()).Add(edge);
			mapStates.ComputeIfAbsent(edgetype, (int k) => new List<Statement>()).Add(direction
				 == Direction_Backward ? edge.GetSource() : edge.GetDestination());
		}

		private void AddEdgeInternal(int direction, StatEdge edge)
		{
			int type = edge.GetType();
			int[] arrtypes;
			if (type == StatEdge.Type_Exception)
			{
				arrtypes = new int[] { Statedge_All, StatEdge.Type_Exception };
			}
			else
			{
				arrtypes = new int[] { Statedge_All, Statedge_Direct_All, type };
			}
			foreach (int edgetype in arrtypes)
			{
				AddEdgeDirectInternal(direction, edge, edgetype);
			}
		}

		private void RemoveEdgeDirectInternal(int direction, StatEdge edge, int edgetype)
		{
			IDictionary<int, List<StatEdge>> mapEdges = direction == Direction_Backward ? mapPredEdges
				 : mapSuccEdges;
			IDictionary<int, List<Statement>> mapStates = direction == Direction_Backward ? 
				mapPredStates : mapSuccStates;
			List<StatEdge> lst = mapEdges.GetOrNull(edgetype);
			if (lst != null)
			{
				int index = lst.IndexOf(edge);
				if (index >= 0)
				{
					lst.RemoveAtReturningValue(index);
					mapStates.GetOrNull(edgetype).RemoveAtReturningValue(index);
				}
			}
		}

		private void RemoveEdgeInternal(int direction, StatEdge edge)
		{
			int type = edge.GetType();
			int[] arrtypes;
			if (type == StatEdge.Type_Exception)
			{
				arrtypes = new int[] { Statedge_All, StatEdge.Type_Exception };
			}
			else
			{
				arrtypes = new int[] { Statedge_All, Statedge_Direct_All, type };
			}
			foreach (int edgetype in arrtypes)
			{
				RemoveEdgeDirectInternal(direction, edge, edgetype);
			}
		}

		public virtual void AddPredecessor(StatEdge edge)
		{
			AddEdgeInternal(Direction_Backward, edge);
		}

		public virtual void RemovePredecessor(StatEdge edge)
		{
			if (edge == null)
			{
				// FIXME: redundant?
				return;
			}
			RemoveEdgeInternal(Direction_Backward, edge);
		}

		public virtual void AddSuccessor(StatEdge edge)
		{
			AddEdgeInternal(Direction_Forward, edge);
			if (edge.closure != null)
			{
				edge.closure.GetLabelEdges().Add(edge);
			}
			edge.GetDestination().AddPredecessor(edge);
		}

		public virtual void RemoveSuccessor(StatEdge edge)
		{
			if (edge == null)
			{
				return;
			}
			RemoveEdgeInternal(Direction_Forward, edge);
			if (edge.closure != null)
			{
				edge.closure.GetLabelEdges().Remove(edge);
			}
			if (edge.GetDestination() != null)
			{
				// TODO: redundant?
				edge.GetDestination().RemovePredecessor(edge);
			}
		}

		// TODO: make obsolete and remove
		public virtual void RemoveAllSuccessors(Statement stat)
		{
			if (stat == null)
			{
				return;
			}
			foreach (StatEdge edge in GetAllSuccessorEdges())
			{
				if (edge.GetDestination() == stat)
				{
					RemoveSuccessor(edge);
				}
			}
		}

		public virtual HashSet<Statement> BuildContinueSet()
		{
			continueSet.Clear();
			foreach (Statement st in stats)
			{
				Sharpen.Collections.AddAll(continueSet, st.BuildContinueSet());
				if (st != first)
				{
					continueSet.Remove(st.GetBasichead());
				}
			}
			foreach (StatEdge edge in GetEdges(StatEdge.Type_Continue, Direction_Forward))
			{
				continueSet.Add(edge.GetDestination().GetBasichead());
			}
			if (type == Type_Do)
			{
				continueSet.Remove(first.GetBasichead());
			}
			return continueSet;
		}

		public virtual void BuildMonitorFlags()
		{
			foreach (Statement st in stats)
			{
				st.BuildMonitorFlags();
			}
			switch (type)
			{
				case Type_Basicblock:
				{
					BasicBlockStatement bblock = (BasicBlockStatement)this;
					InstructionSequence seq = bblock.GetBlock().GetSeq();
					if (seq != null && seq.Length() > 0)
					{
						for (int i = 0; i < seq.Length(); i++)
						{
							if (seq.GetInstr(i).opcode == ICodeConstants.opc_monitorexit)
							{
								containsMonitorExit = true;
								break;
							}
						}
						isMonitorEnter__ = (seq.GetLastInstr().opcode == ICodeConstants.opc_monitorenter);
					}
					break;
				}

				case Type_Sequence:
				case Type_If:
				{
					containsMonitorExit = false;
					foreach (Statement st in stats)
					{
						containsMonitorExit |= st.IsContainsMonitorExit();
					}
					break;
				}

				case Type_Syncronized:
				case Type_Root:
				case Type_General:
				{
					break;
				}

				default:
				{
					containsMonitorExit = false;
					foreach (Statement st in stats)
					{
						containsMonitorExit |= st.IsContainsMonitorExit();
					}
					break;
				}
			}
		}

		public virtual List<Statement> GetReversePostOrderList()
		{
			return GetReversePostOrderList(first);
		}

		public virtual List<Statement> GetReversePostOrderList(Statement stat)
		{
			List<Statement> res = new List<Statement>();
			AddToReversePostOrderListIterative(stat, res);
			return res;
		}

		public virtual List<Statement> GetPostReversePostOrderList()
		{
			return GetPostReversePostOrderList(null);
		}

		public virtual List<Statement> GetPostReversePostOrderList(List<Statement> lstexits
			)
		{
			List<Statement> res = new List<Statement>();
			if (lstexits == null)
			{
				StrongConnectivityHelper schelper = new StrongConnectivityHelper(this);
				lstexits = StrongConnectivityHelper.GetExitReps(schelper.GetComponents());
			}
			HashSet<Statement> setVisited = new HashSet<Statement>();
			foreach (Statement exit in lstexits)
			{
				AddToPostReversePostOrderList(exit, res, setVisited);
			}
			if (res.Count != stats.Count)
			{
				throw new Exception("computing post reverse post order failed!");
			}
			return res;
		}

		public virtual bool ContainsStatement(Statement stat)
		{
			return this == stat || ContainsStatementStrict(stat);
		}

		public virtual bool ContainsStatementStrict(Statement stat)
		{
			if (stats.Contains(stat))
			{
				return true;
			}
			foreach (Statement st in stats)
			{
				if (st.ContainsStatementStrict(stat))
				{
					return true;
				}
			}
			return false;
		}

		public virtual TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			throw new Exception("not implemented");
		}

		// TODO: make obsolete and remove
		public virtual List<object> GetSequentialObjects()
		{
			return new List<object>(stats);
		}

		public virtual void InitExprents()
		{
		}

		// do nothing
		public virtual void ReplaceExprent(Exprent oldexpr, Exprent newexpr)
		{
		}

		// do nothing
		public virtual Statement GetSimpleCopy()
		{
			throw new Exception("not implemented");
		}

		public virtual void InitSimpleCopy()
		{
			if (!(stats.Count == 0))
			{
				first = stats[0];
			}
		}

		public virtual void ReplaceStatement(Statement oldstat, Statement newstat)
		{
			foreach (StatEdge edge in oldstat.GetAllPredecessorEdges())
			{
				oldstat.RemovePredecessor(edge);
				edge.GetSource().ChangeEdgeNode(Direction_Forward, edge, newstat);
				newstat.AddPredecessor(edge);
			}
			foreach (StatEdge edge in oldstat.GetAllSuccessorEdges())
			{
				oldstat.RemoveSuccessor(edge);
				edge.SetSource(newstat);
				newstat.AddSuccessor(edge);
			}
			int statindex = stats.GetIndexByKey(oldstat.id);
			stats.RemoveWithKey(oldstat.id);
			stats.AddWithKeyAndIndex(statindex, newstat, newstat.id);
			newstat.SetParent(this);
			newstat.post = oldstat.post;
			if (first == oldstat)
			{
				first = newstat;
			}
			List<StatEdge> lst = new List<StatEdge>(oldstat.GetLabelEdges());
			for (int i = lst.Count - 1; i >= 0; i--)
			{
				StatEdge edge = lst[i];
				if (edge.GetSource() != newstat)
				{
					newstat.AddLabeledEdge(edge);
				}
				else if (this == edge.GetDestination() || this.ContainsStatementStrict(edge.GetDestination
					()))
				{
					edge.closure = null;
				}
				else
				{
					this.AddLabeledEdge(edge);
				}
			}
			oldstat.GetLabelEdges().Clear();
		}

		// *****************************************************************************
		// private methods
		// *****************************************************************************
		private static void AddToReversePostOrderListIterative<_T0>(Statement root, IList
			<_T0> lst)
		{
			LinkedList<Statement> stackNode = new LinkedList<Statement>();
			LinkedList<int> stackIndex = new LinkedList<int>();
			HashSet<Statement> setVisited = new HashSet<Statement>();
			stackNode.Add(root);
			stackIndex.Add(0);
			while (!(stackNode.Count == 0))
			{
				Statement node = stackNode.GetLast();
				int index = Sharpen.Collections.RemoveLast(stackIndex);
				setVisited.Add(node);
				List<StatEdge> lstEdges = node.GetAllSuccessorEdges();
				for (; index < lstEdges.Count; index++)
				{
					StatEdge edge = lstEdges[index];
					Statement succ = edge.GetDestination();
					if (!setVisited.Contains(succ) && (edge.GetType() == StatEdge.Type_Regular || edge
						.GetType() == StatEdge.Type_Exception))
					{
						// TODO: edge filter?
						stackIndex.Add(index + 1);
						stackNode.Add(succ);
						stackIndex.Add(0);
						break;
					}
				}
				if (index == lstEdges.Count)
				{
					lst.Add(0, node);
					Sharpen.Collections.RemoveLast(stackNode);
				}
			}
		}

		private static void AddToPostReversePostOrderList<_T0, _T0>(Statement stat, IList
			<_T0> lst, HashSet<_T0> setVisited)
		{
			if (setVisited.Contains(stat))
			{
				// because of not considered exception edges, s. isExitComponent. Should be rewritten, if possible.
				return;
			}
			setVisited.Add(stat);
			foreach (StatEdge prededge in stat.GetEdges(StatEdge.Type_Regular | StatEdge.Type_Exception
				, Direction_Backward))
			{
				Statement pred = prededge.GetSource();
				if (!setVisited.Contains(pred))
				{
					AddToPostReversePostOrderList(pred, lst, setVisited);
				}
			}
			lst.Add(0, stat);
		}

		// *****************************************************************************
		// getter and setter methods
		// *****************************************************************************
		public virtual void ChangeEdgeNode(int direction, StatEdge edge, Statement value)
		{
			IDictionary<int, List<StatEdge>> mapEdges = direction == Direction_Backward ? mapPredEdges
				 : mapSuccEdges;
			IDictionary<int, List<Statement>> mapStates = direction == Direction_Backward ? 
				mapPredStates : mapSuccStates;
			int type = edge.GetType();
			int[] arrtypes;
			if (type == StatEdge.Type_Exception)
			{
				arrtypes = new int[] { Statedge_All, StatEdge.Type_Exception };
			}
			else
			{
				arrtypes = new int[] { Statedge_All, Statedge_Direct_All, type };
			}
			foreach (int edgetype in arrtypes)
			{
				List<StatEdge> lst = mapEdges.GetOrNull(edgetype);
				if (lst != null)
				{
					int index = lst.IndexOf(edge);
					if (index >= 0)
					{
						mapStates.GetOrNull(edgetype)[index] = value;
					}
				}
			}
			if (direction == Direction_Backward)
			{
				edge.SetSource(value);
			}
			else
			{
				edge.SetDestination(value);
			}
		}

		public virtual void ChangeEdgeType(int direction, StatEdge edge, int newtype)
		{
			int oldtype = edge.GetType();
			if (oldtype == newtype)
			{
				return;
			}
			if (oldtype == StatEdge.Type_Exception || newtype == StatEdge.Type_Exception)
			{
				throw new Exception("Invalid edge type!");
			}
			RemoveEdgeDirectInternal(direction, edge, oldtype);
			AddEdgeDirectInternal(direction, edge, newtype);
			if (direction == Direction_Forward)
			{
				edge.GetDestination().ChangeEdgeType(Direction_Backward, edge, newtype);
			}
			edge.SetType(newtype);
		}

		private List<StatEdge> GetEdges(int type, int direction)
		{
			IDictionary<int, List<StatEdge>> map = direction == Direction_Backward ? mapPredEdges
				 : mapSuccEdges;
			List<StatEdge> res;
			if ((type & (type - 1)) == 0)
			{
				res = map.GetOrNull(type);
				res = res == null ? new List<StatEdge>() : new List<StatEdge>(res);
			}
			else
			{
				res = new List<StatEdge>();
				foreach (int edgetype in StatEdge.Types)
				{
					if ((type & edgetype) != 0)
					{
						List<StatEdge> lst = map.GetOrNull(edgetype);
						if (lst != null)
						{
							Sharpen.Collections.AddAll(res, lst);
						}
					}
				}
			}
			return res;
		}

		public virtual List<Statement> GetNeighbours(int type, int direction)
		{
			IDictionary<int, List<Statement>> map = direction == Direction_Backward ? mapPredStates
				 : mapSuccStates;
			List<Statement> res;
			if ((type & (type - 1)) == 0)
			{
				res = map.GetOrNull(type);
				res = res == null ? new List<Statement>() : new List<Statement>(res);
			}
			else
			{
				res = new List<Statement>();
				foreach (int edgetype in StatEdge.Types)
				{
					if ((type & edgetype) != 0)
					{
						List<Statement> lst = map.GetOrNull(edgetype);
						if (lst != null)
						{
							Sharpen.Collections.AddAll(res, lst);
						}
					}
				}
			}
			return res;
		}

		public virtual HashSet<Statement> GetNeighboursSet(int type, int direction)
		{
			return new HashSet<Statement>(GetNeighbours(type, direction));
		}

		public virtual List<StatEdge> GetSuccessorEdges(int type)
		{
			return GetEdges(type, Direction_Forward);
		}

		public virtual List<StatEdge> GetPredecessorEdges(int type)
		{
			return GetEdges(type, Direction_Backward);
		}

		public virtual List<StatEdge> GetAllSuccessorEdges()
		{
			return GetEdges(Statedge_All, Direction_Forward);
		}

		public virtual List<StatEdge> GetAllPredecessorEdges()
		{
			return GetEdges(Statedge_All, Direction_Backward);
		}

		public virtual Statement GetFirst()
		{
			return first;
		}

		public virtual void SetFirst(Statement first)
		{
			this.first = first;
		}

		public virtual Statement GetPost()
		{
			return post;
		}

		public virtual VBStyleCollection<Statement, int> GetStats()
		{
			return stats;
		}

		public virtual int GetLastBasicType()
		{
			return lastBasicType;
		}

		public virtual HashSet<Statement> GetContinueSet()
		{
			return continueSet;
		}

		public virtual bool IsContainsMonitorExit()
		{
			return containsMonitorExit;
		}

		public virtual bool IsMonitorEnter()
		{
			return isMonitorEnter__;
		}

		public virtual BasicBlockStatement GetBasichead()
		{
			if (type == Type_Basicblock)
			{
				return (BasicBlockStatement)this;
			}
			else
			{
				return first.GetBasichead();
			}
		}

		public virtual bool IsLabeled()
		{
			foreach (StatEdge edge in labelEdges)
			{
				if (edge.labeled && edge.@explicit)
				{
					// FIXME: consistent setting
					return true;
				}
			}
			return false;
		}

		public virtual bool HasBasicSuccEdge()
		{
			// FIXME: default switch
			return type == Type_Basicblock || (type == Type_If && ((IfStatement)this).iftype 
				== IfStatement.Iftype_If) || (type == Type_Do && ((DoStatement)this).GetLooptype
				() != DoStatement.Loop_Do);
		}

		public virtual Statement GetParent()
		{
			return parent;
		}

		public virtual void SetParent(Statement parent)
		{
			this.parent = parent;
		}

		public virtual HashSet<StatEdge> GetLabelEdges()
		{
			// FIXME: why HashSet?
			return labelEdges;
		}

		public virtual List<Exprent> GetVarDefinitions()
		{
			return varDefinitions;
		}

		public virtual List<Exprent> GetExprents()
		{
			return exprents;
		}

		public virtual void SetExprents(List<Exprent> exprents)
		{
			this.exprents = exprents;
		}

		public virtual bool IsCopied()
		{
			return copied;
		}

		public virtual void SetCopied(bool copied)
		{
			this.copied = copied;
		}

		// helper methods
		public override string ToString()
		{
			return id.ToString();
		}

		// *****************************************************************************
		// IMatchable implementation
		// *****************************************************************************
		public override IIMatchable FindObject(MatchNode matchNode, int index)
		{
			int node_type = matchNode.GetType();
			if (node_type == MatchNode.Matchnode_Statement && !(this.stats.Count == 0))
			{
				string position = (string)matchNode.GetRuleValue(IMatchable.MatchProperties.Statement_Position
					);
				if (position != null)
				{
					if (position.Matches("-?\\d+"))
					{
						return this.stats[(this.stats.Count + System.Convert.ToInt32(position)) % this.stats
							.Count];
					}
				}
				else if (index < this.stats.Count)
				{
					// care for negative positions
					// use 'index' parameter
					return this.stats[index];
				}
			}
			else if (node_type == MatchNode.Matchnode_Exprent && this.exprents != null && !(this
				.exprents.Count == 0))
			{
				string position = (string)matchNode.GetRuleValue(IMatchable.MatchProperties.Exprent_Position
					);
				if (position != null)
				{
					if (position.Matches("-?\\d+"))
					{
						return this.exprents[(this.exprents.Count + System.Convert.ToInt32(position)) % this
							.exprents.Count];
					}
				}
				else if (index < this.exprents.Count)
				{
					// care for negative positions
					// use 'index' parameter
					return this.exprents[index];
				}
			}
			return null;
		}

		public override bool Match(MatchNode matchNode, MatchEngine engine)
		{
			if (matchNode.GetType() != MatchNode.Matchnode_Statement)
			{
				return false;
			}
			foreach (KeyValuePair<IMatchable.MatchProperties, MatchNode.RuleValue> rule in matchNode
				.GetRules())
			{
				switch (rule.Key.ordinal())
				{
					case 0:
					{
						if (this.type != (int)rule.Value.value)
						{
							return false;
						}
						break;
					}

					case 2:
					{
						if (this.stats.Count != (int)rule.Value.value)
						{
							return false;
						}
						break;
					}

					case 3:
					{
						int exprsize = (int)rule.Value.value;
						if (exprsize == -1)
						{
							if (this.exprents != null)
							{
								return false;
							}
						}
						else if (this.exprents == null || this.exprents.Count != exprsize)
						{
							return false;
						}
						break;
					}

					case 1:
					{
						if (!engine.CheckAndSetVariableValue((string)rule.Value.value, this))
						{
							return false;
						}
						break;
					}
				}
			}
			return true;
		}

		public Statement()
		{
			{
				id = DecompilerContext.GetCounterContainer().GetCounterAndIncrement(CounterContainer
					.Statement_Counter);
			}
		}
	}
}
