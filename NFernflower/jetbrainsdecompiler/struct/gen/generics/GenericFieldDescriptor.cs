// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Struct.Gen.Generics
{
	public class GenericFieldDescriptor
	{
		public readonly GenericType type;

		public GenericFieldDescriptor(GenericType type)
		{
			this.type = type;
		}
	}
}
