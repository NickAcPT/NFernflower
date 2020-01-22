// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Java.IO;
using Sharpen;

namespace JetBrainsDecompiler.Util
{
	public class DataInputFullStream : DataInputStream
	{
		public DataInputFullStream(byte[] bytes)
			: base(new ByteArrayInputStream(bytes))
		{
		}

		/// <exception cref="System.IO.IOException"/>
		public virtual byte[] Read(int n)
		{
			return InterpreterUtil.ReadBytes(this, n);
		}

		/// <exception cref="System.IO.IOException"/>
		public virtual void Discard(int n)
		{
			InterpreterUtil.DiscardBytes(this, n);
		}
	}
}
