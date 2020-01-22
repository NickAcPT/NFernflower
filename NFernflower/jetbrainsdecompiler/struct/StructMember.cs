// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct
{
	public class StructMember
	{
		protected internal int accessFlags;

		protected internal IDictionary<string, StructGeneralAttribute> attributes;

		public virtual int GetAccessFlags()
		{
			return accessFlags;
		}

		public virtual T GetAttribute<T>(StructGeneralAttribute.Key<T> attribute)
			where T : StructGeneralAttribute
		{
			//noinspection unchecked
			return (T)attributes.GetOrNull(attribute.GetName());
		}

		public virtual bool HasAttribute<_T0>(StructGeneralAttribute.Key<_T0> attribute)
			where _T0 : StructGeneralAttribute
		{
			return attributes.ContainsKey(attribute.GetName());
		}

		public virtual bool HasModifier(int modifier)
		{
			return (accessFlags & modifier) == modifier;
		}

		public virtual bool IsSynthetic()
		{
			return HasModifier(ICodeConstants.Acc_Synthetic) || HasAttribute(StructGeneralAttribute
				.Attribute_Synthetic);
		}

		/// <exception cref="System.IO.IOException"/>
		protected internal virtual IDictionary<string, StructGeneralAttribute> ReadAttributes
			(DataInputFullStream @in, ConstantPool pool)
		{
			int length = @in.ReadUnsignedShort();
			IDictionary<string, StructGeneralAttribute> attributes = new Dictionary<string, StructGeneralAttribute
				>(length);
			for (int i = 0; i < length; i++)
			{
				int nameIndex = @in.ReadUnsignedShort();
				string name = pool.GetPrimitiveConstant(nameIndex).GetString();
				StructGeneralAttribute attribute = ReadAttribute(@in, pool, name);
				if (attribute != null)
				{
					if (StructGeneralAttribute.Attribute_Local_Variable_Table.GetName().Equals(name) 
						&& attributes.ContainsKey(name))
					{
						// merge all variable tables
						StructLocalVariableTableAttribute table = (StructLocalVariableTableAttribute)attributes
							.GetOrNull(name);
						table.Add((StructLocalVariableTableAttribute)attribute);
					}
					else if (StructGeneralAttribute.Attribute_Local_Variable_Type_Table.GetName().Equals
						(name) && attributes.ContainsKey(name))
					{
						// merge all variable tables
						StructLocalVariableTypeTableAttribute table = (StructLocalVariableTypeTableAttribute
							)attributes.GetOrNull(name);
						table.Add((StructLocalVariableTypeTableAttribute)attribute);
					}
					else
					{
						Sharpen.Collections.Put(attributes, attribute.GetName(), attribute);
					}
				}
			}
			return attributes;
		}

		/// <exception cref="System.IO.IOException"/>
		protected internal virtual StructGeneralAttribute ReadAttribute(DataInputFullStream
			 @in, ConstantPool pool, string name)
		{
			StructGeneralAttribute attribute = StructGeneralAttribute.CreateAttribute(name);
			int length = @in.ReadInt();
			if (attribute == null)
			{
				@in.Discard(length);
			}
			else
			{
				attribute.InitContent(@in, pool);
			}
			return attribute;
		}
	}
}
