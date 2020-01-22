// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Main.Extern
{
	public abstract class IIIdentifierRenamer
	{
		[System.Serializable]
		public sealed class Type : Sharpen.EnumBase
		{
			public static readonly IIdentifierRenamer.Type Element_Class = new IIdentifierRenamer.Type
				(0, "ELEMENT_CLASS");

			public static readonly IIdentifierRenamer.Type Element_Field = new IIdentifierRenamer.Type
				(1, "ELEMENT_FIELD");

			public static readonly IIdentifierRenamer.Type Element_Method = new IIdentifierRenamer.Type
				(2, "ELEMENT_METHOD");

			private Type(int ordinal, string name)
				: base(ordinal, name)
			{
			}

			public static Type[] Values()
			{
				return new Type[] { Element_Class, Element_Field, Element_Method };
			}

			static Type()
			{
				RegisterValues<Type>(Values());
			}
		}

		public abstract bool ToBeRenamed(IIdentifierRenamer.Type elementType, string className
			, string element, string descriptor);

		public abstract string GetNextClassName(string fullName, string shortName);

		public abstract string GetNextFieldName(string className, string field, string descriptor
			);

		public abstract string GetNextMethodName(string className, string method, string 
			descriptor);
	}

	public static class IIdentifierRenamerConstants
	{
	}
}
