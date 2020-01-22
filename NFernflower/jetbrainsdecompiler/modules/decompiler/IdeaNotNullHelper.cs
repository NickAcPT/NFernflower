// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Gen;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class IdeaNotNullHelper
	{
		public static bool RemoveHardcodedChecks(Statement root, StructMethod mt)
		{
			bool checks_removed = false;
			// parameter @NotNull annotations
			while (FindAndRemoveParameterCheck(root, mt))
			{
				// iterate until nothing found. Each invocation removes one parameter check.
				checks_removed = true;
			}
			// method @NotNull annotation
			while (FindAndRemoveReturnCheck(root, mt))
			{
				// iterate until nothing found. Each invocation handles one method exit check.
				checks_removed = true;
			}
			return checks_removed;
		}

		private static bool FindAndRemoveParameterCheck(Statement stat, StructMethod mt)
		{
			Statement st = stat.GetFirst();
			while (st.type == Statement.Type_Sequence)
			{
				st = st.GetFirst();
			}
			if (st.type == Statement.Type_If)
			{
				IfStatement ifstat = (IfStatement)st;
				Statement ifbranch = ifstat.GetIfstat();
				Exprent if_condition = ifstat.GetHeadexprent().GetCondition();
				bool is_notnull_check = false;
				// TODO: FUNCTION_NE also possible if reversed order (in theory)
				if (ifbranch != null && if_condition.type == Exprent.Exprent_Function && ((FunctionExprent
					)if_condition).GetFuncType() == FunctionExprent.Function_Eq && ifbranch.type == 
					Statement.Type_Basicblock && ifbranch.GetExprents().Count == 1 && ifbranch.GetExprents
					()[0].type == Exprent.Exprent_Exit)
				{
					FunctionExprent func = (FunctionExprent)if_condition;
					Exprent first_param = func.GetLstOperands()[0];
					Exprent second_param = func.GetLstOperands()[1];
					if (second_param.type == Exprent.Exprent_Const && second_param.GetExprType().type
						 == ICodeConstants.Type_Null)
					{
						// TODO: reversed parameter order
						if (first_param.type == Exprent.Exprent_Var)
						{
							VarExprent var = (VarExprent)first_param;
							bool thisvar = !mt.HasModifier(ICodeConstants.Acc_Static);
							MethodDescriptor md = MethodDescriptor.ParseDescriptor(mt.GetDescriptor());
							// parameter annotations
							StructAnnotationParameterAttribute param_annotations = mt.GetAttribute(StructGeneralAttribute
								.Attribute_Runtime_Invisible_Parameter_Annotations);
							if (param_annotations != null)
							{
								List<List<AnnotationExprent>> param_annotations_lists = param_annotations.GetParamAnnotations
									();
								int method_param_number = md.@params.Length;
								int index = thisvar ? 1 : 0;
								for (int i = 0; i < method_param_number; i++)
								{
									if (index == var.GetIndex())
									{
										if (param_annotations_lists.Count >= method_param_number - i)
										{
											int shift = method_param_number - param_annotations_lists.Count;
											// NOTE: workaround for compiler bug, count annotations starting with the last parameter
											List<AnnotationExprent> annotations = param_annotations_lists[i - shift];
											foreach (AnnotationExprent ann in annotations)
											{
												if (ann.GetClassName().Equals("org/jetbrains/annotations/NotNull"))
												{
													is_notnull_check = true;
													break;
												}
											}
										}
										break;
									}
									index += md.@params[i].stackSize;
								}
							}
						}
					}
				}
				if (!is_notnull_check)
				{
					return false;
				}
				RemoveParameterCheck(stat);
				return true;
			}
			return false;
		}

		private static void RemoveParameterCheck(Statement stat)
		{
			Statement st = stat.GetFirst();
			while (st.type == Statement.Type_Sequence)
			{
				st = st.GetFirst();
			}
			IfStatement ifstat = (IfStatement)st;
			if (ifstat.GetElsestat() != null)
			{
				// if - else
				StatEdge ifedge = ifstat.GetIfEdge();
				StatEdge elseedge = ifstat.GetElseEdge();
				Statement ifbranch = ifstat.GetIfstat();
				Statement elsebranch = ifstat.GetElsestat();
				ifstat.GetFirst().RemoveSuccessor(ifedge);
				ifstat.GetFirst().RemoveSuccessor(elseedge);
				ifstat.GetStats().RemoveWithKey(ifbranch.id);
				ifstat.GetStats().RemoveWithKey(elsebranch.id);
				if (!(ifbranch.GetAllSuccessorEdges().Count == 0))
				{
					ifbranch.RemoveSuccessor(ifbranch.GetAllSuccessorEdges()[0]);
				}
				ifstat.GetParent().ReplaceStatement(ifstat, elsebranch);
				ifstat.GetParent().SetAllParent();
			}
		}

		private static bool FindAndRemoveReturnCheck(Statement stat, StructMethod mt)
		{
			bool is_notnull_check = false;
			// method annotation, refers to the return value
			StructAnnotationAttribute attr = mt.GetAttribute(StructGeneralAttribute.Attribute_Runtime_Invisible_Annotations
				);
			if (attr != null)
			{
				List<AnnotationExprent> annotations = attr.GetAnnotations();
				foreach (AnnotationExprent ann in annotations)
				{
					if (ann.GetClassName().Equals("org/jetbrains/annotations/NotNull"))
					{
						is_notnull_check = true;
						break;
					}
				}
			}
			return is_notnull_check && RemoveReturnCheck(stat, mt);
		}

		private static bool RemoveReturnCheck(Statement stat, StructMethod mt)
		{
			Statement parent = stat.GetParent();
			if (parent != null && parent.type == Statement.Type_If && stat.type == Statement.
				Type_Basicblock && stat.GetExprents().Count == 1)
			{
				Exprent exprent = stat.GetExprents()[0];
				if (exprent.type == Exprent.Exprent_Exit)
				{
					ExitExprent exit_exprent = (ExitExprent)exprent;
					if (exit_exprent.GetExitType() == ExitExprent.Exit_Return)
					{
						Exprent exprent_value = exit_exprent.GetValue();
						//if(exprent_value.type == Exprent.EXPRENT_VAR) {
						//	VarExprent var_value = (VarExprent)exprent_value;
						IfStatement ifparent = (IfStatement)parent;
						Exprent if_condition = ifparent.GetHeadexprent().GetCondition();
						if (ifparent.GetElsestat() == stat && if_condition.type == Exprent.Exprent_Function
							 && ((FunctionExprent)if_condition).GetFuncType() == FunctionExprent.Function_Eq)
						{
							// TODO: reversed order possible (in theory)
							FunctionExprent func = (FunctionExprent)if_condition;
							Exprent first_param = func.GetLstOperands()[0];
							Exprent second_param = func.GetLstOperands()[1];
							StatEdge ifedge = ifparent.GetIfEdge();
							StatEdge elseedge = ifparent.GetElseEdge();
							Statement ifbranch = ifparent.GetIfstat();
							Statement elsebranch = ifparent.GetElsestat();
							if (second_param.type == Exprent.Exprent_Const && second_param.GetExprType().type
								 == ICodeConstants.Type_Null)
							{
								// TODO: reversed parameter order
								//if(first_param.type == Exprent.EXPRENT_VAR && ((VarExprent)first_param).getIndex() == var_value.getIndex()) {
								if (first_param.Equals(exprent_value))
								{
									// TODO: check for absence of side effects like method invocations etc.
									if (ifbranch.type == Statement.Type_Basicblock && ifbranch.GetExprents().Count ==
										 1 && ifbranch.GetExprents()[0].type == Exprent.Exprent_Exit)
									{
										// TODO: special check for IllegalStateException
										ifparent.GetFirst().RemoveSuccessor(ifedge);
										ifparent.GetFirst().RemoveSuccessor(elseedge);
										ifparent.GetStats().RemoveWithKey(ifbranch.id);
										ifparent.GetStats().RemoveWithKey(elsebranch.id);
										if (!(ifbranch.GetAllSuccessorEdges().Count == 0))
										{
											ifbranch.RemoveSuccessor(ifbranch.GetAllSuccessorEdges()[0]);
										}
										if (!(ifparent.GetFirst().GetExprents().Count == 0))
										{
											elsebranch.GetExprents().InsertRange(0, ifparent.GetFirst().GetExprents());
										}
										ifparent.GetParent().ReplaceStatement(ifparent, elsebranch);
										ifparent.GetParent().SetAllParent();
										return true;
									}
								}
							}
						}
					}
				}
			}
			else if (parent != null && parent.type == Statement.Type_Sequence && stat.type ==
				 Statement.Type_Basicblock && stat.GetExprents().Count == 1)
			{
				//}
				Exprent exprent = stat.GetExprents()[0];
				if (exprent.type == Exprent.Exprent_Exit)
				{
					ExitExprent exit_exprent = (ExitExprent)exprent;
					if (exit_exprent.GetExitType() == ExitExprent.Exit_Return)
					{
						Exprent exprent_value = exit_exprent.GetValue();
						SequenceStatement sequence = (SequenceStatement)parent;
						int sequence_stats_number = sequence.GetStats().Count;
						if (sequence_stats_number > 1 && sequence.GetStats().GetLast() == stat && sequence
							.GetStats()[sequence_stats_number - 2].type == Statement.Type_If)
						{
							IfStatement ifstat = (IfStatement)sequence.GetStats()[sequence_stats_number - 2];
							Exprent if_condition = ifstat.GetHeadexprent().GetCondition();
							if (ifstat.iftype == IfStatement.Iftype_If && if_condition.type == Exprent.Exprent_Function
								 && ((FunctionExprent)if_condition).GetFuncType() == FunctionExprent.Function_Eq)
							{
								// TODO: reversed order possible (in theory)
								FunctionExprent func = (FunctionExprent)if_condition;
								Exprent first_param = func.GetLstOperands()[0];
								Exprent second_param = func.GetLstOperands()[1];
								Statement ifbranch = ifstat.GetIfstat();
								if (second_param.type == Exprent.Exprent_Const && second_param.GetExprType().type
									 == ICodeConstants.Type_Null)
								{
									// TODO: reversed parameter order
									if (first_param.Equals(exprent_value))
									{
										// TODO: check for absence of side effects like method invocations etc.
										if (ifbranch.type == Statement.Type_Basicblock && ifbranch.GetExprents().Count ==
											 1 && ifbranch.GetExprents()[0].type == Exprent.Exprent_Exit)
										{
											// TODO: special check for IllegalStateException
											ifstat.RemoveSuccessor(ifstat.GetAllSuccessorEdges()[0]);
											// remove 'else' edge
											if (!(ifstat.GetFirst().GetExprents().Count == 0))
											{
												stat.GetExprents().InsertRange(0, ifstat.GetFirst().GetExprents());
											}
											foreach (StatEdge edge in ifstat.GetAllPredecessorEdges())
											{
												ifstat.RemovePredecessor(edge);
												edge.GetSource().ChangeEdgeNode(Statement.Direction_Forward, edge, stat);
												stat.AddPredecessor(edge);
											}
											sequence.GetStats().RemoveWithKey(ifstat.id);
											sequence.SetFirst(sequence.GetStats()[0]);
											return true;
										}
									}
								}
							}
						}
					}
				}
			}
			foreach (Statement st in stat.GetStats())
			{
				if (RemoveReturnCheck(st, mt))
				{
					return true;
				}
			}
			return false;
		}
	}
}
