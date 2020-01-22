// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Sharpen;

namespace JetBrainsDecompiler.Util
{
	[System.Serializable]
	public class ListStack<T> : List<T>
	{
		protected internal int pointer = 0;

		public ListStack()
			: base()
		{
		}

		public ListStack(List<T> list)
			: base(list)
		{
		}

		public override object Clone()
		{
			ListStack<T> copy = new ListStack<T>(this);
			copy.pointer = this.pointer;
			return copy;
		}

		public virtual void Push(T item)
		{
			this.Add(item);
			pointer++;
		}

		public virtual T Pop()
		{
			pointer--;
			T o = this[pointer];
			this.RemoveAtReturningValue(pointer);
			return o;
		}

		public virtual T Pop(int count)
		{
			T o = null;
			for (int i = count; i > 0; i--)
			{
				o = this.Pop();
			}
			return o;
		}

		public virtual void RemoveMultiple(int count)
		{
			while (count > 0)
			{
				pointer--;
				this.RemoveAtReturningValue(pointer);
				count--;
			}
		}

		public virtual int GetPointer()
		{
			return pointer;
		}

		public virtual T GetByOffset(int offset)
		{
			return this[pointer + offset];
		}

		public virtual void InsertByOffset(int offset, T item)
		{
			this.Add(pointer + offset, item);
			pointer++;
		}

		public override void Clear()
		{
			base.Clear();
			pointer = 0;
		}
	}
}
