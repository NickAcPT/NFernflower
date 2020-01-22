// Copyright 2000-2019 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Struct.Lazy;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct
{
	public class StructClass : StructMember
	{
		public readonly string qualifiedName;

		public readonly PrimitiveConstant superClass;

		private readonly bool own;

		private readonly LazyLoader loader;

		private readonly int minorVersion;

		private readonly int majorVersion;

		private readonly int[] interfaces;

		private readonly string[] interfaceNames;

		private readonly VBStyleCollection<StructField, string> fields;

		private readonly VBStyleCollection<StructMethod, string> methods;

		private ConstantPool pool;

		/// <exception cref="System.IO.IOException"/>
		public StructClass(byte[] bytes, bool own, LazyLoader loader)
			: this(new DataInputFullStream(bytes), own, loader)
		{
		}

		/// <exception cref="System.IO.IOException"/>
		public StructClass(DataInputFullStream @in, bool own, LazyLoader loader)
		{
			/*
			class_file {
			u4 magic;
			u2 minor_version;
			u2 major_version;
			u2 constant_pool_count;
			cp_info constant_pool[constant_pool_count-1];
			u2 access_flags;
			u2 this_class;
			u2 super_class;
			u2 interfaces_count;
			u2 interfaces[interfaces_count];
			u2 fields_count;
			field_info fields[fields_count];
			u2 methods_count;
			method_info methods[methods_count];
			u2 attributes_count;
			attribute_info attributes[attributes_count];
			}
			*/
			this.own = own;
			this.loader = loader;
			@in.Discard(4);
			minorVersion = @in.ReadUnsignedShort();
			majorVersion = @in.ReadUnsignedShort();
			pool = new ConstantPool(@in);
			accessFlags = @in.ReadUnsignedShort();
			int thisClassIdx = @in.ReadUnsignedShort();
			int superClassIdx = @in.ReadUnsignedShort();
			qualifiedName = pool.GetPrimitiveConstant(thisClassIdx).GetString();
			superClass = pool.GetPrimitiveConstant(superClassIdx);
			// interfaces
			int length = @in.ReadUnsignedShort();
			interfaces = new int[length];
			interfaceNames = new string[length];
			for (int i = 0; i < length; i++)
			{
				interfaces[i] = @in.ReadUnsignedShort();
				interfaceNames[i] = pool.GetPrimitiveConstant(interfaces[i]).GetString();
			}
			// fields
			length = @in.ReadUnsignedShort();
			fields = new VBStyleCollection<StructField, string>(length);
			for (int i = 0; i < length; i++)
			{
				StructField field = new StructField(@in, this);
				fields.AddWithKey(field, InterpreterUtil.MakeUniqueKey(field.GetName(), field.GetDescriptor
					()));
			}
			// methods
			length = @in.ReadUnsignedShort();
			methods = new VBStyleCollection<StructMethod, string>(length);
			for (int i = 0; i < length; i++)
			{
				StructMethod method = new StructMethod(@in, this);
				methods.AddWithKey(method, InterpreterUtil.MakeUniqueKey(method.GetName(), method
					.GetDescriptor()));
			}
			// attributes
			attributes = ReadAttributes(@in, pool);
			ReleaseResources();
		}

		public virtual bool HasField(string name, string descriptor)
		{
			return GetField(name, descriptor) != null;
		}

		public virtual StructField GetField(string name, string descriptor)
		{
			return fields.GetWithKey(InterpreterUtil.MakeUniqueKey(name, descriptor));
		}

		public virtual StructMethod GetMethod(string key)
		{
			return methods.GetWithKey(key);
		}

		public virtual StructMethod GetMethod(string name, string descriptor)
		{
			return methods.GetWithKey(InterpreterUtil.MakeUniqueKey(name, descriptor));
		}

		public virtual string GetInterface(int i)
		{
			return interfaceNames[i];
		}

		public virtual void ReleaseResources()
		{
			if (loader != null)
			{
				pool = null;
			}
		}

		public virtual ConstantPool GetPool()
		{
			if (pool == null && loader != null)
			{
				pool = loader.LoadPool(qualifiedName);
			}
			return pool;
		}

		public virtual int[] GetInterfaces()
		{
			return interfaces;
		}

		public virtual string[] GetInterfaceNames()
		{
			return interfaceNames;
		}

		public virtual VBStyleCollection<StructMethod, string> GetMethods()
		{
			return methods;
		}

		public virtual VBStyleCollection<StructField, string> GetFields()
		{
			return fields;
		}

		public virtual bool IsOwn()
		{
			return own;
		}

		public virtual LazyLoader GetLoader()
		{
			return loader;
		}

		public virtual bool IsVersionGE_1_5()
		{
			return (majorVersion > ICodeConstants.Bytecode_Java_Le_4 || (majorVersion == ICodeConstants
				.Bytecode_Java_Le_4 && minorVersion > 0));
		}

		// FIXME: check second condition
		public virtual bool IsVersionGE_1_7()
		{
			return (majorVersion >= ICodeConstants.Bytecode_Java_7);
		}

		public virtual int GetBytecodeVersion()
		{
			return majorVersion < ICodeConstants.Bytecode_Java_Le_4 ? ICodeConstants.Bytecode_Java_Le_4
				 : majorVersion;
		}

		public override string ToString()
		{
			return qualifiedName;
		}
	}
}
