/*
* Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
*/
using System.Collections.Generic;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class MonitorExprent : Exprent
	{
		public const int Monitor_Enter = 0;

		public const int Monitor_Exit = 1;

		private readonly int monType;

		private Exprent value;

		public MonitorExprent(int monType, Exprent value, HashSet<int> bytecodeOffsets)
			: base(Exprent_Monitor)
		{
			this.monType = monType;
			this.value = value;
			AddBytecodeOffsets(bytecodeOffsets);
		}

		public override Exprent Copy()
		{
			return new MonitorExprent(monType, value.Copy(), bytecode);
		}

		public override List<Exprent> GetAllExprents()
		{
			List<Exprent> lst = new List<Exprent>();
			lst.Add(value);
			return lst;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			tracer.AddMapping(bytecode);
			if (monType == Monitor_Enter)
			{
				return value.ToJava(indent, tracer).Enclose("synchronized(", ")");
			}
			else
			{
				return new TextBuffer();
			}
		}

		public override void ReplaceExprent(Exprent oldExpr, Exprent newExpr)
		{
			if (oldExpr == value)
			{
				value = newExpr;
			}
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is MonitorExprent))
			{
				return false;
			}
			MonitorExprent me = (MonitorExprent)o;
			return monType == me.GetMonType() && InterpreterUtil.EqualObjects(value, me.GetValue
				());
		}

		public virtual int GetMonType()
		{
			return monType;
		}

		public virtual Exprent GetValue()
		{
			return value;
		}
	}
}
