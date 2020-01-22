// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.IO;
using Java.IO;
using Java.Nio.Charset;
using Java.Util.Jar;
using Java.Util.Zip;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main.Decompiler
{
	public class ConsoleDecompiler : IIBytecodeProvider, IIResultSaver
	{
		public static void Main(string[] args)
		{
			if (args.Length < 2)
			{
				System.Console.Out.WriteLine("Usage: java -jar fernflower.jar [-<option>=<value>]* [<source>]+ <destination>\n"
					 + "Example: java -jar fernflower.jar -dgs=true c:\\my\\source\\ c:\\my.jar d:\\decompiled\\"
					);
				return;
			}
			IDictionary<string, object> mapOptions = new Dictionary<string, object>();
			List<File> sources = new List<File>();
			List<File> libraries = new List<File>();
			bool isOption = true;
			for (int i = 0; i < args.Length - 1; ++i)
			{
				// last parameter - destination
				string arg = args[i];
				if (isOption && arg.Length > 5 && arg[0] == '-' && arg[4] == '=')
				{
					string value = Sharpen.Runtime.Substring(arg, 5);
					if (Sharpen.Runtime.EqualsIgnoreCase("true", value))
					{
						value = "1";
					}
					else if (Sharpen.Runtime.EqualsIgnoreCase("false", value))
					{
						value = "0";
					}
					Sharpen.Collections.Put(mapOptions, Sharpen.Runtime.Substring(arg, 1, 4), value);
				}
				else
				{
					isOption = false;
					if (arg.StartsWith("-e="))
					{
						AddPath(libraries, Sharpen.Runtime.Substring(arg, 3));
					}
					else
					{
						AddPath(sources, arg);
					}
				}
			}
			if ((sources.Count == 0))
			{
				System.Console.Out.WriteLine("error: no sources given");
				return;
			}
			File destination = new File(args[args.Length - 1]);
			if (!destination.IsDirectory())
			{
				System.Console.Out.WriteLine("error: destination '" + destination + "' is not a directory"
					);
				return;
			}
			PrintStreamLogger logger = new PrintStreamLogger(System.Console.Out);
			ConsoleDecompiler decompiler = new ConsoleDecompiler(destination, mapOptions, logger
				);
			foreach (File library in libraries)
			{
				decompiler.AddLibrary(library);
			}
			foreach (File source in sources)
			{
				decompiler.AddSource(source);
			}
			decompiler.DecompileContext();
		}

		private static void AddPath<_T0>(List<_T0> list, string path)
		{
			File file = new File(path);
			if (file.Exists())
			{
				list.Add(file);
			}
			else
			{
				System.Console.Out.WriteLine("warn: missing '" + path + "', ignored");
			}
		}

		private readonly File root;

		private readonly Fernflower engine;

		private readonly IDictionary<string, ZipOutputStream> mapArchiveStreams = new Dictionary
			<string, ZipOutputStream>();

		private readonly IDictionary<string, HashSet<string>> mapArchiveEntries = new Dictionary
			<string, HashSet<string>>();

		protected internal ConsoleDecompiler(File destination, IDictionary<string, object
			> options, IFernflowerLogger logger)
		{
			// *******************************************************************
			// Implementation
			// *******************************************************************
			root = destination;
			engine = new Fernflower(this, this, options, logger);
		}

		public virtual void AddSource(File source)
		{
			engine.AddSource(source);
		}

		public virtual void AddLibrary(File library)
		{
			engine.AddLibrary(library);
		}

		public virtual void DecompileContext()
		{
			try
			{
				engine.DecompileContext();
			}
			finally
			{
				engine.ClearContext();
			}
		}

		// *******************************************************************
		// Interface IBytecodeProvider
		// *******************************************************************
		/// <exception cref="System.IO.IOException"/>
		public virtual byte[] GetBytecode(string externalPath, string internalPath)
		{
			File file = new File(externalPath);
			if (internalPath == null)
			{
				return InterpreterUtil.GetBytes(file);
			}
			else
			{
				using (ZipFile archive = new ZipFile(file))
				{
					ZipEntry entry = archive.GetEntry(internalPath);
					if (entry == null)
					{
						throw new IOException("Entry not found: " + internalPath);
					}
					return InterpreterUtil.GetBytes(archive, entry);
				}
			}
		}

		// *******************************************************************
		// Interface IResultSaver
		// *******************************************************************
		private string GetAbsolutePath(string path)
		{
			return new File(root, path).GetAbsolutePath();
		}

		public virtual void SaveFolder(string path)
		{
			File dir = new File(GetAbsolutePath(path));
			if (!(dir.Mkdirs() || dir.IsDirectory()))
			{
				throw new Exception("Cannot create directory " + dir);
			}
		}

		public virtual void CopyFile(string source, string path, string entryName)
		{
			try
			{
				InterpreterUtil.CopyFile(new File(source), new File(GetAbsolutePath(path), entryName
					));
			}
			catch (IOException ex)
			{
				DecompilerContext.GetLogger().WriteMessage("Cannot copy " + source + " to " + entryName
					, ex);
			}
		}

		public virtual void SaveClassFile(string path, string qualifiedName, string entryName
			, string content, int[] mapping)
		{
			File file = new File(GetAbsolutePath(path), entryName);
			try
			{
				using (TextWriter @out = new OutputStreamWriter(new FileOutputStream(file), StandardCharsets
					.Utf_8))
				{
					@out.Write(content);
				}
			}
			catch (IOException ex)
			{
				DecompilerContext.GetLogger().WriteMessage("Cannot write class file " + file, ex);
			}
		}

		public virtual void CreateArchive(string path, string archiveName, Manifest manifest
			)
		{
			File file = new File(GetAbsolutePath(path), archiveName);
			try
			{
				if (!(file.CreateNewFile() || file.IsFile()))
				{
					throw new IOException("Cannot create file " + file);
				}
				FileOutputStream fileStream = new FileOutputStream(file);
				ZipOutputStream zipStream = manifest != null ? new JarOutputStream(fileStream, manifest
					) : new ZipOutputStream(fileStream);
				Sharpen.Collections.Put(mapArchiveStreams, file.GetPath(), zipStream);
			}
			catch (IOException ex)
			{
				DecompilerContext.GetLogger().WriteMessage("Cannot create archive " + file, ex);
			}
		}

		public virtual void SaveDirEntry(string path, string archiveName, string entryName
			)
		{
			SaveClassEntry(path, archiveName, null, entryName, null);
		}

		public virtual void CopyEntry(string source, string path, string archiveName, string
			 entryName)
		{
			string file = new File(GetAbsolutePath(path), archiveName).GetPath();
			if (!CheckEntry(entryName, file))
			{
				return;
			}
			try
			{
				using (ZipFile srcArchive = new ZipFile(new File(source)))
				{
					ZipEntry entry = srcArchive.GetEntry(entryName);
					if (entry != null)
					{
						using (InputStream @in = srcArchive.GetInputStream(entry))
						{
							ZipOutputStream @out = mapArchiveStreams.GetOrNull(file);
							@out.PutNextEntry(new ZipEntry(entryName));
							InterpreterUtil.CopyStream(@in, @out);
						}
					}
				}
			}
			catch (IOException ex)
			{
				string message = "Cannot copy entry " + entryName + " from " + source + " to " + 
					file;
				DecompilerContext.GetLogger().WriteMessage(message, ex);
			}
		}

		public virtual void SaveClassEntry(string path, string archiveName, string qualifiedName
			, string entryName, string content)
		{
			string file = new File(GetAbsolutePath(path), archiveName).GetPath();
			if (!CheckEntry(entryName, file))
			{
				return;
			}
			try
			{
				ZipOutputStream @out = mapArchiveStreams.GetOrNull(file);
				@out.PutNextEntry(new ZipEntry(entryName));
				if (content != null)
				{
					@out.Write(Sharpen.Runtime.GetBytesForString(content, StandardCharsets.Utf_8));
				}
			}
			catch (IOException ex)
			{
				string message = "Cannot write entry " + entryName + " to " + file;
				DecompilerContext.GetLogger().WriteMessage(message, ex);
			}
		}

		private bool CheckEntry(string entryName, string file)
		{
			HashSet<string> set = mapArchiveEntries.ComputeIfAbsent(file, (string k) => new HashSet
				<string>());
			bool added = set.Add(entryName);
			if (!added)
			{
				string message = "Zip entry " + entryName + " already exists in " + file;
				DecompilerContext.GetLogger().WriteMessage(message, IFernflowerLogger.Severity.Warn
					);
			}
			return added;
		}

		public virtual void CloseArchive(string path, string archiveName)
		{
			string file = new File(GetAbsolutePath(path), archiveName).GetPath();
			try
			{
				Sharpen.Collections.Remove(mapArchiveEntries, file);
				Sharpen.Collections.Remove(mapArchiveStreams, file).Close();
			}
			catch (IOException)
			{
				DecompilerContext.GetLogger().WriteMessage("Cannot close " + file, IFernflowerLogger.Severity
					.Warn);
			}
		}
	}
}
