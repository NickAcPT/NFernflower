// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Renamer
{
	public class PoolInterceptor
	{
		private readonly Dictionary<string, string> mapOldToNewNames = new Dictionary<string
			, string>();

		private readonly Dictionary<string, string> mapNewToOldNames = new Dictionary<string
			, string>();

		public virtual void AddName(string oldName, string newName)
		{
			Sharpen.Collections.Put(mapOldToNewNames, oldName, newName);
			Sharpen.Collections.Put(mapNewToOldNames, newName, oldName);
		}

		public virtual string GetName(string oldName)
		{
			return mapOldToNewNames.GetOrNull(oldName);
		}

		public virtual string GetOldName(string newName)
		{
			return mapNewToOldNames.GetOrNull(newName);
		}
	}
}
