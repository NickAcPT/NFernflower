// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler.Sforms;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using Sharpen;

namespace JetBrainsDecompiler.Main.Rels
{
	public class MethodWrapper
	{
		public readonly RootStatement root;

		public readonly VarProcessor varproc;

		public readonly StructMethod methodStruct;

		public readonly CounterContainer counter;

		public readonly HashSet<string> setOuterVarNames = new HashSet<string>();

		public DirectGraph graph;

		public List<VarVersionPair> synthParameters;

		public bool decompiledWithErrors;

		public MethodWrapper(RootStatement root, VarProcessor varproc, StructMethod methodStruct
			, CounterContainer counter)
		{
			this.root = root;
			this.varproc = varproc;
			this.methodStruct = methodStruct;
			this.counter = counter;
		}

		public virtual DirectGraph GetOrBuildGraph()
		{
			if (graph == null && root != null)
			{
				graph = new FlattenStatementsHelper().BuildDirectGraph(root);
			}
			return graph;
		}

		public override string ToString()
		{
			return methodStruct.GetName();
		}
	}
}
