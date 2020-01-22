// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.Text;
using JetBrainsDecompiler.Code;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Gen
{
	public class MethodDescriptor
	{
		public readonly VarType[] @params;

		public readonly VarType ret;

		private MethodDescriptor(VarType[] @params, VarType ret)
		{
			this.@params = @params;
			this.ret = ret;
		}

		public static MethodDescriptor ParseDescriptor(string descriptor)
		{
			int parenth = descriptor.LastIndexOf(')');
			if (descriptor.Length < 2 || parenth < 0 || descriptor[0] != '(')
			{
				throw new ArgumentException("Invalid descriptor: " + descriptor);
			}
			VarType[] @params;
			if (parenth > 1)
			{
				string parameters = Sharpen.Runtime.Substring(descriptor, 1, parenth);
				List<string> lst = new List<string>();
				int indexFrom = -1;
				int ind;
				int len = parameters.Length;
				int index = 0;
				while (index < len)
				{
					switch (parameters[index])
					{
						case '[':
						{
							if (indexFrom < 0)
							{
								indexFrom = index;
							}
							break;
						}

						case 'L':
						{
							ind = parameters.IndexOf(";", index);
							lst.Add(Sharpen.Runtime.Substring(parameters, indexFrom < 0 ? index : indexFrom, 
								ind + 1));
							index = ind;
							indexFrom = -1;
							break;
						}

						default:
						{
							lst.Add(Sharpen.Runtime.Substring(parameters, indexFrom < 0 ? index : indexFrom, 
								index + 1));
							indexFrom = -1;
							break;
						}
					}
					index++;
				}
				@params = new VarType[lst.Count];
				for (int i = 0; i < lst.Count; i++)
				{
					@params[i] = new VarType(lst[i]);
				}
			}
			else
			{
				@params = VarType.Empty_Array;
			}
			VarType ret = new VarType(Sharpen.Runtime.Substring(descriptor, parenth + 1));
			return new MethodDescriptor(@params, ret);
		}

		public virtual string BuildNewDescriptor(INewClassNameBuilder builder)
		{
			bool updated = false;
			VarType[] newParams;
			if (@params.Length > 0)
			{
				newParams = new VarType[@params.Length];
				System.Array.Copy(@params, 0, newParams, 0, @params.Length);
				for (int i = 0; i < @params.Length; i++)
				{
					VarType substitute = BuildNewType(@params[i], builder);
					if (substitute != null)
					{
						newParams[i] = substitute;
						updated = true;
					}
				}
			}
			else
			{
				newParams = VarType.Empty_Array;
			}
			VarType newRet = ret;
			VarType substitute_1 = BuildNewType(ret, builder);
			if (substitute_1 != null)
			{
				newRet = substitute_1;
				updated = true;
			}
			if (updated)
			{
				StringBuilder res = new StringBuilder("(");
				foreach (VarType param in newParams)
				{
					res.Append(param);
				}
				res.Append(")").Append(newRet.ToString());
				return res.ToString();
			}
			return null;
		}

		private static VarType BuildNewType(VarType type, INewClassNameBuilder builder)
		{
			if (type.type == ICodeConstants.Type_Object)
			{
				string newClassName = builder.BuildNewClassname(type.value);
				if (newClassName != null)
				{
					return new VarType(type.type, type.arrayDim, newClassName);
				}
			}
			return null;
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is MethodDescriptor))
			{
				return false;
			}
			MethodDescriptor md = (MethodDescriptor)o;
			return ret.Equals(md.ret) && Sharpen.Arrays.Equals(@params, md.@params);
		}

		public override int GetHashCode()
		{
			int result = ret.GetHashCode();
			result = 31 * result + @params.Length;
			return result;
		}
	}
}
