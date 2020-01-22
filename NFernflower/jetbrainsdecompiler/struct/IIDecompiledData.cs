// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Struct
{
	public interface IIDecompiledData
	{
		string GetClassEntryName(StructClass cl, string entryname);

		string GetClassContent(StructClass cl);
	}
}
