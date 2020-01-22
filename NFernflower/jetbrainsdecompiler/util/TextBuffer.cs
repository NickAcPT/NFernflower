/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System.Collections.Generic;
using System.Text;
using Java.Util;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using Sharpen;

namespace JetBrainsDecompiler.Util
{
	/// <summary>Allows to connect text with resulting lines</summary>
	/// <author>egor</author>
	public class TextBuffer
	{
		private readonly string myLineSeparator = DecompilerContext.GetNewLineSeparator();

		private readonly string myIndent = (string)DecompilerContext.GetProperty(IIFernflowerPreferences
			.Indent_String);

		private readonly StringBuilder myStringBuilder;

		private IDictionary<int, int> myLineToOffsetMapping = null;

		public TextBuffer()
		{
			myStringBuilder = new StringBuilder();
		}

		public TextBuffer(int size)
		{
			myStringBuilder = new StringBuilder(size);
		}

		public TextBuffer(string text)
		{
			myStringBuilder = new StringBuilder(text);
		}

		public virtual TextBuffer Append(string str)
		{
			myStringBuilder.Append(str);
			return this;
		}

		public virtual TextBuffer Append(char ch)
		{
			myStringBuilder.Append(ch);
			return this;
		}

		public virtual TextBuffer Append(int i)
		{
			myStringBuilder.Append(i);
			return this;
		}

		public virtual TextBuffer AppendLineSeparator()
		{
			myStringBuilder.Append(myLineSeparator);
			return this;
		}

		public virtual TextBuffer AppendIndent(int length)
		{
			while (length-- > 0)
			{
				Append(myIndent);
			}
			return this;
		}

		public virtual TextBuffer Prepend(string s)
		{
			myStringBuilder.Insert(0, s);
			ShiftMapping(s.Length);
			return this;
		}

		public virtual TextBuffer Enclose(string left, string right)
		{
			Prepend(left);
			Append(right);
			return this;
		}

		public virtual bool ContainsOnlyWhitespaces()
		{
			for (int i = 0; i < myStringBuilder.Length; i++)
			{
				if (myStringBuilder[i] != ' ')
				{
					return false;
				}
			}
			return true;
		}

		public override string ToString()
		{
			string original = myStringBuilder.ToString();
			if (myLineToOffsetMapping == null || (myLineToOffsetMapping.Count == 0))
			{
				if (myLineMapping != null)
				{
					return AddOriginalLineNumbers();
				}
				return original;
			}
			else
			{
				StringBuilder res = new StringBuilder();
				string[] srcLines = original.Split(myLineSeparator);
				int currentLineStartOffset = 0;
				int currentLine = 0;
				int previousMarkLine = 0;
				int dumpedLines = 0;
				List<int> linesWithMarks = new List<int>(myLineToOffsetMapping.Keys);
				linesWithMarks.Sort();
				foreach (int markLine in linesWithMarks)
				{
					int? markOffset = myLineToOffsetMapping.GetOrNullable(markLine);
					while (currentLine < srcLines.Length)
					{
						string line = srcLines[currentLine];
						int lineEnd = currentLineStartOffset + line.Length + myLineSeparator.Length;
						if (markOffset.Value <= lineEnd)
						{
							int requiredLine = markLine - 1;
							int linesToAdd = requiredLine - dumpedLines;
							dumpedLines = requiredLine;
							AppendLines(res, srcLines, previousMarkLine, currentLine, linesToAdd);
							previousMarkLine = currentLine;
							break;
						}
						currentLineStartOffset = lineEnd;
						currentLine++;
					}
				}
				if (previousMarkLine < srcLines.Length)
				{
					AppendLines(res, srcLines, previousMarkLine, srcLines.Length, srcLines.Length - previousMarkLine
						);
				}
				return res.ToString();
			}
		}

		private string AddOriginalLineNumbers()
		{
			StringBuilder sb = new StringBuilder();
			int lineStart = 0;
			int lineEnd;
			int count = 0;
			int length = myLineSeparator.Length;
			while ((lineEnd = myStringBuilder.IndexOf(myLineSeparator, lineStart)) > 0)
			{
				++count;
				sb.Append(myStringBuilder.Substring(lineStart, lineEnd));
				HashSet<int> integers = myLineMapping.GetOrNull(count);
				if (integers != null)
				{
					sb.Append("//");
					foreach (int integer in integers)
					{
						sb.Append(' ').Append(integer);
					}
				}
				sb.Append(myLineSeparator);
				lineStart = lineEnd + length;
			}
			if (lineStart < myStringBuilder.Length)
			{
				sb.Append(myStringBuilder.Substring(lineStart));
			}
			return sb.ToString();
		}

