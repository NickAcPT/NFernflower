// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler
{
	public class StatEdge
	{
		public const int Type_Regular = 1;

		public const int Type_Exception = 2;

		public const int Type_Break = 4;

		public const int Type_Continue = 8;

		public const int Type_Finallyexit = 32;

		public static readonly int[] Types = new int[] { Type_Regular, Type_Exception, Type_Break
			, Type_Continue, Type_Finallyexit };

		private int type;

		private Statement source;

		private Statement destination;

		private List<string> exceptions;

		public Statement closure;

		public bool labeled = true;

		public bool @explicit = true;

		public StatEdge(int type, Statement source, Statement destination, Statement closure
			)
			: this(type, source, destination)
		{
			this.closure = closure;
		}

		public StatEdge(int type, Statement source, Statement destination)
		{
			this.type = type;
			this.source = source;
			this.destination = destination;
		}

		public StatEdge(Statement source, Statement destination, List<string> exceptions
			)
			: this(Type_Exception, source, destination)
		{
			if (exceptions != null)
			{
				this.exceptions = new List<string>(exceptions);
			}
		}

		public virtual int GetType()
		{
			return type;
		}

		public virtual void SetType(int type)
		{
			this.type = type;
		}

		public virtual Statement GetSource()
		{
			return source;
		}

		public virtual void SetSource(Statement source)
		{
			this.source = source;
		}

		public virtual Statement GetDestination()
		{
			return destination;
		}

		public virtual void SetDestination(Statement destination)
		{
			this.destination = destination;
		}

		public virtual List<string> GetExceptions()
		{
			return this.exceptions;
		}
		//	public void setException(String exception) {
		//		this.exception = exception;
		//	}
	}
}
