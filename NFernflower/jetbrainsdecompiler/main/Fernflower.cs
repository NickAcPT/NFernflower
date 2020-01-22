// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Util;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Modules.Renamer;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Lazy;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main
{
	public class Fernflower : IIDecompiledData
	{
		private readonly StructContext structContext;

		private readonly ClassesProcessor classProcessor;

		private readonly IIdentifierRenamer helper;

		private readonly IdentifierConverter converter;

		public Fernflower(IIBytecodeProvider provider, IIResultSaver saver, Dictionary<string
			, object> customProperties, IFernflowerLogger logger)
		{
			Dictionary<string, object> properties = new Dictionary<string, object>(IFernflowerPreferences.Defaults);
			if (customProperties != null)
			{
				Sharpen.Collections.PutAll(properties, customProperties);
			}
			string level = (string)properties.GetOrNull(IFernflowerPreferences.Log_Level);
			if (level != null)
			{
				try
				{
					logger.SetSeverity(IFernflowerLogger.Severity.ValueOf(level.ToUpper()));
				}
				catch (ArgumentException)
				{
				}
			}
			structContext = new StructContext(saver, this, new LazyLoader(provider));
			classProcessor = new ClassesProcessor(structContext);
			PoolInterceptor interceptor = null;
			if ("1".Equals(properties.GetOrNull(IFernflowerPreferences.Rename_Entities)))
			{
				helper = LoadHelper((string)properties.GetOrNull(IFernflowerPreferences.User_Renamer_Class
					), logger);
				interceptor = new PoolInterceptor();
				converter = new IdentifierConverter(structContext, helper, interceptor);
			}
			else
			{
				helper = null;
				converter = null;
			}
			DecompilerContext context = new DecompilerContext(properties, logger, structContext
				, classProcessor, interceptor);
			DecompilerContext.SetCurrentContext(context);
		}

		private static IIdentifierRenamer LoadHelper(string className, IFernflowerLogger
			 logger)
		{
			if (className != null)
			{
				try
				{
					return (IIdentifierRenamer) Activator.CreateInstance(AppDomain.CurrentDomain.GetAssemblies().SelectMany(c => c.GetTypes()).First(c => c.Name == className));
				}
				catch (Exception e)
				{
					logger.WriteMessage("Cannot load renamer '" + className + "'", IFernflowerLogger.Severity
						.Warn, e);
				}
			}
			return new ConverterHelper();
		}

		public virtual void AddSource(FileSystemInfo source)
		{
			structContext.AddSpace(source, true);
		}

		public virtual void AddLibrary(FileSystemInfo library)
		{
			structContext.AddSpace(library, false);
		}

		public virtual void DecompileContext()
		{
			if (converter != null)
			{
				converter.Rename();
			}
			classProcessor.LoadClasses(helper);
			structContext.SaveContext();
		}

		public virtual void ClearContext()
		{
			DecompilerContext.SetCurrentContext(null);
		}

		public virtual string GetClassEntryName(StructClass cl, string entryName)
		{
			ClassesProcessor.ClassNode node = classProcessor.GetMapRootClasses().GetOrNull(cl
				.qualifiedName);
			if (node.type != ClassesProcessor.ClassNode.Class_Root)
			{
				return null;
			}
			else if (converter != null)
			{
				string simpleClassName = Sharpen.Runtime.Substring(cl.qualifiedName, cl.qualifiedName
					.LastIndexOf('/') + 1);
				return Sharpen.Runtime.Substring(entryName, 0, entryName.LastIndexOf('/') + 1) + 
					simpleClassName + ".java";
			}
			else
			{
				return Sharpen.Runtime.Substring(entryName, 0, entryName.LastIndexOf(".class")) +
					 ".java";
			}
		}

		public virtual string GetClassContent(StructClass cl)
		{
			try
			{
				TextBuffer buffer = new TextBuffer(ClassesProcessor.Average_Class_Size);
				buffer.Append(DecompilerContext.GetProperty(IFernflowerPreferences.Banner).ToString
					());
				classProcessor.WriteClass(cl, buffer);
				return buffer.ToString();
			}
			catch (Exception t)
			{
				DecompilerContext.GetLogger().WriteMessage("Class " + cl.qualifiedName + " couldn't be fully decompiled."
					, t);
				return null;
			}
		}
	}
}
