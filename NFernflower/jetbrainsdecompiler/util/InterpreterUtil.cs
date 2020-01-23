// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using JetBrainsDecompiler.Code.Cfg;
using ObjectWeb.Misc.Java.IO;
using Sharpen;
using ZipFile = System.IO.Compression.ZipFile;

namespace JetBrainsDecompiler.Util
{
	public class InterpreterUtil
	{
		public static readonly bool Is_Windows = System.Runtime.InteropServices.RuntimeInformation
			.IsOSPlatform(OSPlatform.Windows);

		public static readonly int[] Empty_Int_Array = new int[0];

		private const int Buffer_Size = 16 * 1024;

		/// <exception cref="IOException"/>
		public static void CopyFile(FileInfo source, FileInfo target)
		{
			File.Copy(source.FullName, target.FullName);
		}

		/// <exception cref="IOException"/>
		/*
		public static void CopyStream(InputStream @in, ZipOutputStream @out)
		{
			byte[] buffer = new byte[Buffer_Size];
			int len;
			while ((len = @in.Read(buffer)) >= 0)
			{
				@out.Write(buffer, 0, len);
			}
		}
		*/
		public static void CopyStream(InputStream @in, Stream @out)
		{
			byte[] buffer = new byte[Buffer_Size];
			int len;
			while ((len = @in.Read(buffer)) >= 0)
			{
				@out.Write(buffer, 0, len);
			}
		}

		/// <exception cref="IOException"/>
		public static byte[] GetBytes(ZipArchive archive, ZipArchiveEntry entry)
		{
			return entry.Open().ReadFully();
		}

		/// <exception cref="IOException"/>
		public static byte[] GetBytes(FileInfo file)
		{
			return File.ReadAllBytes(file.FullName);
		}

		/// <exception cref="IOException"/>
		public static byte[] ReadBytes(InputStream stream, int length)
		{
			byte[] bytes = new byte[length];
			int n = 0;
			int off = 0;
			while (n < length)
			{
				int count = stream.Read(bytes, off + n, length - n);
				if (count < 0)
				{
					throw new IOException("premature end of stream");
				}
				n += count;
			}
			return bytes;
		}

		/// <exception cref="IOException"/>
		public static void DiscardBytes(InputStream stream, int length)
		{
			if (stream.Skip(length) != length)
			{
				throw new IOException("premature end of stream");
			}
		}

		public static bool EqualSets(List<BasicBlock> c1, List<BasicBlock> c2)
		{
			if (c1 == null)
			{
				return c2 == null;
			}
			else if (c2 == null)
			{
				return false;
			}
			if (c1.Count != c2.Count)
			{
				return false;
			}
			HashSet<object> set = new HashSet<object>(c1);
			set.ExceptWith(c2);
			return (set.Count == 0);
		}

		public static bool EqualObjects(object first, object second)
		{
			return first == null ? second == null : first.Equals(second);
		}

		public static bool EqualLists(List<object> first, List<object> second)
		{
			if (first == null)
			{
				return second == null;
			}
			else if (second == null)
			{
				return false;
			}
			if (first.Count == second.Count)
			{
				for (int i = 0; i < first.Count; i++)
				{
					if (!EqualObjects(first[i], second[i]))
					{
						return false;
					}
				}
				return true;
			}
			return false;
		}

		public static bool EqualLists<T>(List<T> first, List<T> second)
		{
			if (first == null)
			{
				return second == null;
			}
			else if (second == null)
			{
				return false;
			}
			if (first.Count == second.Count)
			{
				for (int i = 0; i < first.Count; i++)
				{
					if (!EqualObjects(first[i], second[i]))
					{
						return false;
					}
				}
				return true;
			}
			return false;
		}

		public static string MakeUniqueKey(string name, string descriptor)
		{
			return name + ' ' + descriptor;
		}

		public static string MakeUniqueKey(string name, string descriptor1, string descriptor2
			)
		{
			return name + ' ' + descriptor1 + ' ' + descriptor2;
		}
	}
}
