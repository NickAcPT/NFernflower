// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.

using System.Collections;
using System.Collections.Generic;
using Sharpen;

namespace JetBrainsDecompiler.Util
{
	public class FastSparseSetFactory<E>
	{
		private readonly VBStyleCollection<int[], E> colValuesInternal = new VBStyleCollection
			<int[], E>();

		private int lastBlock;

		private int lastMask;

		public FastSparseSetFactory(ICollection<E> set)
		{
			int block = -1;
			int mask = -1;
			int index = 0;
			foreach (var element in set)
			{
				block = index / 32;
				if (index % 32 == 0)
				{
					mask = 1;
				}
				else
				{
					mask <<= 1;
				}
				colValuesInternal.PutWithKey(new int[] { block, mask }, element);
				index++;
			}
			lastBlock = block;
			lastMask = mask;
		}

		private int[] AddElement(E element)
		{
			if (lastMask == -1 || lastMask == unchecked((int)(0x80000000)))
			{
				lastMask = 1;
				lastBlock++;
			}
			else
			{
				lastMask <<= 1;
			}
			int[] pointer = new int[] { lastBlock, lastMask };
			colValuesInternal.PutWithKey(pointer, element);
			return pointer;
		}

		public virtual FastSparseSetFactory<E>.FastSparseSet<E> SpawnEmptySet()
		{
			return new FastSparseSetFactory<E>.FastSparseSet<E>(this);
		}

		public virtual int GetLastBlock()
		{
			return lastBlock;
		}

		private VBStyleCollection<int[], E> GetInternalValuesCollection()
		{
			return colValuesInternal;
		}

		public class FastSparseSet<E> : IEnumerable<E>
		{
			public static readonly FastSparseSetFactory<E>.FastSparseSet<E>[] Empty_Array = new FastSparseSetFactory<E>.FastSparseSet<E>
				[0];

			private readonly FastSparseSetFactory<E> factory;

			private readonly VBStyleCollection<int[], E> colValuesInternal;

			private int[] data;

			private int[] next;

			internal FastSparseSet(FastSparseSetFactory<E> factory)
			{
				this.factory = factory;
				this.colValuesInternal = factory.GetInternalValuesCollection();
				int length = factory.GetLastBlock() + 1;
				this.data = new int[length];
				this.next = new int[length];
			}

			private FastSparseSet(FastSparseSetFactory<E> factory, int[] data, int[] next)
			{
				this.factory = factory;
				this.colValuesInternal = factory.GetInternalValuesCollection();
				this.data = data;
				this.next = next;
			}

			public virtual FastSparseSetFactory<E>.FastSparseSet<E> GetCopy()
			{
				int arrlength = data.Length;
				int[] cpdata = new int[arrlength];
				int[] cpnext = new int[arrlength];
				System.Array.Copy(data, 0, cpdata, 0, arrlength);
				System.Array.Copy(next, 0, cpnext, 0, arrlength);
				return new FastSparseSetFactory<E>.FastSparseSet<E>(factory, cpdata, cpnext);
			}

			private int[] EnsureCapacity(int index)
			{
				int newlength = data.Length;
				if (newlength == 0)
				{
					newlength = 1;
				}
				while (newlength <= index)
				{
					newlength *= 2;
				}
				int[] newdata = new int[newlength];
				System.Array.Copy(data, 0, newdata, 0, data.Length);
				data = newdata;
				int[] newnext = new int[newlength];
				System.Array.Copy(next, 0, newnext, 0, next.Length);
				next = newnext;
				return newdata;
			}

			public virtual void Add(E element)
			{
				int[] index = colValuesInternal.GetWithKey(element);
				if (index == null)
				{
					index = factory.AddElement(element);
				}
				int block = index[0];
				if (block >= data.Length)
				{
					EnsureCapacity(block);
				}
				data[block] |= index[1];
				ChangeNext(next, block, next[block], block);
			}

			public virtual void Remove(E element)
			{
				int[] index = colValuesInternal.GetWithKey(element);
				if (index == null)
				{
					index = factory.AddElement(element);
				}
				int block = index[0];
				if (block < data.Length)
				{
					data[block] &= ~index[1];
					if (data[block] == 0)
					{
						ChangeNext(next, block, block, next[block]);
					}
				}
			}

			public virtual bool Contains(E element)
			{
				int[] index = colValuesInternal.GetWithKey(element);
				if (index == null)
				{
					index = factory.AddElement(element);
				}
				return index[0] < data.Length && ((data[index[0]] & index[1]) != 0);
			}

