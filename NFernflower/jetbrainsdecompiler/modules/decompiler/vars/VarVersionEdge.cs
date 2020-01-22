// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Vars
{
	public class VarVersionEdge
	{
		public const int Edge_General = 0;

		public const int Edge_Phantom = 1;

		public readonly int type;

		public readonly VarVersionNode source;

		public readonly VarVersionNode dest;

		private readonly int hashCode__;

		public VarVersionEdge(int type, VarVersionNode source, VarVersionNode dest)
		{
			// FIXME: can be removed?
			this.type = type;
			this.source = source;
			this.dest = dest;
			this.hashCode__ = source.GetHashCode() ^ dest.GetHashCode() + type;
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is VarVersionEdge))
			{
				return false;
			}
			VarVersionEdge edge = (VarVersionEdge)o;
			return type == edge.type && source == edge.source && dest == edge.dest;
		}

		public override int GetHashCode()
		{
			return hashCode__;
		}

		public override string ToString()
		{
			return source.ToString() + " ->" + type + "-> " + dest.ToString();
		}
	}
}
