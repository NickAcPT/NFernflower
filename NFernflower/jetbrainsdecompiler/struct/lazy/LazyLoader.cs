// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.IO;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Lazy
{
	public class LazyLoader
	{
		private readonly IDictionary<string, LazyLoader.Link> mapClassLinks = new Dictionary
			<string, LazyLoader.Link>();

		private readonly IIBytecodeProvider provider;

		public LazyLoader(IIBytecodeProvider provider)
		{
			this.provider = provider;
		}

		public virtual void AddClassLink(string classname, LazyLoader.Link link)
		{
			Sharpen.Collections.Put(mapClassLinks, classname, link);
		}

		public virtual void RemoveClassLink(string classname)
		{
			Sharpen.Collections.Remove(mapClassLinks, classname);
		}

		public virtual LazyLoader.Link GetClassLink(string classname)
		{
			return mapClassLinks.GetOrNull(classname);
		}

		public virtual ConstantPool LoadPool(string classname)
		{
			try
			{
				using (DataInputFullStream @in = GetClassStream(classname))
				{
					if (@in != null)
					{
						@in.Discard(8);
						return new ConstantPool(@in);
					}
					return null;
				}
			}
			catch (IOException ex)
			{
				throw new Exception(ex);
			}
		}

		public virtual byte[] LoadBytecode(StructMethod mt, int codeFullLength)
		{
			string className = mt.GetClassStruct().qualifiedName;
			try
			{
				using (DataInputFullStream @in = GetClassStream(className))
				{
					if (@in != null)
					{
						@in.Discard(8);
						ConstantPool pool = mt.GetClassStruct().GetPool();
						if (pool == null)
						{
							pool = new ConstantPool(@in);
						}
						else
						{
							ConstantPool.SkipPool(@in);
						}
						@in.Discard(6);
						// interfaces
						@in.Discard(@in.ReadUnsignedShort() * 2);
						// fields
						int size = @in.ReadUnsignedShort();
						for (int i = 0; i < size; i++)
						{
							@in.Discard(6);
							SkipAttributes(@in);
						}
						// methods
						size = @in.ReadUnsignedShort();
						for (int i = 0; i < size; i++)
						{
							@in.Discard(2);
							int nameIndex = @in.ReadUnsignedShort();
							int descriptorIndex = @in.ReadUnsignedShort();
							string[] values = pool.GetClassElement(ConstantPool.Method, className, nameIndex, 
								descriptorIndex);
							if (!mt.GetName().Equals(values[0]) || !mt.GetDescriptor().Equals(values[1]))
							{
								SkipAttributes(@in);
								continue;
							}
							int attrSize = @in.ReadUnsignedShort();
							for (int j = 0; j < attrSize; j++)
							{
								int attrNameIndex = @in.ReadUnsignedShort();
								string attrName = pool.GetPrimitiveConstant(attrNameIndex).GetString();
								if (!StructGeneralAttribute.Attribute_Code.GetName().Equals(attrName))
								{
									@in.Discard(@in.ReadInt());
									continue;
								}
								@in.Discard(12);
								return @in.Read(codeFullLength);
							}
							break;
						}
					}
					return null;
				}
			}
			catch (IOException ex)
			{
				throw new Exception(ex);
			}
		}

		/// <exception cref="System.IO.IOException"/>
		public virtual DataInputFullStream GetClassStream(string externalPath, string internalPath
			)
		{
			byte[] bytes = provider.GetBytecode(externalPath, internalPath);
			return new DataInputFullStream(bytes);
		}

		/// <exception cref="System.IO.IOException"/>
		public virtual DataInputFullStream GetClassStream(string qualifiedClassName)
		{
			LazyLoader.Link link = mapClassLinks.GetOrNull(qualifiedClassName);
			return link == null ? null : GetClassStream(link.externalPath, link.internalPath);
		}

		/// <exception cref="System.IO.IOException"/>
		public static void SkipAttributes(DataInputFullStream @in)
		{
			int length = @in.ReadUnsignedShort();
			for (int i = 0; i < length; i++)
			{
				@in.Discard(2);
				@in.Discard(@in.ReadInt());
			}
		}

		public class Link
		{
			public readonly string externalPath;

			public readonly string internalPath;

			public Link(string externalPath, string internalPath)
			{
				this.externalPath = externalPath;
				this.internalPath = internalPath;
			}
		}
	}
}
