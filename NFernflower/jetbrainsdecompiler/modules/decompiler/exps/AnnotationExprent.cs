/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System.Collections.Generic;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class AnnotationExprent : Exprent
	{
		public const int Annotation_Normal = 1;

		public const int Annotation_Marker = 2;

		public const int Annotation_Single_Element = 3;

		private readonly string className;

		private readonly List<string> parNames;

		private readonly List<Exprent> parValues;

		public AnnotationExprent(string className, List<string> parNames, List<Exprent>
			 parValues)
			: base(Exprent_Annotation)
		{
			this.className = className;
			this.parNames = parNames;
			this.parValues = parValues;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer buffer = new TextBuffer();
			buffer.AppendIndent(indent);
			buffer.Append('@');
			buffer.Append(DecompilerContext.GetImportCollector().GetShortName(ExprProcessor.BuildJavaClassName
				(className)));
			int type = GetAnnotationType();
			if (type != Annotation_Marker)
			{
				buffer.Append('(');
				bool oneLiner = type == Annotation_Single_Element || indent < 0;
				for (int i = 0; i < parNames.Count; i++)
				{
					if (!oneLiner)
					{
						buffer.AppendLineSeparator().AppendIndent(indent + 1);
					}
					if (type != Annotation_Single_Element)
					{
						buffer.Append(parNames[i]);
						buffer.Append(" = ");
					}
					buffer.Append(parValues[i].ToJava(0, tracer));
					if (i < parNames.Count - 1)
					{
						buffer.Append(',');
					}
				}
				if (!oneLiner)
				{
					buffer.AppendLineSeparator().AppendIndent(indent);
				}
				buffer.Append(')');
			}
			return buffer;
		}

		public virtual string GetClassName()
		{
			return className;
		}

		public virtual int GetAnnotationType()
		{
			if ((parNames.Count == 0))
			{
				return Annotation_Marker;
			}
			else if (parNames.Count == 1 && "value".Equals(parNames[0]))
			{
				return Annotation_Single_Element;
			}
			else
			{
				return Annotation_Normal;
			}
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is AnnotationExprent))
			{
				return false;
			}
			AnnotationExprent ann = (AnnotationExprent)o;
			return className.Equals(ann.className) && InterpreterUtil.EqualLists(parNames, ann
				.parNames) && InterpreterUtil.EqualLists(parValues, ann.parValues);
		}
	}
}
