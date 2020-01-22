// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using System.IO;
using System.Text;
using Java.Util;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Modules.Renamer;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using NFernflower.Java.Util;
using ObjectWeb.Misc.Java.IO;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Consts
{
	public class ConstantPool : INewClassNameBuilder
	{
		public const int Field = 1;

		public const int Method = 2;

		private readonly List<PooledConstant> pool;

		private readonly PoolInterceptor interceptor;

		/// <exception cref="IOException"/>
		public ConstantPool(DataInputStream @in)
		{
			int size = @in.ReadUnsignedShort();
			pool = new List<PooledConstant>(size);
			BitSet[] nextPass = new BitSet[] { new BitSet(size), new BitSet(size), new BitSet
				(size) };
			// first dummy constant
			pool.Add(null);
			// first pass: read the elements
			for (int i = 1; i < size; i++)
			{
				byte tag = unchecked((byte)@in.ReadUnsignedByte());
				switch (tag)
				{
					case ICodeConstants.CONSTANT_Utf8:
					{
						pool.Add(new PrimitiveConstant(ICodeConstants.CONSTANT_Utf8, @in.ReadUTF()));
						break;
					}

					case ICodeConstants.CONSTANT_Integer:
					{
						pool.Add(new PrimitiveConstant(ICodeConstants.CONSTANT_Integer, (@in.ReadInt
							())));
						break;
					}

					case ICodeConstants.CONSTANT_Float:
					{
						pool.Add(new PrimitiveConstant(ICodeConstants.CONSTANT_Float, @in.ReadFloat()));
						break;
					}

					case ICodeConstants.CONSTANT_Long:
					{
						pool.Add(new PrimitiveConstant(ICodeConstants.CONSTANT_Long, @in.ReadLong()));
						pool.Add(null);
						i++;
						break;
					}

					case ICodeConstants.CONSTANT_Double:
					{
						pool.Add(new PrimitiveConstant(ICodeConstants.CONSTANT_Double, @in.ReadDouble()));
						pool.Add(null);
						i++;
						break;
					}

					case ICodeConstants.CONSTANT_Class:
					case ICodeConstants.CONSTANT_String:
					case ICodeConstants.CONSTANT_MethodType:
					{
						pool.Add(new PrimitiveConstant(tag, @in.ReadUnsignedShort()));
						nextPass[0].Set(i);
						break;
					}

					case ICodeConstants.CONSTANT_NameAndType:
					{
						pool.Add(new LinkConstant(tag, @in.ReadUnsignedShort(), @in.ReadUnsignedShort()));
						nextPass[0].Set(i);
						break;
					}

					case ICodeConstants.CONSTANT_Fieldref:
					case ICodeConstants.CONSTANT_Methodref:
					case ICodeConstants.CONSTANT_InterfaceMethodref:
					case ICodeConstants.CONSTANT_InvokeDynamic:
					{
						pool.Add(new LinkConstant(tag, @in.ReadUnsignedShort(), @in.ReadUnsignedShort()));
						nextPass[1].Set(i);
						break;
					}

					case ICodeConstants.CONSTANT_MethodHandle:
					{
						pool.Add(new LinkConstant(tag, @in.ReadUnsignedByte(), @in.ReadUnsignedShort()));
						nextPass[2].Set(i);
						break;
					}
				}
			}
			// resolving complex pool elements
			foreach (BitSet pass in nextPass)
			{
				int idx = 0;
				while ((idx = pass.NextSetBit(idx + 1)) > 0)
				{
					pool[idx].ResolveConstant(this);
				}
			}
			// get global constant pool interceptor instance, if any available
			interceptor = DecompilerContext.GetPoolInterceptor();
		}

		/// <exception cref="IOException"/>
		public static void SkipPool(DataInputFullStream @in)
		{
			int size = @in.ReadUnsignedShort();
			for (int i = 1; i < size; i++)
			{
				switch (@in.ReadUnsignedByte())
				{
					case ICodeConstants.CONSTANT_Utf8:
					{
						@in.ReadUTF();
						break;
					}

					case ICodeConstants.CONSTANT_Integer:
					case ICodeConstants.CONSTANT_Float:
					case ICodeConstants.CONSTANT_Fieldref:
					case ICodeConstants.CONSTANT_Methodref:
					case ICodeConstants.CONSTANT_InterfaceMethodref:
					case ICodeConstants.CONSTANT_NameAndType:
					case ICodeConstants.CONSTANT_InvokeDynamic:
					{
						@in.Discard(4);
						break;
					}

					case ICodeConstants.CONSTANT_Long:
					case ICodeConstants.CONSTANT_Double:
					{
						@in.Discard(8);
						i++;
						break;
					}

					case ICodeConstants.CONSTANT_Class:
					case ICodeConstants.CONSTANT_String:
					case ICodeConstants.CONSTANT_MethodType:
					{
						@in.Discard(2);
						break;
					}

					case ICodeConstants.CONSTANT_MethodHandle:
					{
						@in.Discard(3);
						break;
					}
				}
			}
		}

		public virtual string[] GetClassElement(int elementType, string className, int nameIndex
			, int descriptorIndex)
		{
			string elementName = ((PrimitiveConstant)GetConstant(nameIndex)).GetString();
			string descriptor = ((PrimitiveConstant)GetConstant(descriptorIndex)).GetString();
			if (interceptor != null)
			{
				string oldClassName = interceptor.GetOldName(className);
				if (oldClassName != null)
				{
					className = oldClassName;
				}
				string newElement = interceptor.GetName(className + ' ' + elementName + ' ' + descriptor
					);
				if (newElement != null)
				{
					elementName = newElement.Split(" ")[1];
				}
				string newDescriptor = BuildNewDescriptor(elementType == Field, descriptor);
				if (newDescriptor != null)
				{
					descriptor = newDescriptor;
				}
			}
			return new string[] { elementName, descriptor };
		}

		public virtual PooledConstant GetConstant(int index)
		{
			return pool[index];
		}

		public virtual PrimitiveConstant GetPrimitiveConstant(int index)
		{
			PrimitiveConstant cn = (PrimitiveConstant)GetConstant(index);
			if (cn != null && interceptor != null)
			{
				if (cn.type == ICodeConstants.CONSTANT_Class)
				{
					string newName = BuildNewClassname(cn.GetString());
					if (newName != null)
					{
						cn = new PrimitiveConstant(ICodeConstants.CONSTANT_Class, newName);
					}
				}
			}
			return cn;
		}

		public virtual LinkConstant GetLinkConstant(int index)
		{
			LinkConstant ln = (LinkConstant)GetConstant(index);
			if (ln != null && interceptor != null && (ln.type == ICodeConstants.CONSTANT_Fieldref
				 || ln.type == ICodeConstants.CONSTANT_Methodref || ln.type == ICodeConstants.CONSTANT_InterfaceMethodref
				))
			{
				string newClassName = BuildNewClassname(ln.classname);
				string newElement = interceptor.GetName(ln.classname + ' ' + ln.elementname + ' '
					 + ln.descriptor);
				string newDescriptor = BuildNewDescriptor(ln.type == ICodeConstants.CONSTANT_Fieldref
					, ln.descriptor);
				//TODO: Fix newElement being null caused by ln.classname being a leaf class instead of the class that declared the field/method.
				//See the comments of IDEA-137253 for more information.
				if (newClassName != null || newElement != null || newDescriptor != null)
				{
					string className = newClassName == null ? ln.classname : newClassName;
					string elementName = newElement == null ? ln.elementname : newElement.Split(" ")[
						1];
					string descriptor = newDescriptor == null ? ln.descriptor : newDescriptor;
					ln = new LinkConstant(ln.type, className, elementName, descriptor);
				}
			}
			return ln;
		}

		public virtual string BuildNewClassname(string className)
		{
			VarType vt = new VarType(className, true);
			string newName = interceptor.GetName(vt.value);
			if (newName != null)
			{
				StringBuilder buffer = new StringBuilder();
				if (vt.arrayDim > 0)
				{
					for (int i = 0; i < vt.arrayDim; i++)
					{
						buffer.Append('[');
					}
					buffer.Append('L').Append(newName).Append(';');
				}
				else
				{
					buffer.Append(newName);
				}
				return buffer.ToString();
			}
			return null;
		}

		private string BuildNewDescriptor(bool isField, string descriptor)
		{
			if (isField)
			{
				return FieldDescriptor.ParseDescriptor(descriptor).BuildNewDescriptor(this);
			}
			else
			{
				return MethodDescriptor.ParseDescriptor(descriptor).BuildNewDescriptor(this);
			}
		}
	}
}
