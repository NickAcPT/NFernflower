// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructBootstrapMethodsAttribute : StructGeneralAttribute
	{
		private readonly List<LinkConstant> methodRefs = new List<LinkConstant>();

		private readonly List<List<PooledConstant>> methodArguments = new List<List<PooledConstant
			>>();

		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			int method_number = data.ReadUnsignedShort();
			for (int i = 0; i < method_number; ++i)
			{
				int bootstrap_method_ref = data.ReadUnsignedShort();
				int num_bootstrap_arguments = data.ReadUnsignedShort();
				List<PooledConstant> list_arguments = new List<PooledConstant>();
				for (int j = 0; j < num_bootstrap_arguments; ++j)
				{
					int bootstrap_argument_ref = data.ReadUnsignedShort();
					list_arguments.Add(pool.GetConstant(bootstrap_argument_ref));
				}
				methodRefs.Add(pool.GetLinkConstant(bootstrap_method_ref));
				methodArguments.Add(list_arguments);
			}
		}

		public virtual int GetMethodsNumber()
		{
			return methodRefs.Count;
		}

		public virtual LinkConstant GetMethodReference(int index)
		{
			return methodRefs[index];
		}

		public virtual List<PooledConstant> GetMethodArguments(int index)
		{
			return methodArguments[index];
		}
	}
}
