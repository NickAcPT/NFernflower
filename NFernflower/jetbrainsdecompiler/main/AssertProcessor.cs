// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Code.Cfg;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main
{
	public class AssertProcessor
	{
		private static readonly VarType Class_Assertion_Error = new VarType(ICodeConstants
			.Type_Object, 0, "java/lang/AssertionError");

		public static void BuildAssertions(ClassesProcessor.ClassNode node)
		{
			ClassWrapper wrapper = node.GetWrapper();
			StructField field = FindAssertionField(node);
			if (field != null)
			{
				string key = InterpreterUtil.MakeUniqueKey(field.GetName(), field.GetDescriptor()
					);
				bool res = false;
				foreach (MethodWrapper meth in wrapper.GetMethods())
				{
					RootStatement root = meth.root;
					if (root != null)
					{
						res |= ReplaceAssertions(root, wrapper.GetClassStruct().qualifiedName, key);
					}
				}
				if (res)
				{
					// hide the helper field
					wrapper.GetHiddenMembers().Add(key);
				}
			}
		}

		private static StructField FindAssertionField(ClassesProcessor.ClassNode node)
		{
			ClassWrapper wrapper = node.GetWrapper();
			bool noSynthFlag = DecompilerContext.GetOption(IIFernflowerPreferences.Synthetic_Not_Set
				);
			foreach (StructField fd in wrapper.GetClassStruct().GetFields())
			{
				string keyField = InterpreterUtil.MakeUniqueKey(fd.GetName(), fd.GetDescriptor());
				// initializer exists
				if (wrapper.GetStaticFieldInitializers().ContainsKey(keyField))
				{
					// access flags set
					if (fd.HasModifier(ICodeConstants.Acc_Static) && fd.HasModifier(ICodeConstants.Acc_Final
						) && (noSynthFlag || fd.IsSynthetic()))
					{
						// field type boolean
						FieldDescriptor fdescr = FieldDescriptor.ParseDescriptor(fd.GetDescriptor());
						if (VarType.Vartype_Boolean.Equals(fdescr.type))
						{
							Exprent initializer = wrapper.GetStaticFieldInitializers().GetWithKey(keyField);
							if (initializer.type == Exprent.Exprent_Function)
							{
								FunctionExprent fexpr = (FunctionExprent)initializer;
								if (fexpr.GetFuncType() == FunctionExprent.Function_Bool_Not && fexpr.GetLstOperands
									()[0].type == Exprent.Exprent_Invocation)
								{
									InvocationExprent invexpr = (InvocationExprent)fexpr.GetLstOperands()[0];
									if (invexpr.GetInstance() != null && invexpr.GetInstance().type == Exprent.Exprent_Const
										 && "desiredAssertionStatus".Equals(invexpr.GetName()) && "java/lang/Class".Equals
										(invexpr.GetClassname()) && (invexpr.GetLstParameters().Count == 0))
									{
										ConstExprent cexpr = (ConstExprent)invexpr.GetInstance();
										if (VarType.Vartype_Class.Equals(cexpr.GetConstType()))
										{
											ClassesProcessor.ClassNode nd = node;
											while (nd != null)
											{
												if (nd.GetWrapper().GetClassStruct().qualifiedName.Equals(cexpr.GetValue()))
												{
													break;
												}
												nd = nd.parent;
											}
											if (nd != null)
											{
												// found enclosing class with the same name
												return fd;
											}
										}
									}
								}
							}
						}
					}
				}
			}
			return null;
		}

		private static bool ReplaceAssertions(Statement statement, string classname, string
			 key)
		{
			bool res = false;
			foreach (Statement st in statement.GetStats())
			{
				res |= ReplaceAssertions(st, classname, key);
			}
			bool replaced = true;
			while (replaced)
			{
				replaced = false;
				foreach (Statement st in statement.GetStats())
				{
					if (st.type == Statement.Type_If)
					{
						if (ReplaceAssertion(statement, (IfStatement)st, classname, key))
						{
							replaced = true;
							break;
						}
					}
				}
				res |= replaced;
			}
			return res;
		}

		private static bool ReplaceAssertion(Statement parent, IfStatement stat, string classname
			, string key)
		{
			bool throwInIf = true;
			Statement ifstat = stat.GetIfstat();
			InvocationExprent throwError = IsAssertionError(ifstat);
			if (throwError == null)
			{
				//check else:
				Statement elsestat = stat.GetElsestat();
				throwError = IsAssertionError(elsestat);
				if (throwError == null)
				{
					return false;
				}
				else
				{
					throwInIf = false;
				}
			}
			object[] exprres = GetAssertionExprent(stat.GetHeadexprent().GetCondition().Copy(
				), classname, key, throwInIf);
			if (!(bool)exprres[1])
			{
				return false;
			}
			List<Exprent> lstParams = new List<Exprent>();
			Exprent ascond = null;
			Exprent retcond = null;
			if (throwInIf)
			{
				if (exprres[0] != null)
				{
					ascond = new FunctionExprent(FunctionExprent.Function_Bool_Not, (Exprent)exprres[
						0], throwError.bytecode);
					retcond = SecondaryFunctionsHelper.PropagateBoolNot(ascond);
				}
			}
			else
			{
				ascond = (Exprent)exprres[0];
				retcond = ascond;
			}
			lstParams.Add(retcond == null ? ascond : retcond);
			if (!(throwError.GetLstParameters().Count == 0))
			{
				lstParams.Add(throwError.GetLstParameters()[0]);
			}
			AssertExprent asexpr = new AssertExprent(lstParams);
			Statement newstat = new BasicBlockStatement(new BasicBlock(DecompilerContext.GetCounterContainer
				().GetCounterAndIncrement(CounterContainer.Statement_Counter)));
			newstat.SetExprents(Sharpen.Arrays.AsList(new Exprent[] { asexpr }));
			Statement first = stat.GetFirst();
			if (stat.iftype == IfStatement.Iftype_Ifelse || (first.GetExprents() != null && !
				(first.GetExprents().Count == 0)))
			{
				first.RemoveSuccessor(stat.GetIfEdge());
				first.RemoveSuccessor(stat.GetElseEdge());
				List<Statement> lstStatements = new List<Statement>();
				if (first.GetExprents() != null && !(first.GetExprents().Count == 0))
				{
					lstStatements.Add(first);
				}
				lstStatements.Add(newstat);
				if (stat.iftype == IfStatement.Iftype_Ifelse)
				{
					if (throwInIf)
					{
						lstStatements.Add(stat.GetElsestat());
					}
					else
					{
						lstStatements.Add(stat.GetIfstat());
					}
				}
				SequenceStatement sequence = new SequenceStatement(lstStatements);
				sequence.SetAllParent();
				for (int i = 0; i < sequence.GetStats().Count - 1; i++)
				{
					sequence.GetStats()[i].AddSuccessor(new StatEdge(StatEdge.Type_Regular, sequence.
						GetStats()[i], sequence.GetStats()[i + 1]));
				}
				if (stat.iftype == IfStatement.Iftype_Ifelse || !throwInIf)
				{
					Statement stmts;
					if (throwInIf)
					{
						stmts = stat.GetElsestat();
					}
					else
					{
						stmts = stat.GetIfstat();
					}
					List<StatEdge> lstSuccs = stmts.GetAllSuccessorEdges();
					if (!(lstSuccs.Count == 0))
					{
						StatEdge endedge = lstSuccs[0];
						if (endedge.closure == stat)
						{
							sequence.AddLabeledEdge(endedge);
						}
					}
				}
				newstat = sequence;
			}
			Sharpen.Collections.AddAll(newstat.GetVarDefinitions(), stat.GetVarDefinitions());
			parent.ReplaceStatement(stat, newstat);
			return true;
		}

		private static InvocationExprent IsAssertionError(Statement stat)
		{
			if (stat == null || stat.GetExprents() == null || stat.GetExprents().Count != 1)
			{
				return null;
			}
			Exprent expr = stat.GetExprents()[0];
			if (expr.type == Exprent.Exprent_Exit)
			{
				ExitExprent exexpr = (ExitExprent)expr;
				if (exexpr.GetExitType() == ExitExprent.Exit_Throw && exexpr.GetValue().type == Exprent
					.Exprent_New)
				{
					NewExprent nexpr = (NewExprent)exexpr.GetValue();
					if (Class_Assertion_Error.Equals(nexpr.GetNewType()) && nexpr.GetConstructor() !=
						 null)
					{
						return nexpr.GetConstructor();
					}
				}
			}
			return null;
		}

		private static object[] GetAssertionExprent(Exprent exprent, string classname, string
			 key, bool throwInIf)
		{
			if (exprent.type == Exprent.Exprent_Function)
			{
				int desiredOperation = FunctionExprent.Function_Cadd;
				if (!throwInIf)
				{
					desiredOperation = FunctionExprent.Function_Cor;
				}
				FunctionExprent fexpr = (FunctionExprent)exprent;
				if (fexpr.GetFuncType() == desiredOperation)
				{
					for (int i = 0; i < 2; i++)
					{
						Exprent param = fexpr.GetLstOperands()[i];
						if (IsAssertionField(param, classname, key, throwInIf))
						{
							return new object[] { fexpr.GetLstOperands()[1 - i], true };
						}
					}
					for (int i = 0; i < 2; i++)
					{
						Exprent param = fexpr.GetLstOperands()[i];
						object[] res = GetAssertionExprent(param, classname, key, throwInIf);
						if ((bool)res[1])
						{
							if (param != res[0])
							{
								fexpr.GetLstOperands()[i] = (Exprent)res[0];
							}
							return new object[] { fexpr, true };
						}
					}
				}
				else if (IsAssertionField(fexpr, classname, key, throwInIf))
				{
					// assert false;
					return new object[] { null, true };
				}
			}
			return new object[] { exprent, false };
		}

		private static bool IsAssertionField(Exprent exprent, string classname, string key
			, bool throwInIf)
		{
			if (throwInIf)
			{
				if (exprent.type == Exprent.Exprent_Function)
				{
					FunctionExprent fparam = (FunctionExprent)exprent;
					if (fparam.GetFuncType() == FunctionExprent.Function_Bool_Not && fparam.GetLstOperands
						()[0].type == Exprent.Exprent_Field)
					{
						FieldExprent fdparam = (FieldExprent)fparam.GetLstOperands()[0];
						return classname.Equals(fdparam.GetClassname()) && key.Equals(InterpreterUtil.MakeUniqueKey
							(fdparam.GetName(), fdparam.GetDescriptor().descriptorString));
					}
				}
			}
			else if (exprent.type == Exprent.Exprent_Field)
			{
				FieldExprent fdparam = (FieldExprent)exprent;
				return classname.Equals(fdparam.GetClassname()) && key.Equals(InterpreterUtil.MakeUniqueKey
					(fdparam.GetName(), fdparam.GetDescriptor().descriptorString));
			}
			return false;
		}
	}
}
