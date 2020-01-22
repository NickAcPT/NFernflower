// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using Java.IO;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using Sharpen;

namespace JetBrainsDecompiler.Main.Decompiler
{
	public class BaseDecompiler
	{
		private readonly Fernflower engine;

		public BaseDecompiler(IIBytecodeProvider provider, IIResultSaver saver, IDictionary
			<string, object> options, IFernflowerLogger logger)
		{
			engine = new Fernflower(provider, saver, options, logger);
		}

		public virtual void AddSource(File source)
		{
			engine.AddSource(source);
		}

		public virtual void AddLibrary(File library)
		{
			engine.AddLibrary(library);
		}

		[System.ObsoleteAttribute(@"use AddSource(Java.IO.File) / AddLibrary(Java.IO.File) instead"
			)]
		public virtual void AddSpace(File file, bool own)
		{
			if (own)
			{
				AddSource(file);
			}
			else
			{
				AddLibrary(file);
			}
		}

		public virtual void DecompileContext()
		{
			try
			{
				engine.DecompileContext();
			}
			finally
			{
				engine.ClearContext();
			}
		}
	}
}
