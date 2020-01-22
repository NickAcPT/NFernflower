// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Struct.Consts
{
	public class PrimitiveConstant : PooledConstant
	{
		public int index;

		public object value;

		public bool isArray;

		public PrimitiveConstant(int type, object value)
			: base(type)
		{
			this.value = value;
			InitConstant();
		}

		public PrimitiveConstant(int type, int index)
			: base(type)
		{
			this.index = index;
		}

		private void InitConstant()
		{
			if (type == CONSTANT_Class)
			{
				string className = GetString();
				isArray = (className.Length > 0 && className[0] == '[');
			}
		}

		// empty string for a class name seems to be possible in some android files
		public virtual string GetString()
		{
			return (string)value;
		}

		public override void ResolveConstant(ConstantPool pool)
		{
			if (type == CONSTANT_Class || type == CONSTANT_String || type == CONSTANT_MethodType)
			{
				value = pool.GetPrimitiveConstant(index).GetString();
				InitConstant();
			}
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is PrimitiveConstant))
			{
				return false;
			}
			PrimitiveConstant cn = (PrimitiveConstant)o;
			return this.type == cn.type && this.isArray == cn.isArray && this.value.Equals(cn
				.value);
		}
	}
}
