// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Sharpen;

namespace JetBrainsDecompiler.Struct.Match
{
	public abstract class IMatchable
	{
		[System.Serializable]
		public sealed class MatchProperties : Sharpen.EnumBase
		{
			public static readonly IMatchable.MatchProperties Statement_Type = new IMatchable.MatchProperties
				(0, "STATEMENT_TYPE");

			public static readonly IMatchable.MatchProperties Statement_Ret = new IMatchable.MatchProperties
				(1, "STATEMENT_RET");

			public static readonly IMatchable.MatchProperties Statement_Statsize = new IMatchable.MatchProperties
				(2, "STATEMENT_STATSIZE");

			public static readonly IMatchable.MatchProperties Statement_Exprsize = new IMatchable.MatchProperties
				(3, "STATEMENT_EXPRSIZE");

			public static readonly IMatchable.MatchProperties Statement_Position = new IMatchable.MatchProperties
				(4, "STATEMENT_POSITION");

			public static readonly IMatchable.MatchProperties Statement_Iftype = new IMatchable.MatchProperties
				(5, "STATEMENT_IFTYPE");

			public static readonly IMatchable.MatchProperties Exprent_Type = new IMatchable.MatchProperties
				(6, "EXPRENT_TYPE");

			public static readonly IMatchable.MatchProperties Exprent_Ret = new IMatchable.MatchProperties
				(7, "EXPRENT_RET");

			public static readonly IMatchable.MatchProperties Exprent_Position = new IMatchable.MatchProperties
				(8, "EXPRENT_POSITION");

			public static readonly IMatchable.MatchProperties Exprent_Functype = new IMatchable.MatchProperties
				(9, "EXPRENT_FUNCTYPE");

			public static readonly IMatchable.MatchProperties Exprent_Exittype = new IMatchable.MatchProperties
				(10, "EXPRENT_EXITTYPE");

			public static readonly IMatchable.MatchProperties Exprent_Consttype = new IMatchable.MatchProperties
				(11, "EXPRENT_CONSTTYPE");

			public static readonly IMatchable.MatchProperties Exprent_Constvalue = new IMatchable.MatchProperties
				(12, "EXPRENT_CONSTVALUE");

			public static readonly IMatchable.MatchProperties Exprent_Invocation_Class = new 
				IMatchable.MatchProperties(13, "EXPRENT_INVOCATION_CLASS");

			public static readonly IMatchable.MatchProperties Exprent_Invocation_Signature = 
				new IMatchable.MatchProperties(14, "EXPRENT_INVOCATION_SIGNATURE");

			public static readonly IMatchable.MatchProperties Exprent_Invocation_Parameter = 
				new IMatchable.MatchProperties(15, "EXPRENT_INVOCATION_PARAMETER");

			public static readonly IMatchable.MatchProperties Exprent_Var_Index = new IMatchable.MatchProperties
				(16, "EXPRENT_VAR_INDEX");

			public static readonly IMatchable.MatchProperties Exprent_Field_Name = new IMatchable.MatchProperties
				(17, "EXPRENT_FIELD_NAME");

			private MatchProperties(int ordinal, string name)
				: base(ordinal, name)
			{
			}

			public static MatchProperties[] Values()
			{
				return new MatchProperties[] { Statement_Type, Statement_Ret, Statement_Statsize, 
					Statement_Exprsize, Statement_Position, Statement_Iftype, Exprent_Type, Exprent_Ret
					, Exprent_Position, Exprent_Functype, Exprent_Exittype, Exprent_Consttype, Exprent_Constvalue
					, Exprent_Invocation_Class, Exprent_Invocation_Signature, Exprent_Invocation_Parameter
					, Exprent_Var_Index, Exprent_Field_Name };
			}

			static MatchProperties()
			{
				RegisterValues<MatchProperties>(Values());
			}
		}

		public abstract IMatchable FindObject(MatchNode matchNode, int index);

		public abstract bool Match(MatchNode matchNode, MatchEngine engine);
	}

	public static class IMatchableConstants
	{
	}
}
