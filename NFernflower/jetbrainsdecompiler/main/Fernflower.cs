// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using Java.IO;
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

		private readonly IIIdentifierRenamer helper;

		private readonly IdentifierConverter converter;

		public Fernflower(IIBytecodeProvider provider, IIResultSaver saver, IDictionary<string
			, object> customProperties, IFernflowerLogger logger)
		{
			IDictionary<string, object> properties = new Dictionary<string, object>(IIFernflowerPreferences
				.Defaults);
			if (customProperties != null)
			{
				Sharpen.Collections.PutAll(properties, customProperties);
			}
			string level = (string)properties.GetOrNull(IIFernflowerPreferences.Log_Level);
			if (level != null)
			{
				try
				{
					logger.SetSeverity(IFernflowerLogger.Severity.ValueOf(level.ToUpper(Locale.English
						)));
				}
				catch (ArgumentException)
				{
				}
			}
			structContext = new StructContext(saver, this, new LazyLoader(provider));
			classProcessor = new ClassesProcessor(structContext);
			PoolInterceptor interceptor = null;
			if ("1".Equals(properties.GetOrNull(IIFernflowerPreferences.Rename_Entities)))
			{
				helper = LoadHelper((string)properties.GetOrNull(IIFernflowerPreferences.User_Renamer_Class
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

		private static IIIdentifierRenamer LoadHelper(string className, IFernflowerLogger
			 logger)
		{
			if (className != null)
			{
				try
				{
					Type renamerClass = typeof(Fernflower).GetClassLoader().LoadClass(className);
					return (IIIdentifierRenamer)renamerClass.GetDeclaredConstructor().NewInstance();
				}
				catch (Exception e)
				{
					logger.WriteMessage("Cannot load renamer '" + className + "'", IFernflowerLogger.Severity
						.Warn, e);
				}
			}
			return new ConverterHelper();
		}

		public virtual void AddSource(File source)
		{
			structContext.AddSpace(source, true);
		}

		public virtual void AddLibrary(File library)
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
				buffer.Append(DecompilerContext.GetProperty(IIFernflowerPreferences.Banner).ToString
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
