// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using System.Text;
using Sharpen;

namespace JetBrainsDecompiler.Util
{
	public class FastFixedSetFactory<E>
	{
		private readonly VBStyleCollection<int[], E> colValuesInternal = new VBStyleCollection
			<int[], E>();

		private readonly int dataLength;

		public FastFixedSetFactory(ICollection<E> set)
		{
			dataLength = set.Count / 32 + 1;
			int index = 0;
			int mask = 1;
			foreach (E element in set)
			{
				int block = index / 32;
				if (index % 32 == 0)
				{
					mask = 1;
				}
				colValuesInternal.PutWithKey(new int[] { block, mask }, element);
				index++;
				mask <<= 1;
			}
		}

		public virtual FastFixedSetFactory.FastFixedSet<E> SpawnEmptySet()
		{
			return new FastFixedSetFactory.FastFixedSet<E>(this);
		}

		private int GetDataLength()
		{
			return dataLength;
		}

		private VBStyleCollection<int[], E> GetInternalValuesCollection()
		{
			return colValuesInternal;
		}

		public class FastFixedSet<E> : IEnumerable<E>
		{
			private readonly FastFixedSetFactory<E> factory;

			private readonly VBStyleCollection<int[], E> colValuesInternal;

			private int[] data;

			private FastFixedSet(FastFixedSetFactory<E> factory)
			{
				this.factory = factory;
				this.colValuesInternal = factory.GetInternalValuesCollection();
				this.data = new int[factory.GetDataLength()];
			}

			public virtual FastFixedSetFactory.FastFixedSet<E> GetCopy()
			{
				FastFixedSetFactory.FastFixedSet<E> copy = new FastFixedSetFactory.FastFixedSet<E
					>(factory);
				int arrlength = data.Length;
				int[] cpdata = new int[arrlength];
				System.Array.Copy(data, 0, cpdata, 0, arrlength);
				copy.SetData(cpdata);
				return copy;
			}

			public virtual void SetAllElements()
			{
				int[] lastindex = colValuesInternal[colValuesInternal.Count - 1];
				for (int i = lastindex[0] - 1; i >= 0; i--)
				{
					data[i] = unchecked((int)(0xFFFFFFFF));
				}
				data[lastindex[0]] = lastindex[1] | (lastindex[1] - 1);
			}

			public virtual void Add(E element)
			{
				int[] index = colValuesInternal.GetWithKey(element);
				data[index[0]] |= index[1];
			}

			public virtual void AddAll(ICollection<E> set)
			{
				foreach (E element in set)
				{
					Add(element);
				}
			}

			public virtual void Remove(E element)
			{
				int[] index = colValuesInternal.GetWithKey(element);
				data[index[0]] &= ~index[1];
			}

			public virtual bool Contains(E element)
			{
				int[] index = colValuesInternal.GetWithKey(element);
				return (data[index[0]] & index[1]) != 0;
			}

			public virtual bool Contains(FastFixedSetFactory.FastFixedSet<E> set)
			{
				int[] extdata = set.GetData();
				int[] intdata = data;
				for (int i = intdata.Length - 1; i >= 0; i--)
				{
					if ((extdata[i] & ~intdata[i]) != 0)
					{
						return false;
					}
				}
				return true;
			}

			public virtual void Union(FastFixedSetFactory.FastFixedSet<E> set)
			{
				int[] extdata = set.GetData();
				int[] intdata = data;
				for (int i = intdata.Length - 1; i >= 0; i--)
				{
					intdata[i] |= extdata[i];
				}
			}

			public virtual void Intersection(FastFixedSetFactory.FastFixedSet<E> set)
			{
				int[] extdata = set.GetData();
				int[] intdata = data;
				for (int i = intdata.Length - 1; i >= 0; i--)
				{
					intdata[i] &= extdata[i];
				}
			}

