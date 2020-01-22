// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.

using System;
using System.IO;
using ObjectWeb.Misc.Java.IO;
using ObjectWeb.Misc.Java.Nio;
using Sharpen;

namespace JetBrainsDecompiler.Util
{
	public class DataInputFullStream : DataInputStream, IDisposable
	{
		public DataInputFullStream(byte[] bytes)
			: base(new MemoryStream(bytes).ToInputStream())
		{
		}

		/// <exception cref="IOException"/>
		public virtual byte[] Read(int n)
		{
			return InterpreterUtil.ReadBytes(this, n);
		}

		/// <exception cref="IOException"/>
		public virtual void Discard(int n)
		{
			InterpreterUtil.DiscardBytes(this, n);
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}
	}
}
