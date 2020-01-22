// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Vars
{
	public class VarVersionPair
	{
		public readonly int var;

		public readonly int version;

		private int hashCode__ = -1;

		public VarVersionPair(int var, int version)
		{
			this.var = var;
			this.version = version;
		}

		public VarVersionPair(int var, int version)
		{
			this.var = var;
			this.version = version;
		}

		public VarVersionPair(VarExprent var)
		{
			this.var = var.GetIndex();
			this.version = var.GetVersion();
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is VarVersionPair))
			{
				return false;
			}
			VarVersionPair paar = (VarVersionPair)o;
			return var == paar.var && version == paar.version;
		}

		public override int GetHashCode()
		{
			if (hashCode__ == -1)
			{
				hashCode__ = this.var * 3 + this.version;
			}
			return hashCode__;
		}

		public override string ToString()
		{
			return "(" + var + "," + version + ")";
		}
	}
}
