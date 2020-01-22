// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
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
			: base(initialCapacity)
		{
			lstKeys = new List<K>(initialCapacity);
			map = new Dictionary<K, int>(initialCapacity);
		}

		public override bool Add(E element)
		{
			lstKeys.Add(null);
			base.Add(element);
			return true;
		}

		public override bool Remove(object element)
		{
			// TODO: error on void remove(E element)
			throw new Exception("not implemented!");
		}

		public override bool AddAll<_T0>(ICollection<_T0> c)
		{
			for (int i = c.Count - 1; i >= 0; i--)
			{
				lstKeys.Add(null);
			}
			return base.Sharpen.Collections.AddAll(c);
		}

		public virtual void AddAllWithKey(ICollection<E> elements, ICollection<K> keys)
		{
			int index = base.Count;
			foreach (K key in keys)
			{
				Sharpen.Collections.Put(map, key, index++);
			}
			base.Sharpen.Collections.AddAll(elements);
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
				return base.Set(index.Value, element);
			}
			return null;
		}

		public override void Add(int index, E element)
		{
			AddToListIndex(index, 1);
			lstKeys.Add(index, null);
			base.Add(index, element);
		}

		public virtual void AddWithKeyAndIndex(int index, E element, K key)
		{
			AddToListIndex(index, 1);
			Sharpen.Collections.Put(map, key, index);
			base.Add(index, element);
			lstKeys.Add(index, key);
		}

		public virtual void RemoveWithKey(K key)
		{
			int? index = map.GetOrNullable(key);
			AddToListIndex(index.Value + 1, -1);
			base.RemoveAtReturningValue(index.Value);
			lstKeys.RemoveAtReturningValue(index.Value);
			Sharpen.Collections.Remove(map, key);
		}

		public override E RemoveAtReturningValue(int index)
		{
			AddToListIndex(index + 1, -1);
			K obj = lstKeys[index];
			if (obj != null)
			{
				Sharpen.Collections.Remove(map, obj);
			}
			lstKeys.RemoveAtReturningValue(index);
			return base.RemoveAtReturningValue(index);
		}

		public virtual E GetWithKey(K key)
		{
			int? index = map.GetOrNullable(key);
			if (index == null)
			{
				return null;
			}
			return base.Get;
		}

		public virtual int GetIndexByKey(K key)
		{
			return map.GetOrNullable(key);
		}

		public virtual E GetLast()
		{
			return base.Get;
		}

		public virtual bool ContainsKey(K key)
		{
			return map.ContainsKey(key);
		}

		public override void Clear()
		{
			map.Clear();
			lstKeys.Clear();
			base.Clear();
		}

		public override object Clone()
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
	}
}