			public virtual void Complement(FastFixedSetFactory.FastFixedSet<E> set)
			{
				int[] extdata = set.GetData();
				int[] intdata = data;
				for (int i = intdata.Length - 1; i >= 0; i--)
				{
					intdata[i] &= ~extdata[i];
				}
			}

			public override bool Equals(object o)
			{
				if (o == this)
				{
					return true;
				}
				if (!(Sharpen.Runtime.InstanceOf(o, typeof(FastFixedSetFactory.FastFixedSet<>))))
				{
					return false;
				}
				int[] extdata = ((FastFixedSetFactory.FastFixedSet)o).GetData();
				int[] intdata = data;
				for (int i = intdata.Length - 1; i >= 0; i--)
				{
					if (intdata[i] != extdata[i])
					{
						return false;
					}
				}
				return true;
			}

			public virtual bool IsEmpty()
			{
				int[] intdata = data;
				for (int i = intdata.Length - 1; i >= 0; i--)
				{
					if (intdata[i] != 0)
					{
						return false;
					}
				}
				return true;
			}

			public override IEnumerator<E> GetEnumerator()
			{
				return new FastFixedSetFactory.FastFixedSetIterator<E>(this);
			}

			public virtual HashSet<E> ToPlainSet()
			{
				return ToPlainCollection(new HashSet<E>());
			}

			private T ToPlainCollection<T>(T cl)
				where T : ICollection<E>
			{
				int[] intdata = data;
				for (int bindex = 0; bindex < intdata.Length; bindex++)
				{
					int block = intdata[bindex];
					if (block != 0)
					{
						int index = bindex << 5;
						// * 32
						for (int i = 31; i >= 0; i--)
						{
							if ((block & 1) != 0)
							{
								cl.Add(colValuesInternal.GetKey(index));
							}
							index++;
							block = (int)(((uint)block) >> 1);
						}
					}
				}
				return cl;
			}

			public override string ToString()
			{
				StringBuilder buffer = new StringBuilder("{");
				int[] intdata = data;
				bool first = true;
				for (int i = colValuesInternal.Count - 1; i >= 0; i--)
				{
					int[] index = colValuesInternal[i];
					if ((intdata[index[0]] & index[1]) != 0)
					{
						if (first)
						{
							first = false;
						}
						else
						{
							buffer.Append(",");
						}
						buffer.Append(colValuesInternal.GetKey(i));
					}
				}
				buffer.Append("}");
				return buffer.ToString();
			}

			private int[] GetData()
			{
				return data;
			}

			private void SetData(int[] data)
			{
				this.data = data;
			}

			public virtual FastFixedSetFactory<E> GetFactory()
			{
				return factory;
			}
		}

		public class FastFixedSetIterator<E> : IEnumerator<E>
		{
			private readonly VBStyleCollection<int[], E> colValuesInternal;

			private readonly int[] data;

			private readonly int size;

			private int pointer = -1;

			private int next_pointer = -1;

			private FastFixedSetIterator(FastFixedSetFactory.FastFixedSet<E> set)
			{
				colValuesInternal = set.GetFactory().GetInternalValuesCollection();
				data = set.GetData();
				size = colValuesInternal.Count;
			}

			private int GetNextIndex(int index)
			{
				index++;
				int ret = index;
				int bindex = index / 32;
				int dindex = index % 32;
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
								return ret;
							}
							block = (int)(((uint)block) >> 1);
							dindex++;
							ret++;
						}
					}
					else
					{
						ret += (32 - dindex);
					}
					dindex = 0;
					bindex++;
				}
				return -1;
			}

			public override bool MoveNext()
			{
				next_pointer = GetNextIndex(pointer);
				return (next_pointer >= 0);
			}

			public override E Current
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
					return pointer < size ? colValuesInternal.GetKey(pointer) : null;
				}
			}

			public override void Remove()
			{
				int[] index = colValuesInternal[pointer];
				data[index[0]] &= ~index[1];
			}
		}
	}
}
