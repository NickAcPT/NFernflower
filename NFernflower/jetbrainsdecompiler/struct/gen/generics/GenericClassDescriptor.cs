// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Gen.Generics
{
	public class GenericClassDescriptor
	{
		public GenericType superclass;

		public readonly List<GenericType> superinterfaces = new List<GenericType>();

		public readonly List<string> fparameters = new List<string>();

		public readonly List<List<GenericType>> fbounds = new List<List<GenericType>>(
			);
	}
}
