// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using System.Text;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using Sharpen;

namespace JetBrainsDecompiler.Util
{
	public class SFormsFastMapDirect
	{
		private int size__;

		private readonly FastSparseSetFactory.FastSparseSet<int>[][] elements = new FastSparseSetFactory.FastSparseSet
			[3][];

		private readonly int[][] next = new int[3][];

		public SFormsFastMapDirect()
			: this(true)
		{
		}

		private SFormsFastMapDirect(bool initialize)
		{
			if (initialize)
			{
				for (int i = 2; i >= 0; i--)
				{
					FastSparseSetFactory.FastSparseSet<int>[] empty = FastSparseSetFactory.FastSparseSet
						.Empty_Array;
					elements[i] = empty;
					next[i] = InterpreterUtil.Empty_Int_Array;
				}
			}
		}

		public SFormsFastMapDirect(SFormsFastMapDirect map)
		{
			for (int i = 2; i >= 0; i--)
			{
				FastSparseSetFactory.FastSparseSet<int>[] arr = map.elements[i];
				int[] arrnext = map.next[i];
				int length = arr.Length;
				FastSparseSetFactory.FastSparseSet<int>[] arrnew = new FastSparseSetFactory.FastSparseSet
					[length];
				int[] arrnextnew = new int[length];
				System.Array.Copy(arr, 0, arrnew, 0, length);
				System.Array.Copy(arrnext, 0, arrnextnew, 0, length);
				elements[i] = arrnew;
				next[i] = arrnextnew;
				size__ = map.size;
			}
		}

		public virtual SFormsFastMapDirect GetCopy()
		{
			SFormsFastMapDirect map = new SFormsFastMapDirect(false);
			map.size = size__;
			FastSparseSetFactory.FastSparseSet[][] mapelements = map.elements;
			int[][] mapnext = map.next;
			for (int i = 2; i >= 0; i--)
			{
				FastSparseSetFactory.FastSparseSet<int>[] arr = elements[i];
				int length = arr.Length;
				if (length > 0)
				{
					int[] arrnext = next[i];
					FastSparseSetFactory.FastSparseSet<int>[] arrnew = new FastSparseSetFactory.FastSparseSet
						[length];
					int[] arrnextnew = new int[length];
					System.Array.Copy(arrnext, 0, arrnextnew, 0, length);
					mapelements[i] = arrnew;
					mapnext[i] = arrnextnew;
					int pointer = 0;
					do
					{
						FastSparseSetFactory.FastSparseSet<int> set = arr[pointer];
						if (set != null)
						{
							arrnew[pointer] = set.GetCopy();
						}
						pointer = arrnext[pointer];
					}
					while (pointer != 0);
				}
				else
				{
					mapelements[i] = FastSparseSetFactory.FastSparseSet.Empty_Array;
					mapnext[i] = InterpreterUtil.Empty_Int_Array;
				}
			}
			return map;
		}

		public virtual int Size()
		{
			return size__;
		}

		public virtual bool IsEmpty()
		{
			return size__ == 0;
		}

		public virtual void Put(int key, FastSparseSetFactory.FastSparseSet<int> value)
		{
			PutInternal(key, value, false);
		}

		public virtual void RemoveAllFields()
		{
			FastSparseSetFactory.FastSparseSet<int>[] arr = elements[2];
			int[] arrnext = next[2];
			for (int i = arr.Length - 1; i >= 0; i--)
			{
				FastSparseSetFactory.FastSparseSet<int> val = arr[i];
				if (val != null)
				{
					arr[i] = null;
					size__--;
				}
				arrnext[i] = 0;
			}
		}

