// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Java.Util;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main.Collectors
{
	public class BytecodeSourceMapper
	{
		private int offset_total;

		private readonly IDictionary<string, IDictionary<string, IDictionary<int, int>>> 
			mapping = new LinkedHashMap<string, IDictionary<string, IDictionary<int, int>>>(
			);

		private readonly IDictionary<int, int> linesMapping = new Dictionary<int, int>();

		private readonly HashSet<int> unmappedLines = new TreeSet<int>();

		// class, method, bytecode offset, source line
		// original line to decompiled line
		public virtual void AddMapping(string className, string methodName, int bytecodeOffset
			, int sourceLine)
		{
			IDictionary<string, IDictionary<int, int>> class_mapping = mapping.ComputeIfAbsent
				(className, (string k) => new LinkedHashMap<string, IDictionary<int, int>>());
			// need to preserve order
			IDictionary<int, int> method_mapping = class_mapping.ComputeIfAbsent(methodName, 
				(string k) => new Dictionary<int, int>());
			// don't overwrite
			method_mapping.PutIfAbsent(bytecodeOffset, sourceLine);
		}

		public virtual void AddTracer(string className, string methodName, BytecodeMappingTracer
			 tracer)
		{
			foreach (KeyValuePair<int, int> entry in tracer.GetMapping())
			{
				AddMapping(className, methodName, entry.Key, entry.Value);
			}
			Sharpen.Collections.PutAll(linesMapping, tracer.GetOriginalLinesMapping());
			Sharpen.Collections.AddAll(unmappedLines, tracer.GetUnmappedLines());
		}

		public virtual void DumpMapping(TextBuffer buffer, bool offsetsToHex)
		{
			if ((mapping.Count == 0) && (linesMapping.Count == 0))
			{
				return;
			}
			string lineSeparator = DecompilerContext.GetNewLineSeparator();
			foreach (KeyValuePair<string, IDictionary<string, IDictionary<int, int>>> class_entry
				 in mapping)
			{
				IDictionary<string, IDictionary<int, int>> class_mapping = class_entry.Value;
				buffer.Append("class '" + class_entry.Key + "' {" + lineSeparator);
				bool is_first_method = true;
				foreach (KeyValuePair<string, IDictionary<int, int>> method_entry in class_mapping)
				{
					IDictionary<int, int> method_mapping = method_entry.Value;
					if (!is_first_method)
					{
						buffer.AppendLineSeparator();
					}
					buffer.AppendIndent(1).Append("method '" + method_entry.Key + "' {" + lineSeparator
						);
					List<int> lstBytecodeOffsets = new List<int>(method_mapping.Keys);
					lstBytecodeOffsets.Sort();
					foreach (int offset in lstBytecodeOffsets)
					{
						int? line = method_mapping.GetOrNullable(offset);
						string strOffset = offsetsToHex ? int.ToHexString(offset) : line.ToString();
						buffer.AppendIndent(2).Append(strOffset).AppendIndent(2).Append((line.Value + offset_total
							) + lineSeparator);
					}
					buffer.AppendIndent(1).Append("}").AppendLineSeparator();
					is_first_method = false;
				}
				buffer.Append("}").AppendLineSeparator().AppendLineSeparator();
			}
			// lines mapping
			buffer.Append("Lines mapping:").AppendLineSeparator();
			IDictionary<int, int> sorted = new SortedDictionary<int, int>(linesMapping);
			foreach (KeyValuePair<int, int> entry in sorted)
			{
				buffer.Append(entry.Key).Append(" <-> ").Append(entry.Value + offset_total + 1).AppendLineSeparator
					();
			}
			if (!(unmappedLines.Count == 0))
			{
				buffer.Append("Not mapped:").AppendLineSeparator();
				foreach (int line in unmappedLines)
				{
					if (!linesMapping.ContainsKey(line))
					{
						buffer.Append(line).AppendLineSeparator();
					}
				}
			}
		}

		public virtual void AddTotalOffset(int offset_total)
		{
			this.offset_total += offset_total;
		}

		/// <summary>Original to decompiled line mapping.</summary>
		public virtual int[] GetOriginalLinesMapping()
		{
			int[] res = new int[linesMapping.Count * 2];
			int i = 0;
			foreach (KeyValuePair<int, int> entry in linesMapping)
			{
				res[i] = entry.Key;
				unmappedLines.Remove(entry.Key);
				res[i + 1] = entry.Value + offset_total + 1;
				// make it 1 based
				i += 2;
			}
			return res;
		}
	}
}
