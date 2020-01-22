// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Struct.Gen;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Gen.Generics
{
	public class GenericType
	{
		public const int Wildcard_Extends = 1;

		public const int Wildcard_Super = 2;

		public const int Wildcard_Unbound = 3;

		public const int Wildcard_No = 4;

		public readonly int type;

		public readonly int arrayDim;

		public readonly string value;

		private readonly List<GenericType> enclosingClasses = new List<GenericType>();

		private readonly List<GenericType> arguments = new List<GenericType>();

		private readonly List<int> wildcards = new List<int>();

		public GenericType(int type, int arrayDim, string value)
		{
			this.type = type;
			this.arrayDim = arrayDim;
			this.value = value;
		}

		private GenericType(GenericType other, int arrayDim)
			: this(other.type, arrayDim, other.value)
		{
			Sharpen.Collections.AddAll(enclosingClasses, other.enclosingClasses);
			Sharpen.Collections.AddAll(arguments, other.arguments);
			Sharpen.Collections.AddAll(wildcards, other.wildcards);
		}

		public GenericType(string signature)
		{
			int type = 0;
			int arrayDim = 0;
			string value = null;
			int index = 0;
			while (index < signature.Length)
			{
				switch (signature[index])
				{
					case '[':
					{
						arrayDim++;
						break;
					}

					case 'T':
					{
						type = ICodeConstants.Type_Genvar;
						value = Sharpen.Runtime.Substring(signature, index + 1, signature.Length - 1);
						goto loop_break;
					}

					case 'L':
					{
						type = ICodeConstants.Type_Object;
						signature = Sharpen.Runtime.Substring(signature, index + 1, signature.Length - 1);
						while (true)
						{
							string cl = GetNextClassSignature(signature);
							string name = cl;
							string args = null;
							int argStart = cl.IndexOf("<");
							if (argStart >= 0)
							{
								name = Sharpen.Runtime.Substring(cl, 0, argStart);
								args = Sharpen.Runtime.Substring(cl, argStart + 1, cl.Length - 1);
							}
							if (cl.Length < signature.Length)
							{
								signature = Sharpen.Runtime.Substring(signature, cl.Length + 1);
								// skip '.'
								GenericType type11 = new GenericType(ICodeConstants.Type_Object, 0, name);
								ParseArgumentsList(args, type11);
								enclosingClasses.Add(type11);
							}
							else
							{
								value = name;
								ParseArgumentsList(args, this);
								break;
							}
						}
						goto loop_break;
					}

					default:
					{
						value = Sharpen.Runtime.Substring(signature, index, index + 1);
						type = VarType.GetType(value[0]);
						break;
					}
				}
				index++;
loop_continue: ;
			}
loop_break: ;
			this.type = type;
			this.arrayDim = arrayDim;
			this.value = value;
		}

		private static string GetNextClassSignature(string value)
		{
			int counter = 0;
			int index = 0;
			while (index < value.Length)
			{
				switch (value[index])
				{
					case '<':
					{
						counter++;
						break;
					}

					case '>':
					{
						counter--;
						break;
					}

					case '.':
					{
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
			return Sharpen.Runtime.Substring(value, 0, index);
		}

		private static void ParseArgumentsList(string value, GenericType type)
		{
			if (value == null)
			{
				return;
			}
			while (value.Length > 0)
			{
				string typeStr = GetNextType(value);
				int len = typeStr.Length;
				int wildcard = Wildcard_No;
				switch (typeStr[0])
				{
					case '*':
					{
						wildcard = Wildcard_Unbound;
						break;
					}

					case '+':
					{
						wildcard = Wildcard_Extends;
						break;
					}

					case '-':
					{
						wildcard = Wildcard_Super;
						break;
					}
				}
				type.GetWildcards().Add(wildcard);
				if (wildcard != Wildcard_No)
				{
					typeStr = Sharpen.Runtime.Substring(typeStr, 1);
				}
				type.GetArguments().Add(typeStr.Length == 0 ? null : new GenericType(typeStr));
				value = Sharpen.Runtime.Substring(value, len);
			}
		}

		public static string GetNextType(string value)
		{
			int counter = 0;
			int index = 0;
			bool contMode = false;
			while (index < value.Length)
			{
				switch (value[index])
				{
					case '*':
					{
						if (!contMode)
						{
							goto loop_break;
						}
						break;
					}

					case 'L':
					case 'T':
					{
						if (!contMode)
						{
							contMode = true;
						}
						goto case '[';
					}

					case '[':
					case '+':
					case '-':
					{
						break;
					}

					default:
					{
						if (!contMode)
						{
							goto loop_break;
						}
						break;
					}

					case '<':
					{
						counter++;
						break;
					}

					case '>':
					{
						counter--;
						break;
					}

					case ';':
					{
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
			return Sharpen.Runtime.Substring(value, 0, index + 1);
		}

		public virtual GenericType DecreaseArrayDim()
		{
			System.Diagnostics.Debug.Assert(arrayDim > 0, "arrayDim > 0");
			return new GenericType(this, arrayDim - 1);
		}

		public virtual List<GenericType> GetArguments()
		{
			return arguments;
		}

		public virtual List<GenericType> GetEnclosingClasses()
		{
			return enclosingClasses;
		}

		public virtual List<int> GetWildcards()
		{
			return wildcards;
		}
	}
}
