// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrainsDecompiler.Modules.Decompiler.Exps;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Struct.Gen;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Match
{
	public class MatchEngine
	{
		private static readonly Dictionary<string, IMatchable.MatchProperties> stat_properties
			 = new Dictionary<string, IMatchable.MatchProperties>();

		private static readonly Dictionary<string, IMatchable.MatchProperties> expr_properties
			 = new Dictionary<string, IMatchable.MatchProperties>();

		private static readonly Dictionary<string, int> stat_type = new Dictionary<string
			, int>();

		private static readonly Dictionary<string, int> expr_type = new Dictionary<string
			, int>();

		private static readonly Dictionary<string, int> expr_func_type = new Dictionary<
			string, int>();

		private static readonly Dictionary<string, int> expr_exit_type = new Dictionary<
			string, int>();

		private static readonly Dictionary<string, int> stat_if_type = new Dictionary<string
			, int>();

		private static readonly Dictionary<string, VarType> expr_const_type = new Dictionary
			<string, VarType>();

		static MatchEngine()
		{
			Sharpen.Collections.Put(stat_properties, "type", IMatchable.MatchProperties.Statement_Type
				);
			Sharpen.Collections.Put(stat_properties, "ret", IMatchable.MatchProperties.Statement_Ret
				);
			Sharpen.Collections.Put(stat_properties, "position", IMatchable.MatchProperties.Statement_Position
				);
			Sharpen.Collections.Put(stat_properties, "statsize", IMatchable.MatchProperties.Statement_Statsize
				);
			Sharpen.Collections.Put(stat_properties, "exprsize", IMatchable.MatchProperties.Statement_Exprsize
				);
			Sharpen.Collections.Put(stat_properties, "iftype", IMatchable.MatchProperties.Statement_Iftype
				);
			Sharpen.Collections.Put(expr_properties, "type", IMatchable.MatchProperties.Exprent_Type
				);
			Sharpen.Collections.Put(expr_properties, "ret", IMatchable.MatchProperties.Exprent_Ret
				);
			Sharpen.Collections.Put(expr_properties, "position", IMatchable.MatchProperties.Exprent_Position
				);
			Sharpen.Collections.Put(expr_properties, "functype", IMatchable.MatchProperties.Exprent_Functype
				);
			Sharpen.Collections.Put(expr_properties, "exittype", IMatchable.MatchProperties.Exprent_Exittype
				);
			Sharpen.Collections.Put(expr_properties, "consttype", IMatchable.MatchProperties.
				Exprent_Consttype);
			Sharpen.Collections.Put(expr_properties, "constvalue", IMatchable.MatchProperties
				.Exprent_Constvalue);
			Sharpen.Collections.Put(expr_properties, "invclass", IMatchable.MatchProperties.Exprent_Invocation_Class
				);
			Sharpen.Collections.Put(expr_properties, "signature", IMatchable.MatchProperties.
				Exprent_Invocation_Signature);
			Sharpen.Collections.Put(expr_properties, "parameter", IMatchable.MatchProperties.
				Exprent_Invocation_Parameter);
			Sharpen.Collections.Put(expr_properties, "index", IMatchable.MatchProperties.Exprent_Var_Index
				);
			Sharpen.Collections.Put(expr_properties, "name", IMatchable.MatchProperties.Exprent_Field_Name
				);
			Sharpen.Collections.Put(stat_type, "if", Statement.Type_If);
			Sharpen.Collections.Put(stat_type, "do", Statement.Type_Do);
			Sharpen.Collections.Put(stat_type, "switch", Statement.Type_Switch);
			Sharpen.Collections.Put(stat_type, "trycatch", Statement.Type_Trycatch);
			Sharpen.Collections.Put(stat_type, "basicblock", Statement.Type_Basicblock);
			Sharpen.Collections.Put(stat_type, "sequence", Statement.Type_Sequence);
			Sharpen.Collections.Put(expr_type, "array", Exprent.Exprent_Array);
			Sharpen.Collections.Put(expr_type, "assignment", Exprent.Exprent_Assignment);
			Sharpen.Collections.Put(expr_type, "constant", Exprent.Exprent_Const);
			Sharpen.Collections.Put(expr_type, "exit", Exprent.Exprent_Exit);
			Sharpen.Collections.Put(expr_type, "field", Exprent.Exprent_Field);
			Sharpen.Collections.Put(expr_type, "function", Exprent.Exprent_Function);
			Sharpen.Collections.Put(expr_type, "if", Exprent.Exprent_If);
			Sharpen.Collections.Put(expr_type, "invocation", Exprent.Exprent_Invocation);
			Sharpen.Collections.Put(expr_type, "monitor", Exprent.Exprent_Monitor);
			Sharpen.Collections.Put(expr_type, "new", Exprent.Exprent_New);
			Sharpen.Collections.Put(expr_type, "switch", Exprent.Exprent_Switch);
			Sharpen.Collections.Put(expr_type, "var", Exprent.Exprent_Var);
			Sharpen.Collections.Put(expr_type, "annotation", Exprent.Exprent_Annotation);
			Sharpen.Collections.Put(expr_type, "assert", Exprent.Exprent_Assert);
			Sharpen.Collections.Put(expr_func_type, "eq", FunctionExprent.Function_Eq);
			Sharpen.Collections.Put(expr_exit_type, "return", ExitExprent.Exit_Return);
			Sharpen.Collections.Put(expr_exit_type, "throw", ExitExprent.Exit_Throw);
			Sharpen.Collections.Put(stat_if_type, "if", IfStatement.Iftype_If);
			Sharpen.Collections.Put(stat_if_type, "ifelse", IfStatement.Iftype_Ifelse);
			Sharpen.Collections.Put(expr_const_type, "null", VarType.Vartype_Null);
			Sharpen.Collections.Put(expr_const_type, "string", VarType.Vartype_String);
		}

		private readonly MatchNode rootNode;

		private readonly Dictionary<string, object> variables = new Dictionary<string, object
			>();

		public MatchEngine(string description)
		{
			// each line is a separate statement/exprent
			string[] lines = description.Split("\n");
			int depth = 0;
			Stack<MatchNode> stack = new Stack<MatchNode>();
			foreach (string line in lines)
			{
				List<string> properties = new List<string>(Sharpen.Arrays.AsList(line.Split("\\s+"
					)));
				// split on any number of whitespaces
				if ((properties[0].Length == 0))
				{
					properties.RemoveAtReturningValue(0);
				}
				int node_type = "statement".Equals(properties[0]) ? MatchNode.Matchnode_Statement
					 : MatchNode.Matchnode_Exprent;
				// create new node
				MatchNode matchNode = new MatchNode(node_type);
				for (int i = 1; i < properties.Count; ++i)
				{
					string[] values = properties[i].Split(":");
					IMatchable.MatchProperties property = (node_type == MatchNode.Matchnode_Statement
						 ? stat_properties : expr_properties).GetOrNull(values[0]);
					if (property == null)
					{
						// unknown property defined
						throw new Exception("Unknown matching property");
					}
					else
					{
						object value;
						int parameter = 0;
						string strValue = values[1];
						if (values.Length == 3)
						{
							parameter = System.Convert.ToInt32(values[1]);
							strValue = values[2];
						}
						switch (property.ordinal())
						{
							case 0:
							{
								value = stat_type.GetOrNullable(strValue);
								break;
							}

							case 2:
							case 3:
							{
								value = int.Parse(strValue);
								break;
							}

							case 4:
							case 8:
							case 13:
							case 14:
							case 15:
							case 16:
							case 17:
							case 12:
							case 1:
							case 7:
							{
								value = strValue;
								break;
							}

							case 5:
							{
								value = stat_if_type.GetOrNullable(strValue);
								break;
							}

							case 9:
							{
								value = expr_func_type.GetOrNullable(strValue);
								break;
							}

							case 10:
							{
								value = expr_exit_type.GetOrNullable(strValue);
								break;
							}

							case 11:
							{
								value = expr_const_type.GetOrNull(strValue);
								break;
							}

							case 6:
							{
								value = expr_type.GetOrNullable(strValue);
								break;
							}

							default:
							{
								throw new Exception("Unhandled matching property");
							}
						}
						matchNode.AddRule(property, new MatchNode.RuleValue(parameter, value));
					}
				}
				if ((stack.Count == 0))
				{
					// first line, root node
					stack.Push(matchNode);
				}
				else
				{
					// return to the correct parent on the stack
					int new_depth = line.LastIndexOf(' ', depth) + 1;
					for (int i = new_depth; i <= depth; ++i)
					{
						stack.Pop();
					}
					// insert new node
					stack.First().AddChild(matchNode);
					stack.Push(matchNode);
					depth = new_depth;
				}
			}
			this.rootNode = stack.Last();
		}

		public virtual bool Match(IMatchable @object)
		{
			variables.Clear();
			return Match(this.rootNode, @object);
		}

		private bool Match(MatchNode matchNode, IMatchable @object)
		{
			if (!@object.Match(matchNode, this))
			{
				return false;
			}
			int expr_index = 0;
			int stat_index = 0;
			foreach (MatchNode childNode in matchNode.GetChildren())
			{
				bool isStatement = childNode.GetType() == MatchNode.Matchnode_Statement;
				IMatchable childObject = @object.FindObject(childNode, isStatement ? stat_index : 
					expr_index);
				if (childObject == null || !Match(childNode, childObject))
				{
					return false;
				}
				if (isStatement)
				{
					stat_index++;
				}
				else
				{
					expr_index++;
				}
			}
			return true;
		}

		public virtual bool CheckAndSetVariableValue(string name, object value)
		{
			object old_value = variables.GetOrNull(name);
			if (old_value != null)
			{
				return old_value.Equals(value);
			}
			else
			{
				Sharpen.Collections.Put(variables, name, value);
				return true;
			}
		}

		public virtual object GetVariableValue(string name)
		{
			return variables.GetOrNull(name);
		}
	}
}
