/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System.Collections.Generic;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class AssertExprent : Exprent
	{
		private readonly List<Exprent> parameters;

		public AssertExprent(List<Exprent> parameters)
			: base(Exprent_Assert)
		{
			this.parameters = parameters;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer buffer = new TextBuffer();
			buffer.Append("assert ");
			tracer.AddMapping(bytecode);
			if (parameters[0] == null)
			{
				buffer.Append("false");
			}
			else
			{
				buffer.Append(parameters[0].ToJava(indent, tracer));
			}
			if (parameters.Count > 1)
			{
				buffer.Append(" : ");
				buffer.Append(parameters[1].ToJava(indent, tracer));
			}
			return buffer;
		}
	}
}
