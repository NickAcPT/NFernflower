// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Struct.Gen;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Vars
{
	public class CheckTypesResult
	{
		private readonly List<CheckTypesResult.ExprentTypePair> lstMaxTypeExprents = new 
			List<CheckTypesResult.ExprentTypePair>();

		private readonly List<CheckTypesResult.ExprentTypePair> lstMinTypeExprents = new 
			List<CheckTypesResult.ExprentTypePair>();

		public virtual void AddMaxTypeExprent(Exprent exprent, VarType type)
		{
			lstMaxTypeExprents.Add(new CheckTypesResult.ExprentTypePair(exprent, type));
		}

		public virtual void AddMinTypeExprent(Exprent exprent, VarType type)
		{
			lstMinTypeExprents.Add(new CheckTypesResult.ExprentTypePair(exprent, type));
		}

		public virtual List<CheckTypesResult.ExprentTypePair> GetLstMaxTypeExprents()
		{
			return lstMaxTypeExprents;
		}

		public virtual List<CheckTypesResult.ExprentTypePair> GetLstMinTypeExprents()
		{
			return lstMinTypeExprents;
		}

		public class ExprentTypePair
		{
			public readonly Exprent exprent;

			public readonly VarType type;

			public ExprentTypePair(Exprent exprent, VarType type)
			{
				this.exprent = exprent;
				this.type = type;
			}
		}
	}
}
