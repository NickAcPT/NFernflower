// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Struct.Attr;
using Sharpen;

namespace JetBrainsDecompiler.Main.Collectors
{
	public class BytecodeMappingTracer
	{
		public static readonly BytecodeMappingTracer Dummy = new BytecodeMappingTracer();

		private int currentSourceLine;

		private StructLineNumberTableAttribute lineNumberTable = null;

		private readonly IDictionary<int, int> mapping = new Dictionary<int, int>();

		public BytecodeMappingTracer()
		{
		}

		public BytecodeMappingTracer(int initial_source_line)
		{
			// bytecode offset, source line
			currentSourceLine = initial_source_line;
		}

		public virtual void IncrementCurrentSourceLine()
		{
			currentSourceLine++;
		}

		public virtual void IncrementCurrentSourceLine(int number_lines)
		{
			currentSourceLine += number_lines;
		}

		public virtual void AddMapping(int bytecode_offset)
		{
			mapping.PutIfAbsent(bytecode_offset, currentSourceLine);
		}

		public virtual void AddMapping(HashSet<int> bytecode_offsets)
		{
			if (bytecode_offsets != null)
			{
				foreach (int bytecode_offset in bytecode_offsets)
				{
					AddMapping(bytecode_offset);
				}
			}
		}

		public virtual void AddTracer(BytecodeMappingTracer tracer)
		{
			if (tracer != null)
			{
				foreach (KeyValuePair<int, int> entry in tracer.mapping)
				{
					mapping.PutIfAbsent(entry.Key, entry.Value);
				}
			}
		}

		public virtual IDictionary<int, int> GetMapping()
		{
			return mapping;
		}

		public virtual int GetCurrentSourceLine()
		{
			return currentSourceLine;
		}

		public virtual void SetCurrentSourceLine(int currentSourceLine)
		{
			this.currentSourceLine = currentSourceLine;
		}

		public virtual void SetLineNumberTable(StructLineNumberTableAttribute lineNumberTable
			)
		{
			this.lineNumberTable = lineNumberTable;
		}

		private readonly HashSet<int> unmappedLines = new HashSet<int>();

		public virtual HashSet<int> GetUnmappedLines()
		{
			return unmappedLines;
		}

		public virtual IDictionary<int, int> GetOriginalLinesMapping()
		{
			if (lineNumberTable == null)
			{
				return new System.Collections.Generic.Dictionary<int, int>();
			}
			IDictionary<int, int> res = new Dictionary<int, int>();
			// first match offsets from line number table
			int[] data = lineNumberTable.GetRawData();
			for (int i = 0; i < data.Length; i += 2)
			{
				int originalOffset = data[i];
				int originalLine = data[i + 1];
				int? newLine = mapping.GetOrNullable(originalOffset);
				if (newLine != null)
				{
					Sharpen.Collections.Put(res, originalLine, newLine.Value);
				}
				else
				{
					unmappedLines.Add(originalLine);
				}
			}
			// now match offsets from decompiler mapping
			foreach (KeyValuePair<int, int> entry in mapping)
			{
				int originalLine = lineNumberTable.FindLineNumber(entry.Key);
				if (originalLine > -1 && !res.ContainsKey(originalLine))
				{
					Sharpen.Collections.Put(res, originalLine, entry.Value);
					unmappedLines.Remove(originalLine);
				}
			}
			return res;
		}
	}
}
