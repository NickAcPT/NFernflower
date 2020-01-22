// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Code;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Gen
{
	public class FieldDescriptor
	{
		public static readonly FieldDescriptor Integer_Descriptor = ParseDescriptor("Ljava/lang/Integer;"
			);

		public static readonly FieldDescriptor Long_Descriptor = ParseDescriptor("Ljava/lang/Long;"
			);

		public static readonly FieldDescriptor Float_Descriptor = ParseDescriptor("Ljava/lang/Float;"
			);

		public static readonly FieldDescriptor Double_Descriptor = ParseDescriptor("Ljava/lang/Double;"
			);

		public readonly VarType type;

		public readonly string descriptorString;

		private FieldDescriptor(string descriptor)
		{
			type = new VarType(descriptor);
			descriptorString = descriptor;
		}

		public static FieldDescriptor ParseDescriptor(string descriptor)
		{
			return new FieldDescriptor(descriptor);
		}

		public virtual string BuildNewDescriptor(INewClassNameBuilder builder)
		{
			if (type.type == ICodeConstants.Type_Object)
			{
				string newClassName = builder.BuildNewClassname(type.value);
				if (newClassName != null)
				{
					return new VarType(type.type, type.arrayDim, newClassName).ToString();
				}
			}
			return null;
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is FieldDescriptor))
			{
				return false;
			}
			FieldDescriptor fd = (FieldDescriptor)o;
			return type.Equals(fd.type);
		}

		public override int GetHashCode()
		{
			return type.GetHashCode();
		}
	}
}
