// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Sforms;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Vars
{
	public class VarTypeProcessor
	{
		public const int Var_Non_Final = 1;

		public const int Var_Explicit_Final = 2;

		public const int Var_Final = 3;

		private readonly StructMethod method;

		private readonly MethodDescriptor methodDescriptor;

		private readonly Dictionary<VarVersionPair, VarType> mapExprentMinTypes = new Dictionary
			<VarVersionPair, VarType>();

		private readonly Dictionary<VarVersionPair, VarType> mapExprentMaxTypes = new Dictionary
			<VarVersionPair, VarType>();

		private readonly Dictionary<VarVersionPair, int> mapFinalVars = new Dictionary<VarVersionPair
			, int>();

		public VarTypeProcessor(StructMethod mt, MethodDescriptor md)
		{
			method = mt;
			methodDescriptor = md;
		}

		public virtual void CalculateVarTypes(RootStatement root, DirectGraph graph)
		{
			SetInitVars(root);
			ResetExprentTypes(graph);
			//noinspection StatementWithEmptyBody
			while (!ProcessVarTypes(graph))
			{
			}
		}

		private void SetInitVars(RootStatement root)
		{
			bool thisVar = !method.HasModifier(ICodeConstants.Acc_Static);
			MethodDescriptor md = methodDescriptor;
			if (thisVar)
			{
				StructClass cl = (StructClass)DecompilerContext.GetProperty(DecompilerContext.Current_Class
					);
				VarType clType = new VarType(ICodeConstants.Type_Object, 0, cl.qualifiedName);
				Sharpen.Collections.Put(mapExprentMinTypes, new VarVersionPair(0, 1), clType);
				Sharpen.Collections.Put(mapExprentMaxTypes, new VarVersionPair(0, 1), clType);
			}
			int varIndex = 0;
			for (int i = 0; i < md.@params.Length; i++)
			{
				Sharpen.Collections.Put(mapExprentMinTypes, new VarVersionPair(varIndex + (thisVar
					 ? 1 : 0), 1), md.@params[i]);
				Sharpen.Collections.Put(mapExprentMaxTypes, new VarVersionPair(varIndex + (thisVar
					 ? 1 : 0), 1), md.@params[i]);
				varIndex += md.@params[i].stackSize;
			}
			// catch variables
			LinkedList<Statement> stack = new LinkedList<Statement>();
			stack.AddLast(root);
			while (!(stack.Count == 0))
			{
				Statement stat = Sharpen.Collections.RemoveFirst(stack);
				List<VarExprent> lstVars = null;
				if (stat.type == Statement.Type_Catchall)
				{
					lstVars = ((CatchAllStatement)stat).GetVars();
				}
				else if (stat.type == Statement.Type_Trycatch)
				{
					lstVars = ((CatchStatement)stat).GetVars();
				}
				if (lstVars != null)
				{
					foreach (VarExprent var in lstVars)
					{
						Sharpen.Collections.Put(mapExprentMinTypes, new VarVersionPair(var.GetIndex(), 1)
							, var.GetVarType());
						Sharpen.Collections.Put(mapExprentMaxTypes, new VarVersionPair(var.GetIndex(), 1)
							, var.GetVarType());
					}
				}
				Sharpen.Collections.AddAll(stack, stat.GetStats());
			}
		}

		private static void ResetExprentTypes(DirectGraph graph)
		{
			graph.IterateExprents((Exprent exprent) => 			{
					List<Exprent> lst = exprent.GetAllExprents(true);
					lst.Add(exprent);
					foreach (Exprent expr in lst)
					{
						if (expr.type == Exprent.Exprent_Var)
						{
							((VarExprent)expr).SetVarType(VarType.Vartype_Unknown);
						}
						else if (expr.type == Exprent.Exprent_Const)
						{
							ConstExprent constExpr = (ConstExprent)expr;
							if (constExpr.GetConstType().typeFamily == ICodeConstants.Type_Family_Integer)
							{
								constExpr.SetConstType(new ConstExprent(constExpr.GetIntValue(), constExpr.IsBoolPermitted
									(), null).GetConstType());
							}
						}
					}
					return 0;
				}
);
		}

		private bool ProcessVarTypes(DirectGraph graph)
		{
			return graph.IterateExprents((Exprent exprent) => CheckTypeExprent(exprent) ? 0 : 
				1);
		}

		private bool CheckTypeExprent(Exprent exprent)
		{
			foreach (Exprent expr in exprent.GetAllExprents())
			{
				if (!CheckTypeExprent(expr))
				{
					return false;
				}
			}
			if (exprent.type == Exprent.Exprent_Const)
			{
				ConstExprent constExpr = (ConstExprent)exprent;
				if (constExpr.GetConstType().typeFamily <= ICodeConstants.Type_Family_Integer)
				{
					// boolean or integer
					VarVersionPair pair = new VarVersionPair(constExpr.id, -1);
					if (!mapExprentMinTypes.ContainsKey(pair))
					{
						Sharpen.Collections.Put(mapExprentMinTypes, pair, constExpr.GetConstType());
					}
				}
			}
			CheckTypesResult result = exprent.CheckExprTypeBounds();
			bool res = true;
			if (result != null)
			{
				foreach (CheckTypesResult.ExprentTypePair entry in result.GetLstMaxTypeExprents())
				{
					if (entry.type.typeFamily != ICodeConstants.Type_Family_Object)
					{
						ChangeExprentType(entry.exprent, entry.type, 1);
					}
				}
				foreach (CheckTypesResult.ExprentTypePair entry in result.GetLstMinTypeExprents())
				{
					res &= ChangeExprentType(entry.exprent, entry.type, 0);
				}
			}
			return res;
		}

		private bool ChangeExprentType(Exprent exprent, VarType newType, int minMax)
		{
			bool res = true;
			switch (exprent.type)
			{
				case Exprent.Exprent_Const:
				{
					ConstExprent constExpr = (ConstExprent)exprent;
					VarType constType = constExpr.GetConstType();
					if (newType.typeFamily > ICodeConstants.Type_Family_Integer || constType.typeFamily
						 > ICodeConstants.Type_Family_Integer)
					{
						return true;
					}
					else if (newType.typeFamily == ICodeConstants.Type_Family_Integer)
					{
						VarType minInteger = new ConstExprent((int)constExpr.GetValue(), false, null).GetConstType
							();
						if (minInteger.IsStrictSuperset(newType))
						{
							newType = minInteger;
						}
					}
					goto case Exprent.Exprent_Var;
				}

				case Exprent.Exprent_Var:
				{
					VarVersionPair pair = null;
					if (exprent.type == Exprent.Exprent_Const)
					{
						pair = new VarVersionPair(((ConstExprent)exprent).id, -1);
					}
					else if (exprent.type == Exprent.Exprent_Var)
					{
						pair = new VarVersionPair((VarExprent)exprent);
					}
					if (minMax == 0)
					{
						// min
						VarType currentMinType = mapExprentMinTypes.GetOrNull(pair);
						VarType newMinType;
						if (currentMinType == null || newType.typeFamily > currentMinType.typeFamily)
						{
							newMinType = newType;
						}
						else if (newType.typeFamily < currentMinType.typeFamily)
						{
							return true;
						}
						else
						{
							newMinType = VarType.GetCommonSupertype(currentMinType, newType);
						}
						Sharpen.Collections.Put(mapExprentMinTypes, pair, newMinType);
						if (exprent.type == Exprent.Exprent_Const)
						{
							((ConstExprent)exprent).SetConstType(newMinType);
						}
						if (currentMinType != null && (newMinType.typeFamily > currentMinType.typeFamily 
							|| newMinType.IsStrictSuperset(currentMinType)))
						{
							return false;
						}
					}
					else
					{
						// max
						VarType currentMaxType = mapExprentMaxTypes.GetOrNull(pair);
						VarType newMaxType;
						if (currentMaxType == null || newType.typeFamily < currentMaxType.typeFamily)
						{
							newMaxType = newType;
						}
						else if (newType.typeFamily > currentMaxType.typeFamily)
						{
							return true;
						}
						else
						{
							newMaxType = VarType.GetCommonMinType(currentMaxType, newType);
						}
						Sharpen.Collections.Put(mapExprentMaxTypes, pair, newMaxType);
					}
					break;
				}

				case Exprent.Exprent_Assignment:
				{
					return ChangeExprentType(((AssignmentExprent)exprent).GetRight(), newType, minMax
						);
				}

				case Exprent.Exprent_Function:
				{
					FunctionExprent func = (FunctionExprent)exprent;
					switch (func.GetFuncType())
					{
						case FunctionExprent.Function_Iif:
						{
							// FIXME:
							res = ChangeExprentType(func.GetLstOperands()[1], newType, minMax) & ChangeExprentType
								(func.GetLstOperands()[2], newType, minMax);
							break;
						}

						case FunctionExprent.Function_And:
						case FunctionExprent.Function_Or:
						case FunctionExprent.Function_Xor:
						{
							res = ChangeExprentType(func.GetLstOperands()[0], newType, minMax) & ChangeExprentType
								(func.GetLstOperands()[1], newType, minMax);
							break;
						}
					}
					break;
				}
			}
			return res;
		}

		public virtual Dictionary<VarVersionPair, VarType> GetMapExprentMaxTypes()
		{
			return mapExprentMaxTypes;
		}

		public virtual Dictionary<VarVersionPair, VarType> GetMapExprentMinTypes()
		{
			return mapExprentMinTypes;
		}

		public virtual Dictionary<VarVersionPair, int> GetMapFinalVars()
		{
			return mapFinalVars;
		}

		public virtual void SetVarType(VarVersionPair pair, VarType type)
		{
			Sharpen.Collections.Put(mapExprentMinTypes, pair, type);
		}

		public virtual VarType GetVarType(VarVersionPair pair)
		{
			return mapExprentMinTypes.GetOrNull(pair);
		}
	}
}
