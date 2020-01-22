// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using JetBrainsDecompiler.Struct.Gen;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class ClasspathHelper
	{
		private static readonly ConcurrentDictionary<string, MethodInfo> Method_Cache = (new ConcurrentDictionary<string, MethodInfo>());

		public static MethodInfo FindMethod(string classname, string methodName, MethodDescriptor
			 descriptor)
		{
			string targetClass = classname.Replace('/', '.');
			string methodSignature = BuildMethodSignature(targetClass + '.' + methodName, descriptor
				);
			MethodInfo method;
			if (Method_Cache.ContainsKey(methodSignature))
			{
				method = Method_Cache.GetOrNull(methodSignature);
			}
			else
			{
				method = FindMethodOnClasspath(targetClass, methodSignature);
				Sharpen.Collections.Put(Method_Cache, methodSignature, method);
			}
			return method;
		}

		private static MethodInfo FindMethodOnClasspath(string targetClass, string methodSignature
			)
		{
			/*
			try
			{
				// use bootstrap classloader to only provide access to JRE classes
				Type cls = new _ClassLoader_35(null).LoadClass(targetClass);
				foreach (MethodInfo mtd in cls.GetMethods())
				{
					// use contains() to ignore access modifiers and thrown exceptions
					if (mtd.ToString().Contains(methodSignature))
					{
						return mtd;
					}
				}
			}
			catch (Exception)
			{
			}
			*/
			// ignore
			return null;
		}

		private static string BuildMethodSignature(string name, MethodDescriptor md)
		{
			StringBuilder sb = new StringBuilder();
			AppendType(sb, md.ret);
			sb.Append(' ').Append(name).Append('(');
			foreach (VarType param in md.@params)
			{
				AppendType(sb, param);
				sb.Append(',');
			}
			if (sb[sb.Length - 1] == ',')
			{
				sb.Length = sb.Length - 1;
			}
			sb.Append(')');
			return sb.ToString();
		}

		private static void AppendType(StringBuilder sb, VarType type)
		{
			sb.Append(type.value.Replace('/', '.'));
			for (int i = 0; i < type.arrayDim; i++)
			{
				sb.Append("[]");
			}
		}
	}
}
