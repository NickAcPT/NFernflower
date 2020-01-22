// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Sharpen;

namespace JetBrainsDecompiler.Main.Collectors
{
	public class VarNamesCollector
	{
		private readonly HashSet<string> usedNames = new HashSet<string>();

		public VarNamesCollector()
		{
		}

		public VarNamesCollector(ICollection<string> setNames)
		{
			Sharpen.Collections.AddAll(usedNames, setNames);
		}

		public virtual void AddName(string value)
		{
			usedNames.Add(value);
		}

		public virtual string GetFreeName(int index)
		{
			return GetFreeName("var" + index);
		}

		public virtual string GetFreeName(string proposition)
		{
			while (usedNames.Contains(proposition))
			{
				proposition += "x";
			}
			usedNames.Add(proposition);
			return proposition;
		}
	}
}
