// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Main.Collectors
{
	public class CounterContainer
	{
		public const int Statement_Counter = 0;

		public const int Expression_Counter = 1;

		public const int Var_Counter = 2;

		private readonly int[] values = new int[] { 1, 1, 1 };

		public virtual void SetCounter(int counter, int value)
		{
			values[counter] = value;
		}

		public virtual int GetCounter(int counter)
		{
			return values[counter];
		}

		public virtual int GetCounterAndIncrement(int counter)
		{
			return values[counter]++;
		}
	}
}
