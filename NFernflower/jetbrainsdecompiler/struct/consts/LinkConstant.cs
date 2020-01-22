// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Consts
{
	public class LinkConstant : PooledConstant
	{
		public int index1;

		public int index2;

		public string classname;

		public string elementname;

		public string descriptor;

		public LinkConstant(int type, string classname, string elementname, string descriptor
			)
			: base(type)
		{
			this.classname = classname;
			this.elementname = elementname;
			this.descriptor = descriptor;
			InitConstant();
		}

		public LinkConstant(int type, int index1, int index2)
			: base(type)
		{
			this.index1 = index1;
			this.index2 = index2;
		}

		private void InitConstant()
		{
			if (type == CONSTANT_Methodref || type == CONSTANT_InterfaceMethodref || type == 
				CONSTANT_InvokeDynamic || type == CONSTANT_MethodHandle)
			{
				int parenth = descriptor.IndexOf(')');
				if (descriptor.Length < 2 || parenth < 0 || descriptor[0] != '(')
				{
					throw new ArgumentException("Invalid descriptor: " + descriptor);
				}
			}
		}

		public override void ResolveConstant(ConstantPool pool)
		{
			if (type == CONSTANT_NameAndType)
			{
				elementname = pool.GetPrimitiveConstant(index1).GetString();
				descriptor = pool.GetPrimitiveConstant(index2).GetString();
			}
			else if (type == CONSTANT_MethodHandle)
			{
				LinkConstant ref_info = pool.GetLinkConstant(index2);
				classname = ref_info.classname;
				elementname = ref_info.elementname;
				descriptor = ref_info.descriptor;
			}
			else
			{
				if (type != CONSTANT_InvokeDynamic)
				{
					classname = pool.GetPrimitiveConstant(index1).GetString();
				}
				LinkConstant nametype = pool.GetLinkConstant(index2);
				elementname = nametype.elementname;
				descriptor = nametype.descriptor;
			}
			InitConstant();
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is LinkConstant))
			{
				return false;
			}
			LinkConstant cn = (LinkConstant)o;
			return this.type == cn.type && this.elementname.Equals(cn.elementname) && this.descriptor
				.Equals(cn.descriptor) && (this.type != CONSTANT_NameAndType || this.classname.Equals
				(cn.classname));
		}
	}
}