		private void AppendLines(StringBuilder res, string[] srcLines, int from, int to, 
			int requiredLineNumber)
		{
			if (to - from > requiredLineNumber)
			{
				List<string> strings = CompactLines(Sharpen.Arrays.AsList(srcLines).SubList(from
					, to), requiredLineNumber);
				int separatorsRequired = requiredLineNumber - 1;
				foreach (string s in strings)
				{
					res.Append(s);
					if (separatorsRequired-- > 0)
					{
						res.Append(myLineSeparator);
					}
				}
				res.Append(myLineSeparator);
			}
			else if (to - from <= requiredLineNumber)
			{
				for (int i = from; i < to; i++)
				{
					res.Append(srcLines[i]).Append(myLineSeparator);
				}
				for (int i = 0; i < requiredLineNumber - to + from; i++)
				{
					res.Append(myLineSeparator);
				}
			}
		}

		public virtual int Length()
		{
			return myStringBuilder.Length;
		}

		public virtual void SetStart(int position)
		{
			myStringBuilder.Delete(0, position);
			ShiftMapping(-position);
		}

		public virtual void SetLength(int position)
		{
			myStringBuilder.Length = position;
			if (myLineToOffsetMapping != null)
			{
				IDictionary<int, int> newMap = new Dictionary<int, int>();
				foreach (KeyValuePair<int, int> entry in myLineToOffsetMapping)
				{
					if (entry.Value <= position)
					{
						Sharpen.Collections.Put(newMap, entry.Key, entry.Value);
					}
				}
				myLineToOffsetMapping = newMap;
			}
		}

		public virtual TextBuffer Append(TextBuffer buffer)
		{
			if (buffer.myLineToOffsetMapping != null && !(buffer.myLineToOffsetMapping.Count == 0))
			{
				CheckMapCreated();
				foreach (KeyValuePair<int, int> entry in buffer.myLineToOffsetMapping)
				{
					Sharpen.Collections.Put(myLineToOffsetMapping, entry.Key, entry.Value + myStringBuilder
						.Length);
				}
			}
			myStringBuilder.Append(buffer.myStringBuilder);
			return this;
		}

		private void ShiftMapping(int shiftOffset)
		{
			if (myLineToOffsetMapping != null)
			{
				IDictionary<int, int> newMap = new Dictionary<int, int>();
				foreach (KeyValuePair<int, int> entry in myLineToOffsetMapping)
				{
					int newValue = entry.Value;
					if (newValue >= 0)
					{
						newValue += shiftOffset;
					}
					if (newValue >= 0)
					{
						Sharpen.Collections.Put(newMap, entry.Key, newValue);
					}
				}
				myLineToOffsetMapping = newMap;
			}
		}

		private void CheckMapCreated()
		{
			if (myLineToOffsetMapping == null)
			{
				myLineToOffsetMapping = new Dictionary<int, int>();
			}
		}

		public virtual int CountLines()
		{
			return CountLines(0);
		}

		public virtual int CountLines(int from)
		{
			return Count(myLineSeparator, from);
		}

		public virtual int Count(string substring, int from)
		{
			int count = 0;
			int length = substring.Length;
			int p = from;
			while ((p = myStringBuilder.IndexOf(substring, p)) > 0)
			{
				++count;
				p += length;
			}
			return count;
		}

		private static List<string> CompactLines(List<string> srcLines, int requiredLineNumber
			)
		{
			if (srcLines.Count < 2 || srcLines.Count <= requiredLineNumber)
			{
				return srcLines;
			}
			List<string> res = new LinkedList<string>(srcLines);
			// first join lines with a single { or }
			for (int i = res.Count - 1; i > 0; i--)
			{
				string s = res[i];
				if (s.Trim().Equals("{") || s.Trim().Equals("}"))
				{
					res[i - 1] = res[i - 1].Concat(s);
					res.RemoveAtReturningValue(i);
				}
				if (res.Count <= requiredLineNumber)
				{
					return res;
				}
			}
			// now join empty lines
			for (int i = res.Count - 1; i > 0; i--)
			{
				string s = res[i];
				if ((s.Trim().Length == 0))
				{
					res[i - 1] = res[i - 1].Concat(s);
					res.RemoveAtReturningValue(i);
				}
				if (res.Count <= requiredLineNumber)
				{
					return res;
				}
			}
			return res;
		}

		private IDictionary<int, HashSet<int>> myLineMapping = null;

		// new to original
		public virtual void DumpOriginalLineNumbers(int[] lineMapping)
		{
			if (lineMapping.Length > 0)
			{
				myLineMapping = new Dictionary<int, HashSet<int>>();
				for (int i = 0; i < lineMapping.Length; i += 2)
				{
					int key = lineMapping[i + 1];
					HashSet<int> existing = myLineMapping.ComputeIfAbsent(key, (int k) => new TreeSet
						<int>());
					existing.Add(lineMapping[i]);
				}
			}
		}
	}
}
