// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Decompose;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Vars
{
	public class VarVersionNode : IGraphNode
	{
		public const int Flag_Phantom_Finexit = 2;

		public readonly int var;

		public readonly int version;

		public readonly HashSet<VarVersionEdge> succs = new HashSet<VarVersionEdge>();

		public readonly HashSet<VarVersionEdge> preds = new HashSet<VarVersionEdge>();

		public int flags;

		public SFormsFastMapDirect live = new SFormsFastMapDirect();

		public VarVersionNode(int var, int version)
		{
			this.var = var;
			this.version = version;
		}

		public virtual List<IGraphNode> GetPredecessors()
		{
			List<IGraphNode> lst = new List<IGraphNode>(preds.Count);
			foreach (VarVersionEdge edge in preds)
			{
				lst.Add(edge.source);
			}
			return lst;
		}

		public virtual void RemoveSuccessor(VarVersionEdge edge)
		{
			succs.Remove(edge);
		}

		public virtual void RemovePredecessor(VarVersionEdge edge)
		{
			preds.Remove(edge);
		}

		public virtual void AddSuccessor(VarVersionEdge edge)
		{
			succs.Add(edge);
		}

		public virtual void AddPredecessor(VarVersionEdge edge)
		{
			preds.Add(edge);
		}

		public override string ToString()
		{
			return "(" + var + "_" + version + ")";
		}
	}
}
