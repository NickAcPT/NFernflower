// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Main.Extern
{
	public interface IIBytecodeProvider
	{
		/// <exception cref="System.IO.IOException"/>
		byte[] GetBytecode(string externalPath, string internalPath);
	}
}