			private void SetNext()
			{
				int link = 0;
				for (int i = data.Length - 1; i >= 0; i--)
				{
					next[i] = link;
					if (data[i] != 0)
					{
						link = i;
					}
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

			public virtual void Union(FastSparseSetFactory<E>.FastSparseSet<E> set)
			{
				int[] extdata = set.GetData();
				int[] extnext = set.GetNext();
				int[] intdata = data;
				int intlength = intdata.Length;
				int pointer = 0;
				do
				{
					if (pointer >= intlength)
					{
						intdata = EnsureCapacity(extdata.Length - 1);
					}
					bool nextrec = (intdata[pointer] == 0);
					intdata[pointer] |= extdata[pointer];
					if (nextrec)
					{
						ChangeNext(next, pointer, next[pointer], pointer);
					}
					pointer = extnext[pointer];
				}
				while (pointer != 0);
			}

			public virtual void Intersection(FastSparseSetFactory<E>.FastSparseSet<E> set)
			{
				int[] extdata = set.GetData();
				int[] intdata = data;
				int minlength = System.Math.Min(extdata.Length, intdata.Length);
				for (int i = minlength - 1; i >= 0; i--)
				{
					intdata[i] &= extdata[i];
				}
				for (int i = intdata.Length - 1; i >= minlength; i--)
				{
					intdata[i] = 0;
				}
				SetNext();
			}

			public virtual void Complement(FastSparseSetFactory<E>.FastSparseSet<E> set)
			{
				int[] extdata = set.GetData();
				int[] intdata = data;
				int extlength = extdata.Length;
				int pointer = 0;
				do
				{
					if (pointer >= extlength)
					{
						break;
					}
					intdata[pointer] &= ~extdata[pointer];
					if (intdata[pointer] == 0)
					{
						ChangeNext(next, pointer, pointer, next[pointer]);
					}
					pointer = next[pointer];
				}
				while (pointer != 0);
			}

			public override bool Equals(object o)
			{
				if (o == this)
				{
					return true;
				}
				if (!(Sharpen.Runtime.InstanceOf(o, typeof(FastSparseSetFactory<E>.FastSparseSet<E>))
					))
				{
					return false;
				}
				int[] longdata = ((FastSparseSetFactory<E>.FastSparseSet<E>)o).GetData();
				int[] shortdata = data;
				if (data.Length > longdata.Length)
				{
					shortdata = longdata;
					longdata = data;
				}
				for (int i = shortdata.Length - 1; i >= 0; i--)
				{
					if (shortdata[i] != longdata[i])
					{
						return false;
					}
				}
				for (int i = longdata.Length - 1; i >= shortdata.Length; i--)
				{
					if (longdata[i] != 0)
					{
						return false;
					}
				}
				return true;
			}

			public virtual int GetCardinality()
			{
				bool found = false;
				int[] intdata = data;
				for (int i = intdata.Length - 1; i >= 0; i--)
				{
					int block = intdata[i];
					if (block != 0)
					{
						if (found)
						{
							return 2;
						}
						else if ((block & (block - 1)) == 0)
						{
							found = true;
						}
						else
						{
							return 2;
						}
					}
				}
				return found ? 1 : 0;
			}

			public virtual bool IsEmpty()
			{
				return data.Length == 0 || (next[0] == 0 && data[0] == 0);
			}

			public IEnumerator<E> GetEnumerator()
			{
				return new FastSparseSetFactory<E>.FastSparseSetIterator<E>(this);
			}

			public virtual HashSet<E> ToPlainSet()
			{
				HashSet<E> set = new HashSet<E>();
				int[] intdata = data;
				int size = data.Length * 32;
				if (size > colValuesInternal.Count)
				{
					size = colValuesInternal.Count;
				}
				for (int i = size - 1; i >= 0; i--)
				{
					int[] index = colValuesInternal[i];
					if ((intdata[index[0]] & index[1]) != 0)
					{
						set.Add(colValuesInternal.GetKey(i));
					}
				}
				return set;
			}

			public override string ToString()
			{
				return ToPlainSet().ToString();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			internal int[] GetData()
			{
				return data;
			}

			internal int[] GetNext()
			{
				return next;
			}

			public virtual FastSparseSetFactory<E> GetFactory()
			{
				return factory;
			}
		}

		public class FastSparseSetIterator<E> : IEnumerator<E>
		{
			private readonly VBStyleCollection<int[], E> colValuesInternal;

			private readonly int[] data;

			private readonly int[] next;

			private readonly int size;

			private int pointer = -1;

			private int next_pointer = -1;

			internal FastSparseSetIterator(object mySet)
			{
				var set = (FastSparseSetFactory<E>.FastSparseSet<E>) mySet;
				colValuesInternal = set.GetFactory().GetInternalValuesCollection();
				data = set.GetData();
				next = set.GetNext();
				size = colValuesInternal.Count;
			}

			private int GetNextIndex(int index)
			{
				index++;
				int bindex = (int)(((uint)index) >> 5);
				int dindex = index & unchecked((int)(0x1F));
				while (bindex < data.Length)
				{
					int block = data[bindex];
					if (block != 0)
					{
						block = (int)(((uint)block) >> dindex);
						while (dindex < 32)
						{
							if ((block & 1) != 0)
							{
								return (bindex << 5) + dindex;
							}
							block = (int)(((uint)block) >> 1);
							dindex++;
						}
					}
					dindex = 0;
					bindex = next[bindex];
					if (bindex == 0)
					{
						break;
					}
				}
				return -1;
			}

			public bool MoveNext()
			{
				next_pointer = GetNextIndex(pointer);
				return (next_pointer >= 0);
			}

			public void Reset()
			{
				throw new System.NotImplementedException();
			}

			object? IEnumerator.Current => Current;

			public E Current
			{
				get
				{
					if (next_pointer >= 0)
					{
						pointer = next_pointer;
					}
					else
					{
						pointer = GetNextIndex(pointer);
						if (pointer == -1)
						{
							pointer = size;
						}
					}
					next_pointer = -1;
					return pointer < size ? colValuesInternal.GetKey(pointer) : default;
				}
			}

			public void Remove()
			{
				int[] index = colValuesInternal[pointer];
				data[index[0]] &= ~index[1];
			}

			public void Dispose()
			{
			}
		}
	}
}