		public virtual void PutInternal(int key, FastSparseSetFactory.FastSparseSet<int> 
			value, bool remove)
		{
			int index = 0;
			int ikey = key;
			if (ikey < 0)
			{
				index = 2;
				ikey = -ikey;
			}
			else if (ikey >= VarExprent.Stack_Base)
			{
				index = 1;
				ikey -= VarExprent.Stack_Base;
			}
			FastSparseSetFactory.FastSparseSet<int>[] arr = elements[index];
			if (ikey >= arr.Length)
			{
				if (remove)
				{
					return;
				}
				else
				{
					arr = EnsureCapacity(index, ikey + 1, false);
				}
			}
			FastSparseSetFactory.FastSparseSet<int> oldval = arr[ikey];
			arr[ikey] = value;
			int[] arrnext = next[index];
			if (oldval == null && value != null)
			{
				size__++;
				ChangeNext(arrnext, ikey, arrnext[ikey], ikey);
			}
			else if (oldval != null && value == null)
			{
				size__--;
				ChangeNext(arrnext, ikey, ikey, arrnext[ikey]);
			}
		}

		private static void ChangeNext(int[] arrnext, int key, int oldnext, int newnext)
		{
			for (int i = key - 1; i >= 0; i--)
			{
				if (arrnext[i] == oldnext)
				{
					arrnext[i] = newnext;
				}
				else
				{
					break;
				}
			}
		}

		public virtual bool ContainsKey(int key)
		{
			return Get(key) != null;
		}

		public virtual FastSparseSetFactory.FastSparseSet<int> Get(int key)
		{
			int index = 0;
			if (key < 0)
			{
				index = 2;
				key = -key;
			}
			else if (key >= VarExprent.Stack_Base)
			{
				index = 1;
				key -= VarExprent.Stack_Base;
			}
			FastSparseSetFactory.FastSparseSet<int>[] arr = elements[index];
			if (key < arr.Length)
			{
				return arr[key];
			}
			return null;
		}

		public virtual void Complement(SFormsFastMapDirect map)
		{
			for (int i = 2; i >= 0; i--)
			{
				FastSparseSetFactory.FastSparseSet<int>[] lstOwn = elements[i];
				if (lstOwn.Length == 0)
				{
					continue;
				}
				FastSparseSetFactory.FastSparseSet<int>[] lstExtern = map.elements[i];
				int[] arrnext = next[i];
				int pointer = 0;
				do
				{
					FastSparseSetFactory.FastSparseSet<int> first = lstOwn[pointer];
					if (first != null)
					{
						if (pointer >= lstExtern.Length)
						{
							break;
						}
						FastSparseSetFactory.FastSparseSet<int> second = lstExtern[pointer];
						if (second != null)
						{
							first.Complement(second);
							if (first.IsEmpty())
							{
								lstOwn[pointer] = null;
								size__--;
								ChangeNext(arrnext, pointer, pointer, arrnext[pointer]);
							}
						}
					}
					pointer = arrnext[pointer];
				}
				while (pointer != 0);
			}
		}

		public virtual void Intersection(SFormsFastMapDirect map)
		{
			for (int i = 2; i >= 0; i--)
			{
				FastSparseSetFactory.FastSparseSet<int>[] lstOwn = elements[i];
				if (lstOwn.Length == 0)
				{
					continue;
				}
				FastSparseSetFactory.FastSparseSet<int>[] lstExtern = map.elements[i];
				int[] arrnext = next[i];
				int pointer = 0;
				do
				{
					FastSparseSetFactory.FastSparseSet<int> first = lstOwn[pointer];
					if (first != null)
					{
						FastSparseSetFactory.FastSparseSet<int> second = null;
						if (pointer < lstExtern.Length)
						{
							second = lstExtern[pointer];
						}
						if (second != null)
						{
							first.Intersection(second);
						}
						if (second == null || first.IsEmpty())
						{
							lstOwn[pointer] = null;
							size__--;
							ChangeNext(arrnext, pointer, pointer, arrnext[pointer]);
						}
					}
					pointer = arrnext[pointer];
				}
				while (pointer != 0);
			}
		}

