// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Sforms
{
	public class DirectNode
	{
		public const int Node_Direct = 1;

		public const int Node_Tail = 2;

		public const int Node_Init = 3;

		public const int Node_Condition = 4;

		public const int Node_Increment = 5;

		public const int Node_Try = 6;

		public readonly int type;

		public readonly string id;

		public BasicBlockStatement block;

		public readonly Statement statement;

		public List<Exprent> exprents = new List<Exprent>();

		public readonly List<DirectNode> succs = new List<DirectNode>();

		public readonly List<DirectNode> preds = new List<DirectNode>();

		public DirectNode(int type, Statement statement, string id)
		{
			this.type = type;
			this.statement = statement;
			this.id = id;
		}

		public DirectNode(int type, Statement statement, BasicBlockStatement block)
		{
			this.type = type;
			this.statement = statement;
			this.id = block.id.ToString();
			this.block = block;
		}

		public override string ToString()
		{
			return id;
		}
	}
}
