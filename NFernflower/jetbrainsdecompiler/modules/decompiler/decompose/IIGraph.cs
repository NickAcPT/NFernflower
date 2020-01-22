// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Decompose
{
	public interface IIGraph
	{
		List<IIGraphNode> GetReversePostOrderList();

		HashSet<IIGraphNode> GetRoots();
	}
}
