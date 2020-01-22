// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Deobfuscator
{
	public class IrreducibleCFGDeobfuscator
	{
		public static bool IsStatementIrreducible(Statement statement)
		{
			Dictionary<int, _T989645285> mapNodes = new Dictionary<int, _T989645285>();
			// checking exceptions and creating nodes
			foreach (Statement stat in statement.GetStats())
			{
				if (!(stat.GetSuccessorEdges(StatEdge.Type_Exception).Count == 0))
				{
					return false;
				}
				Sharpen.Collections.Put(mapNodes, stat.id, new _T989645285(this, stat.id));
			}
			// connecting nodes
			foreach (Statement stat in statement.GetStats())
			{
				_T989645285 node = mapNodes.GetOrNull(stat.id);
				foreach (Statement succ in stat.GetNeighbours(StatEdge.Type_Regular, Statement.Direction_Forward
					))
				{
					_T989645285 nodeSucc = mapNodes.GetOrNull(succ.id);
					node.succs.Add(nodeSucc);
					nodeSucc.preds.Add(node);
				}
			}
			// transforming and reducing the graph
			while (true)
			{
				int ttype = 0;
				_T989645285 node = null;
				foreach (_T989645285 nd in mapNodes.Values)
				{
					if (nd.succs.Contains(nd))
					{
						// T1
						ttype = 1;
					}
					else if (nd.preds.Count == 1)
					{
						// T2
						ttype = 2;
					}
					if (ttype != 0)
					{
						node = nd;
						break;
					}
				}
				if (node != null)
				{
					if (ttype == 1)
					{
						node.succs.Remove(node);
						node.preds.Remove(node);
					}
					else
					{
						_T989645285 pred = node.preds.GetEnumerator().Current;
						Sharpen.Collections.AddAll(pred.succs, node.succs);
						pred.succs.Remove(node);
						foreach (_T989645285 succ in node.succs)
						{
							succ.preds.Remove(node);
							succ.preds.Add(pred);
						}
						Sharpen.Collections.Remove(mapNodes, node.id);
					}
				}
				else
				{
					// no transformation applicable
					return mapNodes.Count > 1;
				}
			}
		}

		internal class _T989645285
		{
			public readonly int id;

			public readonly HashSet<_T989645285> preds = new HashSet<_T989645285>();

			public readonly HashSet<_T989645285> succs = new HashSet<_T989645285>();

			internal _T989645285(IrreducibleCFGDeobfuscator _enclosing, int id)
			{
				this._enclosing = _enclosing;
				this.id = id;
			}

			private readonly IrreducibleCFGDeobfuscator _enclosing;
		}

		// reducible iff one node remains
		private static Statement GetCandidateForSplitting(Statement statement)
		{
			Statement candidateForSplitting = null;
			int sizeCandidateForSplitting = int.MaxValue;
			int succsCandidateForSplitting = int.MaxValue;
			foreach (Statement stat in statement.GetStats())
			{
				HashSet<Statement> setPreds = stat.GetNeighboursSet(StatEdge.Type_Regular, Statement
					.Direction_Backward);
				if (setPreds.Count > 1)
				{
					int succCount = stat.GetNeighboursSet(StatEdge.Type_Regular, Statement.Direction_Forward
						).Count;
					if (succCount <= succsCandidateForSplitting)
					{
						int size = GetStatementSize(stat) * (setPreds.Count - 1);
						if (succCount < succsCandidateForSplitting || size < sizeCandidateForSplitting)
						{
							candidateForSplitting = stat;
							sizeCandidateForSplitting = size;
							succsCandidateForSplitting = succCount;
						}
					}
				}
			}
			return candidateForSplitting;
		}

		public static bool SplitIrreducibleNode(Statement statement)
		{
			Statement splitnode = GetCandidateForSplitting(statement);
			if (splitnode == null)
			{
				return false;
			}
			StatEdge enteredge = splitnode.GetPredecessorEdges(StatEdge.Type_Regular).GetEnumerator
				().Current;
			// copy the smallest statement
			Statement splitcopy = CopyStatement(splitnode, null, new Dictionary<Statement, Statement
				>());
			InitCopiedStatement(splitcopy);
			// insert the copy
			splitcopy.SetParent(statement);
			statement.GetStats().AddWithKey(splitcopy, splitcopy.id);
			// switch input edges
			foreach (StatEdge prededge in splitnode.GetPredecessorEdges(Statement.Statedge_Direct_All
				))
			{
				if (prededge.GetSource() == enteredge.GetSource() || prededge.closure == enteredge
					.GetSource())
				{
					splitnode.RemovePredecessor(prededge);
					prededge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, prededge, splitcopy
						);
					splitcopy.AddPredecessor(prededge);
				}
			}
			// connect successors
			foreach (StatEdge succ in splitnode.GetSuccessorEdges(Statement.Statedge_Direct_All
				))
			{
				splitcopy.AddSuccessor(new StatEdge(succ.GetType(), splitcopy, succ.GetDestination
					(), succ.closure));
			}
			return true;
		}

		private static int GetStatementSize(Statement statement)
		{
			int res;
			if (statement.type == Statement.Type_Basicblock)
			{
				res = ((BasicBlockStatement)statement).GetBlock().GetSeq().Length();
			}
			else
			{
				res = statement.GetStats().Stream().MapToInt(IrreducibleCFGDeobfuscator).Sum();
			}
			return res;
		}

		private static Statement CopyStatement(Statement from, Statement to, Dictionary<Statement
			, Statement> mapAltToCopies)
		{
			if (to == null)
			{
				// first outer invocation
				to = from.GetSimpleCopy();
				Sharpen.Collections.Put(mapAltToCopies, from, to);
			}
			// copy statements
			foreach (Statement st in from.GetStats())
			{
				Statement stcopy = st.GetSimpleCopy();
				to.GetStats().AddWithKey(stcopy, stcopy.id);
				Sharpen.Collections.Put(mapAltToCopies, st, stcopy);
			}
			// copy edges
			for (int i = 0; i < from.GetStats().Count; i++)
			{
				Statement stold = from.GetStats()[i];
				Statement stnew = to.GetStats()[i];
				foreach (StatEdge edgeold in stold.GetSuccessorEdges(Statement.Statedge_Direct_All
					))
				{
					// type cannot be TYPE_EXCEPTION (checked in isIrreducibleTriangle)
					StatEdge edgenew = new StatEdge(edgeold.GetType(), stnew, mapAltToCopies.ContainsKey
						(edgeold.GetDestination()) ? mapAltToCopies.GetOrNull(edgeold.GetDestination()) : 
						edgeold.GetDestination(), mapAltToCopies.ContainsKey(edgeold.closure) ? mapAltToCopies
						.GetOrNull(edgeold.closure) : edgeold.closure);
					stnew.AddSuccessor(edgenew);
				}
			}
			// recurse statements
			for (int i = 0; i < from.GetStats().Count; i++)
			{
				Statement stold = from.GetStats()[i];
				Statement stnew = to.GetStats()[i];
				CopyStatement(stold, stnew, mapAltToCopies);
			}
			return to;
		}

		private static void InitCopiedStatement(Statement statement)
		{
			statement.InitSimpleCopy();
			statement.SetCopied(true);
			foreach (Statement st in statement.GetStats())
			{
				st.SetParent(statement);
				InitCopiedStatement(st);
			}
		}
	}
}
