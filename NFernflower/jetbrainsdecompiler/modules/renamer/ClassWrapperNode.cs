// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Struct;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Renamer
{
	public class ClassWrapperNode
	{
		private readonly StructClass classStruct;

		private readonly List<ClassWrapperNode> subclasses = new List<ClassWrapperNode>(
			);

		public ClassWrapperNode(StructClass cl)
		{
			this.classStruct = cl;
		}

		public virtual void AddSubclass(ClassWrapperNode node)
		{
			subclasses.Add(node);
		}

		public virtual StructClass GetClassStruct()
		{
			return classStruct;
		}

		public virtual List<ClassWrapperNode> GetSubclasses()
		{
			return subclasses;
		}
	}
}
