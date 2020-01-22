// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Match
{
	public class MatchNode
	{
		public class RuleValue
		{
			public readonly int parameter;

			public readonly object value;

			public RuleValue(int parameter, object value)
			{
				this.parameter = parameter;
				this.value = value;
			}

			public virtual bool IsVariable()
			{
				string strValue = value.ToString();
				return (strValue[0] == '$' && strValue[strValue.Length - 1] == '$');
			}

			public override string ToString()
			{
				return value.ToString();
			}
		}

		public const int Matchnode_Statement = 0;

		public const int Matchnode_Exprent = 1;

		private readonly int type;

		private readonly Dictionary<IMatchable.MatchProperties, MatchNode.RuleValue> rules
			 = new Dictionary<IMatchable.MatchProperties, MatchNode.RuleValue>();

		private readonly List<MatchNode> children = new List<MatchNode>();

		public MatchNode(int type)
		{
			this.type = type;
		}

		public virtual void AddChild(MatchNode child)
		{
			children.Add(child);
		}

		public virtual void AddRule(IMatchable.MatchProperties property, MatchNode.RuleValue
			 value)
		{
			Sharpen.Collections.Put(rules, property, value);
		}

		public virtual int GetType()
		{
			return type;
		}

		public virtual List<MatchNode> GetChildren()
		{
			return children;
		}

		public virtual Dictionary<IMatchable.MatchProperties, MatchNode.RuleValue> GetRules
			()
		{
			return rules;
		}

		public virtual object GetRuleValue(IMatchable.MatchProperties property)
		{
			MatchNode.RuleValue rule = rules.GetOrNull(property);
			return rule == null ? null : rule.value;
		}
	}
}
