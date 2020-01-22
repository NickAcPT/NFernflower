// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using System.Text;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Struct.Gen;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class ConcatenationHelper
	{
		private const string builderClass = "java/lang/StringBuilder";

		private const string bufferClass = "java/lang/StringBuffer";

		private const string stringClass = "java/lang/String";

		private static readonly VarType builderType = new VarType(ICodeConstants.Type_Object
			, 0, "java/lang/StringBuilder");

		private static readonly VarType bufferType = new VarType(ICodeConstants.Type_Object
			, 0, "java/lang/StringBuffer");

		public static Exprent ContractStringConcat(Exprent expr)
		{
			Exprent exprTmp = null;
			VarType cltype = null;
			// first quick test
			if (expr.type == Exprent.Exprent_Invocation)
			{
				InvocationExprent iex = (InvocationExprent)expr;
				if ("toString".Equals(iex.GetName()))
				{
					if (builderClass.Equals(iex.GetClassname()))
					{
						cltype = builderType;
					}
					else if (bufferClass.Equals(iex.GetClassname()))
					{
						cltype = bufferType;
					}
					if (cltype != null)
					{
						exprTmp = iex.GetInstance();
					}
				}
				else if ("makeConcatWithConstants".Equals(iex.GetName()))
				{
					// java 9 style
					List<Exprent> parameters = ExtractParameters(iex.GetBootstrapArguments(), iex);
					if (parameters.Count >= 2)
					{
						return CreateConcatExprent(parameters, expr.bytecode);
					}
				}
			}
			if (exprTmp == null)
			{
				return expr;
			}
			// iterate in depth, collecting possible operands
			List<Exprent> lstOperands = new List<Exprent>();
			while (true)
			{
				int found = 0;
				switch (exprTmp.type)
				{
					case Exprent.Exprent_Invocation:
					{
						InvocationExprent iex = (InvocationExprent)exprTmp;
						if (IsAppendConcat(iex, cltype))
						{
							lstOperands.Add(0, iex.GetLstParameters()[0]);
							exprTmp = iex.GetInstance();
							found = 1;
						}
						break;
					}

					case Exprent.Exprent_New:
					{
						NewExprent nex = (NewExprent)exprTmp;
						if (IsNewConcat(nex, cltype))
						{
							VarType[] @params = nex.GetConstructor().GetDescriptor().@params;
							if (@params.Length == 1)
							{
								lstOperands.Add(0, nex.GetConstructor().GetLstParameters()[0]);
							}
							found = 2;
						}
						break;
					}
				}
				if (found == 0)
				{
					return expr;
				}
				else if (found == 2)
				{
					break;
				}
			}
			int first2str = 0;
			int index = 0;
			while (index < lstOperands.Count && index < 2)
			{
				if (lstOperands[index].GetExprType().Equals(VarType.Vartype_String))
				{
					first2str |= (index + 1);
				}
				index++;
			}
			if (first2str == 0)
			{
				lstOperands.Add(0, new ConstExprent(VarType.Vartype_String, string.Empty, expr.bytecode
					));
			}
			// remove redundant String.valueOf
			for (int i = 0; i < lstOperands.Count; i++)
			{
				Exprent rep = RemoveStringValueOf(lstOperands[i]);
				bool ok = (i > 1);
				if (!ok)
				{
					bool isstr = rep.GetExprType().Equals(VarType.Vartype_String);
					ok = isstr || first2str != i + 1;
					if (i == 0)
					{
						first2str &= 2;
					}
				}
				if (ok)
				{
					lstOperands[i] = rep;
				}
			}
			return CreateConcatExprent(lstOperands, expr.bytecode);
		}

		private static Exprent CreateConcatExprent(List<Exprent> lstOperands, HashSet<int
			> bytecode)
		{
			// build exprent to return
			Exprent func = lstOperands[0];
			for (int i = 1; i < lstOperands.Count; i++)
			{
				func = new FunctionExprent(FunctionExprent.Function_Str_Concat, Sharpen.Arrays.AsList
					(func, lstOperands[i]), bytecode);
			}
			return func;
		}

		private const char Tag_Arg = '\u0001';

		private const char Tag_Const = '\u0002';

		// See StringConcatFactory in jdk sources
		private static List<Exprent> ExtractParameters(List<PooledConstant> bootstrapArguments
			, InvocationExprent expr)
		{
			List<Exprent> parameters = expr.GetLstParameters();
			if (bootstrapArguments != null)
			{
				PooledConstant constant = bootstrapArguments[0];
				if (constant.type == ICodeConstants.CONSTANT_String)
				{
					string recipe = ((PrimitiveConstant)constant).GetString();
					List<Exprent> res = new List<Exprent>();
					StringBuilder acc = new StringBuilder();
					int parameterId = 0;
					for (int i = 0; i < recipe.Length; i++)
					{
						char c = recipe[i];
						if (c == Tag_Const || c == Tag_Arg)
						{
							// Detected a special tag, flush all accumulated characters
							// as a constant first:
							if (acc.Length > 0)
							{
								res.Add(new ConstExprent(VarType.Vartype_String, acc.ToString(), expr.bytecode));
								acc.Length = 0;
							}
							if (c == Tag_Const)
							{
							}
							// skip for now
							if (c == Tag_Arg)
							{
								res.Add(parameters[parameterId++]);
							}
						}
						else
						{
							// Not a special characters, this is a constant embedded into
							// the recipe itself.
							acc.Append(c);
						}
					}
					// Flush the remaining characters as constant:
					if (acc.Length > 0)
					{
						res.Add(new ConstExprent(VarType.Vartype_String, acc.ToString(), expr.bytecode));
					}
					return res;
				}
			}
			return new List<Exprent>(parameters);
		}

		private static bool IsAppendConcat(InvocationExprent expr, VarType cltype)
		{
			if ("append".Equals(expr.GetName()))
			{
				MethodDescriptor md = expr.GetDescriptor();
				if (md.ret.Equals(cltype) && md.@params.Length == 1)
				{
					VarType param = md.@params[0];
					switch (param.type)
					{
						case ICodeConstants.Type_Object:
						{
							if (!param.Equals(VarType.Vartype_String) && !param.Equals(VarType.Vartype_Object
								))
							{
								break;
							}
							goto case ICodeConstants.Type_Boolean;
						}

						case ICodeConstants.Type_Boolean:
						case ICodeConstants.Type_Char:
						case ICodeConstants.Type_Double:
						case ICodeConstants.Type_Float:
						case ICodeConstants.Type_Int:
						case ICodeConstants.Type_Long:
						{
							return true;
						}

						default:
						{
							break;
						}
					}
				}
			}
			return false;
		}

		private static bool IsNewConcat(NewExprent expr, VarType cltype)
		{
			if (expr.GetNewType().Equals(cltype))
			{
				VarType[] @params = expr.GetConstructor().GetDescriptor().@params;
				return @params.Length == 0 || @params.Length == 1 && @params[0].Equals(VarType.Vartype_String
					);
			}
			return false;
		}

		private static Exprent RemoveStringValueOf(Exprent exprent)
		{
			if (exprent.type == Exprent.Exprent_Invocation)
			{
				InvocationExprent iex = (InvocationExprent)exprent;
				if ("valueOf".Equals(iex.GetName()) && stringClass.Equals(iex.GetClassname()))
				{
					MethodDescriptor md = iex.GetDescriptor();
					if (md.@params.Length == 1)
					{
						VarType param = md.@params[0];
						switch (param.type)
						{
							case ICodeConstants.Type_Object:
							{
								if (!param.Equals(VarType.Vartype_Object))
								{
									break;
								}
								goto case ICodeConstants.Type_Boolean;
							}

							case ICodeConstants.Type_Boolean:
							case ICodeConstants.Type_Char:
							case ICodeConstants.Type_Double:
							case ICodeConstants.Type_Float:
							case ICodeConstants.Type_Int:
							case ICodeConstants.Type_Long:
							{
								return iex.GetLstParameters()[0];
							}
						}
					}
				}
			}
			return exprent;
		}
	}
}
