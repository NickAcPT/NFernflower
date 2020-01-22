// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.Text;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Gen.Generics
{
	public class GenericMain
	{
		private static readonly string[] typeNames = new string[] { "byte", "char", "double"
			, "float", "int", "long", "short", "boolean" };

		public static GenericClassDescriptor ParseClassSignature(string signature)
		{
			string original = signature;
			try
			{
				GenericClassDescriptor descriptor = new GenericClassDescriptor();
				signature = ParseFormalParameters(signature, descriptor.fparameters, descriptor.fbounds
					);
				string superCl = GenericType.GetNextType(signature);
				descriptor.superclass = new GenericType(superCl);
				signature = Sharpen.Runtime.Substring(signature, superCl.Length);
				while (signature.Length > 0)
				{
					string superIf = GenericType.GetNextType(signature);
					descriptor.superinterfaces.Add(new GenericType(superIf));
					signature = Sharpen.Runtime.Substring(signature, superIf.Length);
				}
				return descriptor;
			}
			catch (Exception)
			{
				DecompilerContext.GetLogger().WriteMessage("Invalid signature: " + original, IFernflowerLogger.Severity
					.Warn);
				return null;
			}
		}

		public static GenericFieldDescriptor ParseFieldSignature(string signature)
		{
			try
			{
				return new GenericFieldDescriptor(new GenericType(signature));
			}
			catch (Exception)
			{
				DecompilerContext.GetLogger().WriteMessage("Invalid signature: " + signature, IFernflowerLogger.Severity
					.Warn);
				return null;
			}
		}

		public static GenericMethodDescriptor ParseMethodSignature(string signature)
		{
			string original = signature;
			try
			{
				List<string> typeParameters = new List<string>();
				List<List<GenericType>> typeParameterBounds = new List<List<GenericType>>();
				signature = ParseFormalParameters(signature, typeParameters, typeParameterBounds);
				int to = signature.IndexOf(")");
				string parameters = Sharpen.Runtime.Substring(signature, 1, to);
				signature = Sharpen.Runtime.Substring(signature, to + 1);
				List<GenericType> parameterTypes = new List<GenericType>();
				while (parameters.Length > 0)
				{
					string par = GenericType.GetNextType(parameters);
					parameterTypes.Add(new GenericType(par));
					parameters = Sharpen.Runtime.Substring(parameters, par.Length);
				}
				string ret = GenericType.GetNextType(signature);
				GenericType returnType = new GenericType(ret);
				signature = Sharpen.Runtime.Substring(signature, ret.Length);
				List<GenericType> exceptionTypes = new List<GenericType>();
				if (signature.Length > 0)
				{
					string[] exceptions = signature.Split("\\^");
					for (int i = 1; i < exceptions.Length; i++)
					{
						exceptionTypes.Add(new GenericType(exceptions[i]));
					}
				}
				return new GenericMethodDescriptor(typeParameters, typeParameterBounds, parameterTypes
					, returnType, exceptionTypes);
			}
			catch (Exception)
			{
				DecompilerContext.GetLogger().WriteMessage("Invalid signature: " + original, IFernflowerLogger.Severity
					.Warn);
				return null;
			}
		}

		private static string ParseFormalParameters<_T0, _T0>(string signature, List<_T0
			> parameters, List<_T0> bounds)
		{
			if (signature[0] != '<')
			{
				return signature;
			}
			int counter = 1;
			int index = 1;
			while (index < signature.Length)
			{
				switch (signature[index])
				{
					case '<':
					{
						counter++;
						break;
					}

					case '>':
					{
						counter--;
						if (counter == 0)
						{
							goto loop_break;
						}
						break;
					}
				}
				index++;
loop_continue: ;
			}
loop_break: ;
			string value = Sharpen.Runtime.Substring(signature, 1, index);
			signature = Sharpen.Runtime.Substring(signature, index + 1);
			while (value.Length > 0)
			{
				int to = value.IndexOf(":");
				string param = Sharpen.Runtime.Substring(value, 0, to);
				value = Sharpen.Runtime.Substring(value, to + 1);
				List<GenericType> lstBounds = new List<GenericType>();
				while (true)
				{
					if (value[0] == ':')
					{
						// empty superclass, skip
						value = Sharpen.Runtime.Substring(value, 1);
					}
					string bound = GenericType.GetNextType(value);
					lstBounds.Add(new GenericType(bound));
					value = Sharpen.Runtime.Substring(value, bound.Length);
					if (value.Length == 0 || value[0] != ':')
					{
						break;
					}
					else
					{
						value = Sharpen.Runtime.Substring(value, 1);
					}
				}
				parameters.Add(param);
				bounds.Add(lstBounds);
			}
			return signature;
		}

		public static string GetGenericCastTypeName(GenericType type)
		{
			StringBuilder s = new StringBuilder(GetTypeName(type));
			TextUtil.Append(s, "[]", type.arrayDim);
			return s.ToString();
		}

		private static string GetTypeName(GenericType type)
		{
			int tp = type.type;
			if (tp <= ICodeConstants.Type_Boolean)
			{
				return typeNames[tp];
			}
			else if (tp == ICodeConstants.Type_Void)
			{
				return "void";
			}
			else if (tp == ICodeConstants.Type_Genvar)
			{
				return type.value;
			}
			else if (tp == ICodeConstants.Type_Object)
			{
				StringBuilder buffer = new StringBuilder();
				AppendClassName(type, buffer);
				return buffer.ToString();
			}
			throw new Exception("Invalid type: " + type);
		}

		private static void AppendClassName(GenericType type, StringBuilder buffer)
		{
			List<GenericType> enclosingClasses = type.GetEnclosingClasses();
			if ((enclosingClasses.Count == 0))
			{
				string name = type.value.Replace('/', '.');
				buffer.Append(DecompilerContext.GetImportCollector().GetShortName(name));
			}
			else
			{
				foreach (GenericType tp in enclosingClasses)
				{
					if (buffer.Length == 0)
					{
						buffer.Append(DecompilerContext.GetImportCollector().GetShortName(tp.value.Replace
							('/', '.')));
					}
					else
					{
						buffer.Append(tp.value);
					}
					AppendTypeArguments(tp, buffer);
					buffer.Append('.');
				}
				buffer.Append(type.value);
			}
			AppendTypeArguments(type, buffer);
		}

		private static void AppendTypeArguments(GenericType type, StringBuilder buffer)
		{
			if (!(type.GetArguments().Count == 0))
			{
				buffer.Append('<');
				for (int i = 0; i < type.GetArguments().Count; i++)
				{
					if (i > 0)
					{
						buffer.Append(", ");
					}
					int wildcard = type.GetWildcards()[i];
					switch (wildcard)
					{
						case GenericType.Wildcard_Unbound:
						{
							buffer.Append('?');
							break;
						}

						case GenericType.Wildcard_Extends:
						{
							buffer.Append("? extends ");
							break;
						}

						case GenericType.Wildcard_Super:
						{
							buffer.Append("? super ");
							break;
						}
					}
					GenericType genPar = type.GetArguments()[i];
					if (genPar != null)
					{
						buffer.Append(GetGenericCastTypeName(genPar));
					}
				}
				buffer.Append(">");
			}
		}
	}
}
