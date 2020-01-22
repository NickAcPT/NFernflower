/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System;
using System.Collections.Generic;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Struct.Match;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class Exprent : IMatchable
	{
		public const int Multiple_Uses = 1;

		public const int Side_Effects_Free = 2;

		public const int Both_Flags = 3;

		public const int Exprent_Array = 1;

		public const int Exprent_Assignment = 2;

		public const int Exprent_Const = 3;

		public const int Exprent_Exit = 4;

		public const int Exprent_Field = 5;

		public const int Exprent_Function = 6;

		public const int Exprent_If = 7;

		public const int Exprent_Invocation = 8;

		public const int Exprent_Monitor = 9;

		public const int Exprent_New = 10;

		public const int Exprent_Switch = 11;

		public const int Exprent_Var = 12;

		public const int Exprent_Annotation = 13;

		public const int Exprent_Assert = 14;

		public readonly int type;

		public readonly int id;

		public HashSet<int> bytecode = null;

		public Exprent(int type)
		{
			// offsets of bytecode instructions decompiled to this exprent
			this.type = type;
			this.id = DecompilerContext.GetCounterContainer().GetCounterAndIncrement(CounterContainer
				.Expression_Counter);
		}

		public virtual int GetPrecedence()
		{
			return 0;
		}

		// the highest precedence
		public virtual VarType GetExprType()
		{
			return VarType.Vartype_Void;
		}

		public virtual int GetExprentUse()
		{
			return 0;
		}

		public virtual CheckTypesResult CheckExprTypeBounds()
		{
			return null;
		}

		public virtual bool ContainsExprent(Exprent exprent)
		{
			if (Equals(exprent))
			{
				return true;
			}
			List<Exprent> lst = GetAllExprents();
			for (int i = lst.Count - 1; i >= 0; i--)
			{
				if (lst[i].ContainsExprent(exprent))
				{
					return true;
				}
			}
			return false;
		}

		public virtual List<Exprent> GetAllExprents(bool recursive)
		{
			List<Exprent> lst = GetAllExprents();
			if (recursive)
			{
				for (int i = lst.Count - 1; i >= 0; i--)
				{
					Sharpen.Collections.AddAll(lst, lst[i].GetAllExprents(true));
				}
			}
			return lst;
		}

		public virtual HashSet<VarVersionPair> GetAllVariables()
		{
			List<Exprent> lstAllExprents = GetAllExprents(true);
			lstAllExprents.Add(this);
			HashSet<VarVersionPair> set = new HashSet<VarVersionPair>();
			foreach (Exprent expr in lstAllExprents)
			{
				if (expr.type == Exprent_Var)
				{
					set.Add(new VarVersionPair((VarExprent)expr));
				}
			}
			return set;
		}

		public virtual List<Exprent> GetAllExprents()
		{
			throw new Exception("not implemented");
		}

		public virtual Exprent Copy()
		{
			throw new Exception("not implemented");
		}

		public virtual TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			throw new Exception("not implemented");
		}

		public virtual void ReplaceExprent(Exprent oldExpr, Exprent newExpr)
		{
		}

		public virtual void AddBytecodeOffsets(ICollection<int> bytecodeOffsets)
		{
			if (bytecodeOffsets != null && !(bytecodeOffsets.Count == 0))
			{
				if (bytecode == null)
				{
					bytecode = new HashSet<int>(bytecodeOffsets);
				}
				else
				{
					Sharpen.Collections.AddAll(bytecode, bytecodeOffsets);
				}
			}
		}

		// *****************************************************************************
		// IMatchable implementation
		// *****************************************************************************
		public override IMatchable FindObject(MatchNode matchNode, int index)
		{
			if (matchNode.GetType() != MatchNode.Matchnode_Exprent)
			{
				return null;
			}
			List<Exprent> lstAllExprents = GetAllExprents();
			if (lstAllExprents == null || (lstAllExprents.Count == 0))
			{
				return null;
			}
			string position = (string)matchNode.GetRuleValue(IMatchable.MatchProperties.Exprent_Position
				);
			if (position != null)
			{
				if (position.Matches("-?\\d+"))
				{
					return lstAllExprents[(lstAllExprents.Count + System.Convert.ToInt32(position)) %
						 lstAllExprents.Count];
				}
			}
			else if (index < lstAllExprents.Count)
			{
				// care for negative positions
				// use 'index' parameter
				return lstAllExprents[index];
			}
			return null;
		}

		public override bool Match(MatchNode matchNode, MatchEngine engine)
		{
			if (matchNode.GetType() != MatchNode.Matchnode_Exprent)
			{
				return false;
			}
			foreach (KeyValuePair<IMatchable.MatchProperties, MatchNode.RuleValue> rule in matchNode
				.GetRules())
			{
				IMatchable.MatchProperties key = rule.Key;
				if (key == IMatchable.MatchProperties.Exprent_Type && this.type != (int)rule.Value
					.value)
				{
					return false;
				}
				if (key == IMatchable.MatchProperties.Exprent_Ret && !engine.CheckAndSetVariableValue
					((string)rule.Value.value, this))
				{
					return false;
				}
			}
			return true;
		}

		public override string ToString()
		{
			return ToJava(0, BytecodeMappingTracer.Dummy).ToString();
		}
	}
}
