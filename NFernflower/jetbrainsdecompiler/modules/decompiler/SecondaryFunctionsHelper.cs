// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct.Gen;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class SecondaryFunctionsHelper
	{
		private static readonly int[] funcsnot = new int[] { FunctionExprent.Function_Ne, 
			FunctionExprent.Function_Eq, FunctionExprent.Function_Ge, FunctionExprent.Function_Lt
			, FunctionExprent.Function_Le, FunctionExprent.Function_Gt, FunctionExprent.Function_Cor
			, FunctionExprent.Function_Cadd };

		private static readonly Dictionary<int?, int?[]> mapNumComparisons = new Dictionary<int?, int?[]>();

		static SecondaryFunctionsHelper()
		{
			Sharpen.Collections.Put(mapNumComparisons, FunctionExprent.Function_Eq, new int?[]
				 { FunctionExprent.Function_Lt, FunctionExprent.Function_Eq, FunctionExprent.Function_Gt
				 });
			Sharpen.Collections.Put(mapNumComparisons, FunctionExprent.Function_Ne, new int?[]
				 { FunctionExprent.Function_Ge, FunctionExprent.Function_Ne, FunctionExprent.Function_Le
				 });
			Sharpen.Collections.Put(mapNumComparisons, FunctionExprent.Function_Gt, new int?[]
				 { FunctionExprent.Function_Ge, FunctionExprent.Function_Gt, null });
			Sharpen.Collections.Put(mapNumComparisons, FunctionExprent.Function_Ge, new int?[]
				 { null, FunctionExprent.Function_Ge, FunctionExprent.Function_Gt });
			Sharpen.Collections.Put(mapNumComparisons, FunctionExprent.Function_Lt, new int?[]
				 { null, FunctionExprent.Function_Lt, FunctionExprent.Function_Le });
			Sharpen.Collections.Put(mapNumComparisons, FunctionExprent.Function_Le, new int?[]
				 { FunctionExprent.Function_Lt, FunctionExprent.Function_Le, null });
		}

		public static bool IdentifySecondaryFunctions(Statement stat, VarProcessor varProc
			)
		{
			if (stat.GetExprents() == null)
			{
				// if(){;}else{...} -> if(!){...}
				if (stat.type == Statement.Type_If)
				{
					IfStatement ifelsestat = (IfStatement)stat;
					Statement ifstat = ifelsestat.GetIfstat();
					if (ifelsestat.iftype == IfStatement.Iftype_Ifelse && ifstat.GetExprents() != null
						 && (ifstat.GetExprents().Count == 0) && ((ifstat.GetAllSuccessorEdges().Count == 0)
						 || !ifstat.GetAllSuccessorEdges()[0].@explicit))
					{
						// move else to the if position
						ifelsestat.GetStats().RemoveWithKey(ifstat.id);
						ifelsestat.iftype = IfStatement.Iftype_If;
						ifelsestat.SetIfstat(ifelsestat.GetElsestat());
						ifelsestat.SetElsestat(null);
						if ((ifelsestat.GetAllSuccessorEdges().Count == 0) && !(ifstat.GetAllSuccessorEdges
							().Count == 0))
						{
							StatEdge endedge = ifstat.GetAllSuccessorEdges()[0];
							ifstat.RemoveSuccessor(endedge);
							endedge.SetSource(ifelsestat);
							if (endedge.closure != null)
							{
								ifelsestat.GetParent().AddLabeledEdge(endedge);
							}
							ifelsestat.AddSuccessor(endedge);
						}
						ifelsestat.GetFirst().RemoveSuccessor(ifelsestat.GetIfEdge());
						ifelsestat.SetIfEdge(ifelsestat.GetElseEdge());
						ifelsestat.SetElseEdge(null);
						// negate head expression
						ifelsestat.SetNegated(!ifelsestat.IsNegated());
						ifelsestat.GetHeadexprentList()[0] = ((IfExprent)ifelsestat.GetHeadexprent().Copy
							()).NegateIf();
						return true;
					}
				}
			}
			bool replaced = true;
			while (replaced)
			{
				replaced = false;
				List<object> lstObjects = new List<object>(stat.GetExprents() == null ? (IEnumerable<object>) stat.GetSequentialObjects
					() : stat.GetExprents());
				for (int i = 0; i < lstObjects.Count; i++)
				{
					object obj = lstObjects[i];
					if (obj is Statement)
					{
						if (IdentifySecondaryFunctions((Statement)obj, varProc))
						{
							replaced = true;
							break;
						}
					}
					else if (obj is Exprent)
					{
						Exprent retexpr = IdentifySecondaryFunctions((Exprent)obj, true, varProc);
						if (retexpr != null)
						{
							if (stat.GetExprents() == null)
							{
								// only head expressions can be replaced!
								stat.ReplaceExprent((Exprent)obj, retexpr);
							}
							else
							{
								stat.GetExprents()[i] = retexpr;
							}
							replaced = true;
							break;
						}
					}
				}
			}
			return false;
		}

		private static Exprent IdentifySecondaryFunctions(Exprent exprent, bool statement_level
			, VarProcessor varProc)
		{
			if (exprent.type == Exprent.Exprent_Function)
			{
				FunctionExprent fexpr = (FunctionExprent)exprent;
				switch (fexpr.GetFuncType())
				{
					case FunctionExprent.Function_Bool_Not:
					{
						Exprent retparam = PropagateBoolNot(fexpr);
						if (retparam != null)
						{
							return retparam;
						}
						break;
					}

					case FunctionExprent.Function_Eq:
					case FunctionExprent.Function_Ne:
					case FunctionExprent.Function_Gt:
					case FunctionExprent.Function_Ge:
					case FunctionExprent.Function_Lt:
					case FunctionExprent.Function_Le:
					{
						Exprent expr1 = fexpr.GetLstOperands()[0];
						Exprent expr2 = fexpr.GetLstOperands()[1];
						if (expr1.type == Exprent.Exprent_Const)
						{
							expr2 = expr1;
							expr1 = fexpr.GetLstOperands()[1];
						}
						if (expr1.type == Exprent.Exprent_Function && expr2.type == Exprent.Exprent_Const)
						{
							FunctionExprent funcexpr = (FunctionExprent)expr1;
							ConstExprent cexpr = (ConstExprent)expr2;
							int functype = funcexpr.GetFuncType();
							if (functype == FunctionExprent.Function_Lcmp || functype == FunctionExprent.Function_Fcmpg
								 || functype == FunctionExprent.Function_Fcmpl || functype == FunctionExprent.Function_Dcmpg
								 || functype == FunctionExprent.Function_Dcmpl)
							{
								int desttype = -1;
								int?[] destcons = mapNumComparisons.GetOrNull(fexpr.GetFuncType());
								if (destcons != null)
								{
									int index = cexpr.GetIntValue() + 1;
									if (index >= 0 && index <= 2)
									{
										int? destcon = destcons[index];
										if (destcon != null)
										{
											desttype = destcon.Value;
										}
									}
								}
								if (desttype >= 0)
								{
									return new FunctionExprent(desttype, funcexpr.GetLstOperands(), funcexpr.bytecode
										);
								}
							}
						}
						break;
					}
				}
			}
			bool replaced = true;
			while (replaced)
			{
				replaced = false;
				foreach (Exprent expr in exprent.GetAllExprents())
				{
					Exprent retexpr = IdentifySecondaryFunctions(expr, false, varProc);
					if (retexpr != null)
					{
						exprent.ReplaceExprent(expr, retexpr);
						replaced = true;
						break;
					}
				}
			}
			switch (exprent.type)
			{
				case Exprent.Exprent_Function:
				{
					FunctionExprent fexpr_1 = (FunctionExprent)exprent;
					List<Exprent> lstOperands = fexpr_1.GetLstOperands();
					switch (fexpr_1.GetFuncType())
					{
						case FunctionExprent.Function_Xor:
						{
							for (int i = 0; i < 2; i++)
							{
								Exprent operand = lstOperands[i];
								VarType operandtype = operand.GetExprType();
								if (operand.type == Exprent.Exprent_Const && operandtype.type != ICodeConstants.Type_Boolean)
								{
									ConstExprent cexpr = (ConstExprent)operand;
									long val;
									if (operandtype.type == ICodeConstants.Type_Long)
									{
										val = (long)cexpr.GetValue();
									}
									else
									{
										val = (int)cexpr.GetValue();
									}
									if (val == -1)
									{
										List<Exprent> lstBitNotOperand = new List<Exprent>();
										lstBitNotOperand.Add(lstOperands[1 - i]);
										return new FunctionExprent(FunctionExprent.Function_Bit_Not, lstBitNotOperand, fexpr_1
											.bytecode);
									}
								}
							}
							break;
						}

						case FunctionExprent.Function_Eq:
						case FunctionExprent.Function_Ne:
						{
							if (lstOperands[0].GetExprType().type == ICodeConstants.Type_Boolean && lstOperands
								[1].GetExprType().type == ICodeConstants.Type_Boolean)
							{
								for (int i = 0; i < 2; i++)
								{
									if (lstOperands[i].type == Exprent.Exprent_Const)
									{
										ConstExprent cexpr = (ConstExprent)lstOperands[i];
										int val = (int)cexpr.GetValue();
										if ((fexpr_1.GetFuncType() == FunctionExprent.Function_Eq && val == 1) || (fexpr_1
											.GetFuncType() == FunctionExprent.Function_Ne && val == 0))
										{
											return lstOperands[1 - i];
										}
										else
										{
											List<Exprent> lstNotOperand = new List<Exprent>();
											lstNotOperand.Add(lstOperands[1 - i]);
											return new FunctionExprent(FunctionExprent.Function_Bool_Not, lstNotOperand, fexpr_1
												.bytecode);
										}
									}
								}
							}
							break;
						}

						case FunctionExprent.Function_Bool_Not:
						{
							if (lstOperands[0].type == Exprent.Exprent_Const)
							{
								int val = ((ConstExprent)lstOperands[0]).GetIntValue();
								if (val == 0)
								{
									return new ConstExprent(VarType.Vartype_Boolean, 1, fexpr_1.bytecode);
								}
								else
								{
									return new ConstExprent(VarType.Vartype_Boolean, 0, fexpr_1.bytecode);
								}
							}
							break;
						}

						case FunctionExprent.Function_Iif:
						{
							Exprent expr1_1 = lstOperands[1];
							Exprent expr2_1 = lstOperands[2];
							if (expr1_1.type == Exprent.Exprent_Const && expr2_1.type == Exprent.Exprent_Const)
							{
								ConstExprent cexpr1 = (ConstExprent)expr1_1;
								ConstExprent cexpr2 = (ConstExprent)expr2_1;
								if (cexpr1.GetExprType().type == ICodeConstants.Type_Boolean && cexpr2.GetExprType
									().type == ICodeConstants.Type_Boolean)
								{
									if (cexpr1.GetIntValue() == 0 && cexpr2.GetIntValue() != 0)
									{
										return new FunctionExprent(FunctionExprent.Function_Bool_Not, lstOperands[0], fexpr_1
											.bytecode);
									}
									else if (cexpr1.GetIntValue() != 0 && cexpr2.GetIntValue() == 0)
									{
										return lstOperands[0];
									}
								}
							}
							break;
						}

						case FunctionExprent.Function_Lcmp:
						case FunctionExprent.Function_Fcmpl:
						case FunctionExprent.Function_Fcmpg:
						case FunctionExprent.Function_Dcmpl:
						case FunctionExprent.Function_Dcmpg:
						{
							int var = DecompilerContext.GetCounterContainer().GetCounterAndIncrement(CounterContainer
								.Var_Counter);
							VarType type = lstOperands[0].GetExprType();
							FunctionExprent iff = new FunctionExprent(FunctionExprent.Function_Iif, Sharpen.Arrays.AsList<Exprent>
								(new FunctionExprent(FunctionExprent.Function_Lt, Sharpen.Arrays.AsList<Exprent>(new VarExprent
								(var, type, varProc), ConstExprent.GetZeroConstant(type.type)), null), new ConstExprent
								(VarType.Vartype_Int, -1, null), new ConstExprent(VarType.Vartype_Int, 1, null))
								, null);
							FunctionExprent head = new FunctionExprent(FunctionExprent.Function_Eq, Sharpen.Arrays.AsList<Exprent>
								(new AssignmentExprent(new VarExprent(var, type, varProc), new FunctionExprent(FunctionExprent
								.Function_Sub, Sharpen.Arrays.AsList(lstOperands[0], lstOperands[1]), null), null
								), ConstExprent.GetZeroConstant(type.type)), null);
							varProc.SetVarType(new VarVersionPair(var, 0), type);
							return new FunctionExprent(FunctionExprent.Function_Iif, Sharpen.Arrays.AsList<Exprent>(head
								, new ConstExprent(VarType.Vartype_Int, 0, null), iff), fexpr_1.bytecode);
						}
					}
					break;
				}

				case Exprent.Exprent_Assignment:
				{
					// check for conditional assignment
					AssignmentExprent asexpr = (AssignmentExprent)exprent;
					Exprent right = asexpr.GetRight();
					Exprent left = asexpr.GetLeft();
					if (right.type == Exprent.Exprent_Function)
					{
						FunctionExprent func = (FunctionExprent)right;
						VarType midlayer = null;
						if (func.GetFuncType() >= FunctionExprent.Function_I2l && func.GetFuncType() <= FunctionExprent
							.Function_I2s)
						{
							right = func.GetLstOperands()[0];
							midlayer = func.GetSimpleCastType();
							if (right.type == Exprent.Exprent_Function)
							{
								func = (FunctionExprent)right;
							}
							else
							{
								return null;
							}
						}
						List<Exprent> lstFuncOperands = func.GetLstOperands();
						Exprent cond = null;
						switch (func.GetFuncType())
						{
							case FunctionExprent.Function_Add:
							case FunctionExprent.Function_And:
							case FunctionExprent.Function_Or:
							case FunctionExprent.Function_Xor:
							{
								if (left.Equals(lstFuncOperands[1]))
								{
									cond = lstFuncOperands[0];
									break;
								}
								goto case FunctionExprent.Function_Sub;
							}

							case FunctionExprent.Function_Sub:
							case FunctionExprent.Function_Mul:
							case FunctionExprent.Function_Div:
							case FunctionExprent.Function_Rem:
							case FunctionExprent.Function_Shl:
							case FunctionExprent.Function_Shr:
							case FunctionExprent.Function_Ushr:
							{
								if (left.Equals(lstFuncOperands[0]))
								{
									cond = lstFuncOperands[1];
								}
								break;
							}
						}
						if (cond != null && (midlayer == null || midlayer.Equals(cond.GetExprType())))
						{
							asexpr.SetRight(cond);
							asexpr.SetCondType(func.GetFuncType());
						}
					}
					break;
				}

				case Exprent.Exprent_Invocation:
				{
					if (!statement_level)
					{
						// simplify if exprent is a real expression. The opposite case is pretty absurd, can still happen however (and happened at least once).
						Exprent retexpr = ConcatenationHelper.ContractStringConcat(exprent);
						if (!exprent.Equals(retexpr))
						{
							return retexpr;
						}
					}
					break;
				}
			}
			return null;
		}

		public static Exprent PropagateBoolNot(Exprent exprent)
		{
			if (exprent.type == Exprent.Exprent_Function)
			{
				FunctionExprent fexpr = (FunctionExprent)exprent;
				if (fexpr.GetFuncType() == FunctionExprent.Function_Bool_Not)
				{
					Exprent param = fexpr.GetLstOperands()[0];
					if (param.type == Exprent.Exprent_Function)
					{
						FunctionExprent fparam = (FunctionExprent)param;
						int ftype = fparam.GetFuncType();
						switch (ftype)
						{
							case FunctionExprent.Function_Bool_Not:
							{
								Exprent newexpr = fparam.GetLstOperands()[0];
								Exprent retexpr = PropagateBoolNot(newexpr);
								return retexpr == null ? newexpr : retexpr;
							}

							case FunctionExprent.Function_Cadd:
							case FunctionExprent.Function_Cor:
							{
								List<Exprent> operands = fparam.GetLstOperands();
								for (int i = 0; i < operands.Count; i++)
								{
									Exprent newparam = new FunctionExprent(FunctionExprent.Function_Bool_Not, operands
										[i], operands[i].bytecode);
									Exprent retparam = PropagateBoolNot(newparam);
									operands[i] = retparam == null ? newparam : retparam;
								}
								goto case FunctionExprent.Function_Eq;
							}

							case FunctionExprent.Function_Eq:
							case FunctionExprent.Function_Ne:
							case FunctionExprent.Function_Lt:
							case FunctionExprent.Function_Ge:
							case FunctionExprent.Function_Gt:
							case FunctionExprent.Function_Le:
							{
								fparam.SetFuncType(funcsnot[ftype - FunctionExprent.Function_Eq]);
								return fparam;
							}
						}
					}
				}
			}
			return null;
		}
	}
}
