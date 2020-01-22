// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using System.Threading;
using Java.Util;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Modules.Renamer;
using JetBrainsDecompiler.Struct;
using Sharpen;

namespace JetBrainsDecompiler.Main
{
	public class DecompilerContext
	{
		public const string Current_Class = "CURRENT_CLASS";

		public const string Current_Class_Wrapper = "CURRENT_CLASS_WRAPPER";

		public const string Current_Class_Node = "CURRENT_CLASS_NODE";

		public const string Current_Method_Wrapper = "CURRENT_METHOD_WRAPPER";

		private readonly Dictionary<string, object> properties;

		private readonly IFernflowerLogger logger;

		private readonly StructContext structContext;

		private readonly ClassesProcessor classProcessor;

		private readonly PoolInterceptor poolInterceptor;

		private ImportCollector importCollector;

		private VarProcessor varProcessor;

		private CounterContainer counterContainer;

		private BytecodeSourceMapper bytecodeSourceMapper;

		public DecompilerContext(Dictionary<string, object> properties, IFernflowerLogger
			 logger, StructContext structContext, ClassesProcessor classProcessor, PoolInterceptor
			 interceptor)
		{
			this.properties = properties;
			this.logger = logger;
			this.structContext = structContext;
			this.classProcessor = classProcessor;
			this.poolInterceptor = interceptor;
			this.counterContainer = new CounterContainer();
		}

		private static readonly ThreadLocal<DecompilerContext> currentContext = new ThreadLocal
			<DecompilerContext>();

		// *****************************************************************************
		// context setup and update
		// *****************************************************************************
		public static DecompilerContext GetCurrentContext()
		{
			return currentContext.Value;
		}

		public static void SetCurrentContext(DecompilerContext context)
		{
			currentContext.Value = context;
		}

		public static void SetProperty(string key, object value)
		{
			Sharpen.Collections.Put(GetCurrentContext().properties, key, value);
		}

		public static void StartClass(ImportCollector importCollector)
		{
			DecompilerContext context = GetCurrentContext();
			context.importCollector = importCollector;
			context.counterContainer = new CounterContainer();
			context.bytecodeSourceMapper = new BytecodeSourceMapper();
		}

		public static void StartMethod(VarProcessor varProcessor)
		{
			DecompilerContext context = GetCurrentContext();
			context.varProcessor = varProcessor;
			context.counterContainer = new CounterContainer();
		}

		// *****************************************************************************
		// context access
		// *****************************************************************************
		public static object GetProperty(string key)
		{
			return GetCurrentContext().properties.GetOrNull(key);
		}

		public static bool GetOption(string key)
		{
			return "1".Equals(GetProperty(key));
		}

		public static string GetNewLineSeparator()
		{
			return GetOption(IFernflowerPreferences.New_Line_Separator) ? IFernflowerPreferences
				.Line_Separator_Unx : IFernflowerPreferences.Line_Separator_Win;
		}

		public static IFernflowerLogger GetLogger()
		{
			return GetCurrentContext().logger;
		}

		public static StructContext GetStructContext()
		{
			return GetCurrentContext().structContext;
		}

		public static ClassesProcessor GetClassProcessor()
		{
			return GetCurrentContext().classProcessor;
		}

		public static PoolInterceptor GetPoolInterceptor()
		{
			return GetCurrentContext().poolInterceptor;
		}

		public static ImportCollector GetImportCollector()
		{
			return GetCurrentContext().importCollector;
		}

		public static VarProcessor GetVarProcessor()
		{
			return GetCurrentContext().varProcessor;
		}

		public static CounterContainer GetCounterContainer()
		{
			return GetCurrentContext().counterContainer;
		}

		public static BytecodeSourceMapper GetBytecodeSourceMapper()
		{
			return GetCurrentContext().bytecodeSourceMapper;
		}
	}
}