		public virtual void Union(SFormsFastMapDirect map)
		{
			for (int i = 2; i >= 0; i--)
			{
				FastSparseSetFactory.FastSparseSet<int>[] lstExtern = map.elements[i];
				if (lstExtern.Length == 0)
				{
					continue;
				}
				FastSparseSetFactory.FastSparseSet<int>[] lstOwn = elements[i];
				int[] arrnext = next[i];
				int[] arrnextExtern = map.next[i];
				int pointer = 0;
				do
				{
					if (pointer >= lstOwn.Length)
					{
						lstOwn = EnsureCapacity(i, lstExtern.Length, true);
						arrnext = next[i];
					}
					FastSparseSetFactory.FastSparseSet<int> second = lstExtern[pointer];
					if (second != null)
					{
						FastSparseSetFactory.FastSparseSet<int> first = lstOwn[pointer];
						if (first == null)
						{
							lstOwn[pointer] = second.GetCopy();
							size__++;
							ChangeNext(arrnext, pointer, arrnext[pointer], pointer);
						}
						else
						{
							first.Union(second);
						}
					}
					pointer = arrnextExtern[pointer];
				}
				while (pointer != 0);
			}
		}

		public override string ToString()
		{
			StringBuilder buffer = new StringBuilder("{");
			List<KeyValuePair<int, FastSparseSetFactory.FastSparseSet<int>>> lst = EntryList
				();
			if (lst != null)
			{
				bool first = true;
				foreach (KeyValuePair<int, FastSparseSetFactory.FastSparseSet<int>> entry in lst)
				{
					if (!first)
					{
						buffer.Append(", ");
					}
					else
					{
						first = false;
					}
					HashSet<int> set = entry.Value.ToPlainSet();
					buffer.Append(entry.Key).Append("={").Append(set.ToString()).Append("}");
				}
			}
			buffer.Append("}");
			return buffer.ToString();
		}

		public virtual List<KeyValuePair<int, FastSparseSetFactory.FastSparseSet<int>>> 
			EntryList()
		{
			List<KeyValuePair<int, FastSparseSetFactory.FastSparseSet<int>>> list = new List
				<KeyValuePair<int, FastSparseSetFactory.FastSparseSet<int>>>();
			for (int i = 2; i >= 0; i--)
			{
				int ikey = 0;
				foreach (FastSparseSetFactory.FastSparseSet<int> ent in elements[i])
				{
					if (ent != null)
					{
						int key = i == 0 ? ikey : (i == 1 ? ikey + VarExprent.Stack_Base : -ikey);
						list.Add(new _KeyValuePair_346(key, ent));
					}
					ikey++;
				}
			}
			return list;
		}

		private sealed class _KeyValuePair_346 : KeyValuePair<int, FastSparseSetFactory.FastSparseSet
			<int>>
		{
			public _KeyValuePair_346(int key, FastSparseSetFactory.FastSparseSet<int> ent)
			{
				this.key = key;
				this.ent = ent;
				this.var = key;
				this.val = ent;
			}

			private readonly int var;

			private readonly FastSparseSetFactory.FastSparseSet<int> val;

			public int Key
			{
				get
				{
					return this.var;
				}
			}

			public FastSparseSetFactory.FastSparseSet<int> Value
			{
				get
				{
					return this.val;
				}
			}

			public FastSparseSetFactory.FastSparseSet<int> SetValue(FastSparseSetFactory.FastSparseSet
				<int> newvalue)
			{
				return null;
			}

			private readonly int key;

			private readonly FastSparseSetFactory.FastSparseSet<int> ent;
		}

		private FastSparseSetFactory.FastSparseSet<int>[] EnsureCapacity(int index, int size
			, bool exact)
		{
			FastSparseSetFactory.FastSparseSet<int>[] arr = elements[index];
			int[] arrnext = next[index];
			int minsize = size;
			if (!exact)
			{
				minsize = 2 * arr.Length / 3 + 1;
				if (size > minsize)
				{
					minsize = size;
				}
			}
			FastSparseSetFactory.FastSparseSet<int>[] arrnew = new FastSparseSetFactory.FastSparseSet
				[minsize];
			System.Array.Copy(arr, 0, arrnew, 0, arr.Length);
			int[] arrnextnew = new int[minsize];
			System.Array.Copy(arrnext, 0, arrnextnew, 0, arrnext.Length);
			elements[index] = arrnew;
			next[index] = arrnextnew;
			return arrnew;
		}
	}
}
