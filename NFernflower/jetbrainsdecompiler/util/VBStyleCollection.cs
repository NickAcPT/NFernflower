// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sharpen;

namespace JetBrainsDecompiler.Util
{
	[System.Serializable]
	public class VBStyleCollection<E, K> : List<E>
	{
		private Dictionary<K, int> map = new Dictionary<K, int>();

		private List<K> lstKeys = new List<K>();

		public VBStyleCollection()
			: base()
		{
		}

		public VBStyleCollection(int initialCapacity)
			: base()
		{
			lstKeys = new List<K>(initialCapacity);
			map = new Dictionary<K, int>(initialCapacity);
		}

		public bool Add(E element)
		{
			lstKeys.Add(default);
			base.Add(element);
			return true;
		}

		public bool Remove(object element)
		{
			// TODO: error on void remove(E element)
			throw new Exception("not implemented!");
		}

		public bool AddAll(ICollection<E> c)
		{
			for (int i = c.Count - 1; i >= 0; i--)
			{
				lstKeys.Add(default);
			}

			foreach (var i in c)
			{
				base.Add(i);
			}

			return false;
		}

		public virtual void AddAllWithKey(ICollection<E> elements, ICollection<K> keys)
		{
			int index = base.Count;
			foreach (K key in keys)
			{
				Sharpen.Collections.Put(map, key, index++);
			}
			foreach (var i in elements)
			{
				base.Add(i);
			}
			Sharpen.Collections.AddAll(lstKeys, keys);
		}

		public virtual void AddWithKey(E element, K key)
		{
			Sharpen.Collections.Put(map, key, base.Count);
			base.Add(element);
			lstKeys.Add(key);
		}

		// TODO: speed up the method
		public virtual E PutWithKey(E element, K key)
		{
			int? index = map.GetOrNullable(key);
			if (index == null)
			{
				AddWithKey(element, key);
			}
			else
			{
				return base[(index.Value)] = (element);
			}
			return default;
		}

		public void Add(int index, E element)
		{
			AddToListIndex(index, 1);
			lstKeys.Add(index, default);
			base.Insert(index, element);
		}

		public virtual void AddWithKeyAndIndex(int index, E element, K key)
		{
			AddToListIndex(index, 1);
			Sharpen.Collections.Put(map, key, index);
			base.Insert(index, element);
			lstKeys.Add(index, key);
		}

		public virtual void RemoveWithKey(K key)
		{
			int? index = map.GetOrNullable(key);
			AddToListIndex(index.Value + 1, -1);
			base.RemoveAt(index.Value);
			lstKeys.RemoveAtReturningValue(index.Value);
			Sharpen.Collections.Remove(map, key);
		}

		public E RemoveAtReturningValue(int index)
		{
			AddToListIndex(index + 1, -1);
			K obj = lstKeys[index];
			if (obj != null)
			{
				Sharpen.Collections.Remove(map, obj);
			}
			lstKeys.RemoveAtReturningValue(index);
			var result = base[index];
			base.RemoveAt(index);
			return result;
		}

		public virtual E GetWithKey(K key)
		{
			int? index = map.GetOrNullable(key);
			if (index == null || index < 0)
			{
				return default;
			}
			return base[index.Value];
		}

		public virtual int GetIndexByKey(K key)
		{
			return map.GetOrNullable(key) ?? -1;
		}

		public virtual E GetLast()
		{
			return base[base.Count - 1];
		}

		public virtual bool ContainsKey(K key)
		{
			return map.ContainsKey(key);
		}

		public void Clear()
		{
			map.Clear();
			lstKeys.Clear();
			base.Clear();
		}

		public bool Contains(E item)
		{
			throw new NotImplementedException();
		}

		public void CopyTo(E[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public bool Remove(E item)
		{
			throw new NotImplementedException();
		}

		public int Count { get; }
		public bool IsReadOnly { get; }

		public object Clone()
		{
			VBStyleCollection<E, K> c = new VBStyleCollection<E, K>();
			Sharpen.Collections.AddAll(c, new List<E>(this));
			c.SetMap(new Dictionary<K, int>(map));
			c.SetLstKeys(new List<K>(lstKeys));
			return c;
		}

		public virtual void SetMap(Dictionary<K, int> map)
		{
			this.map = map;
		}

		public virtual K GetKey(int index)
		{
			return lstKeys[index];
		}

		public virtual List<K> GetLstKeys()
		{
			return lstKeys;
		}

		public virtual void SetLstKeys(List<K> lstKeys)
		{
			this.lstKeys = lstKeys;
		}

		private void AddToListIndex(int index, int diff)
		{
			for (int i = lstKeys.Count - 1; i >= index; i--)
			{
				K obj = lstKeys[i];
				if (obj != null)
				{
					Sharpen.Collections.Put(map, obj, i + diff);
				}
			}
		}

		public IEnumerator<E> GetEnumerator()
		{
			throw new NotImplementedException();
		}

		public int IndexOf(E item)
		{
			throw new NotImplementedException();
		}

		public void Insert(int index, E item)
		{
			throw new NotImplementedException();
		}

		public void RemoveAt(int index)
		{
			throw new NotImplementedException();
		}

		public E this[int index]
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}
	}
}
