// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Java.Util.Jar;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Util;
using ObjectWeb.Misc.Java.IO;
using ObjectWeb.Misc.Java.Nio;
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
			Dictionary<string, object> mapOptions = new Dictionary<string, object>();
			List<FileSystemInfo> sources = new List<FileSystemInfo>();
			List<FileSystemInfo> libraries = new List<FileSystemInfo>();
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
			FileSystemInfo destination = new DirectoryInfo(args[args.Length - 1]);
			 // TODO: Add this check back
			if (!destination.Exists)
			{
				System.Console.Out.WriteLine("error: destination '" + destination + "' is not a directory"
					);
				return;
			}
			PrintStreamLogger logger = new PrintStreamLogger(System.Console.Out);
			ConsoleDecompiler decompiler = new ConsoleDecompiler(destination, mapOptions, logger
				);
			foreach (FileSystemInfo library in libraries)
			{
				decompiler.AddLibrary(library);
			}
			foreach (FileSystemInfo source in sources)
			{
				decompiler.AddSource(source);
			}
			decompiler.DecompileContext();
		}

		private static void AddPath(List<FileSystemInfo> list, string path)
		{
			FileSystemInfo file = new FileInfo(path);
			if (file.Exists)
			{
				list.Add(file);
			}
			else
			{
				FileSystemInfo dir = new FileInfo(path);
				if (dir.Exists)
				{
					list.Add(dir);
				}
				else
				{
					System.Console.Out.WriteLine("warn: missing '" + path + "', ignored");
				}
			}
		}

		private readonly FileSystemInfo root;

		private readonly Fernflower engine;

		private readonly Dictionary<string, ZipArchive> mapArchiveStreams = new Dictionary
			<string, ZipArchive>();

		private readonly Dictionary<string, HashSet<string>> mapArchiveEntries = new Dictionary
			<string, HashSet<string>>();

		protected internal ConsoleDecompiler(FileSystemInfo destination, Dictionary<string, object
			> options, IFernflowerLogger logger)
		{
			// *******************************************************************
			// Implementation
			// *******************************************************************
			root = destination;
			engine = new Fernflower(this, this, options, logger);
		}

		public virtual void AddSource(FileSystemInfo source)
		{
			engine.AddSource(source);
		}

		public virtual void AddLibrary(FileSystemInfo library)
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
		/// <exception cref="IOException"/>
		public virtual byte[] GetBytecode(string externalPath, string internalPath)
		{
			var file = new FileInfo(externalPath);
			if (internalPath == null)
			{
				return InterpreterUtil.GetBytes(file);
			}
			else
			{
				using (var archive = ZipFile.Open(file.FullName, ZipArchiveMode.Read))
				{
					var entry = archive.GetEntry(internalPath);
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
			return Path.GetFullPath(Path.Combine(root.FullName, path));
		}

		public virtual void SaveFolder(string path)
		{
			DirectoryInfo dir = new DirectoryInfo(GetAbsolutePath(path));
			if (!(dir.Exists))
			{
				try
				{
					dir.Create();
				}
				catch (Exception e)
				{
					throw new Exception("Cannot create directory " + dir);
				}
			}
		}

		public virtual void CopyFile(string source, string path, string entryName)
		{
			try
			{
				InterpreterUtil.CopyFile(new FileInfo(source), new FileInfo(Path.Combine(GetAbsolutePath(path), entryName)));
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
			FileSystemInfo file = new FileInfo(Path.Combine(GetAbsolutePath(path), entryName));
			try
			{
				File.WriteAllText(file.FullName, content);
			}
			catch (IOException ex)
			{
				DecompilerContext.GetLogger().WriteMessage("Cannot write class file " + file, ex);
			}
		}

		public virtual void CreateArchive(string path, string archiveName, Manifest manifest
			)
		{
			FileSystemInfo file = new FileInfo(Path.Combine(GetAbsolutePath(path), archiveName));
			try
			{

				Sharpen.Collections.Put(mapArchiveStreams, file.FullName, ZipFile.Open(file.FullName, ZipArchiveMode.Update));
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
			string file = Path.Combine(GetAbsolutePath(path), archiveName);
			if (!CheckEntry(entryName, file))
			{
				return;
			}
			try
			{
				using (ZipArchive srcArchive = ZipFile.Open(source, ZipArchiveMode.Read))
				{
					var entry = srcArchive.GetEntry(entryName);
					if (entry != null)
					{
						using (var @in = new MemoryStream(entry.Open().ReadFully()).ToInputStream())
						{
							var @out = mapArchiveStreams.GetOrNull(file);
							var newEntry = @out.CreateEntry(entryName);
							InterpreterUtil.CopyStream(@in, newEntry.Open());
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
			string file = Path.Combine(GetAbsolutePath(path), archiveName);
			if (!CheckEntry(entryName, file))
			{
				return;
			}
			try
			{
				var @out = mapArchiveStreams.GetOrNull(file);
				var newEntry = @out.CreateEntry(entryName);
				if (content != null)
				{
					var stream = newEntry.Open();
					stream.Write(Sharpen.Runtime.GetBytesForString(content, "UTF-8"));
					stream.Flush();
					stream.Close();
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
			string file = new FileInfo(Path.Combine(GetAbsolutePath(path), archiveName)).FullName;
			try
			{
				Sharpen.Collections.Remove(mapArchiveEntries, file);
				Sharpen.Collections.Remove(mapArchiveStreams, file).Dispose();
			}
			catch (IOException)
			{
				DecompilerContext.GetLogger().WriteMessage("Cannot close " + file, IFernflowerLogger.Severity
					.Warn);
			}
		}
	}
}
