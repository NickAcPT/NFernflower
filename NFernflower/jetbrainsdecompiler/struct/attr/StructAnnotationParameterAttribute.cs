// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructAnnotationParameterAttribute : StructGeneralAttribute
	{
		private List<List<AnnotationExprent>> paramAnnotations;

		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			int len = data.ReadUnsignedByte();
			if (len > 0)
			{
				paramAnnotations = new List<List<AnnotationExprent>>(len);
				for (int i = 0; i < len; i++)
				{
					List<AnnotationExprent> annotations = StructAnnotationAttribute.ParseAnnotations
						(pool, data);
					paramAnnotations.Add(annotations);
				}
			}
			else
			{
				paramAnnotations = new System.Collections.Generic.List<List<AnnotationExprent>>();
			}
		}

		public virtual List<List<AnnotationExprent>> GetParamAnnotations()
		{
			return paramAnnotations;
		}
	}
}
