// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Gen.Generics
{
	public class GenericMethodDescriptor
	{
		public readonly List<string> typeParameters;

		public readonly List<List<GenericType>> typeParameterBounds;

		public readonly List<GenericType> parameterTypes;

		public readonly GenericType returnType;

		public readonly List<GenericType> exceptionTypes;

		public GenericMethodDescriptor(List<string> typeParameters, List<List<GenericType
			>> typeParameterBounds, List<GenericType> parameterTypes, GenericType returnType
			, List<GenericType> exceptionTypes)
		{
			this.typeParameters = Substitute(typeParameters);
			this.typeParameterBounds = Substitute(typeParameterBounds);
			this.parameterTypes = Substitute(parameterTypes);
			this.returnType = returnType;
			this.exceptionTypes = Substitute(exceptionTypes);
		}

		private static List<T> Substitute<T>(List<T> list)
		{
			return (list.Count == 0) ? new System.Collections.Generic.List<T>() : list;
		}
	}
}
