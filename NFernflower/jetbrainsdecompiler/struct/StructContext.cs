// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using System.IO;
using Java.IO;
using Java.Util;
using Java.Util.Jar;
using Java.Util.Zip;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Struct.Lazy;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct
{
	public class StructContext
	{
		private readonly IIResultSaver saver;

		private readonly IIDecompiledData decompiledData;

		private readonly LazyLoader loader;

		private readonly IDictionary<string, ContextUnit> units = new Dictionary<string, 
			ContextUnit>();

		private readonly IDictionary<string, StructClass> classes = new Dictionary<string
			, StructClass>();

		public StructContext(IIResultSaver saver, IIDecompiledData decompiledData, LazyLoader
			 loader)
		{
			this.saver = saver;
			this.decompiledData = decompiledData;
			this.loader = loader;
			ContextUnit defaultUnit = new ContextUnit(ContextUnit.Type_Folder, null, string.Empty
				, true, saver, decompiledData);
			Sharpen.Collections.Put(units, string.Empty, defaultUnit);
		}

		public virtual StructClass GetClass(string name)
		{
			return classes.GetOrNull(name);
		}

		/// <exception cref="System.IO.IOException"/>
		public virtual void ReloadContext()
		{
			foreach (ContextUnit unit in units.Values)
			{
				foreach (StructClass cl in unit.GetClasses())
				{
					Sharpen.Collections.Remove(classes, cl.qualifiedName);
				}
				unit.Reload(loader);
				// adjust global class collection
				foreach (StructClass cl in unit.GetClasses())
				{
					Sharpen.Collections.Put(classes, cl.qualifiedName, cl);
				}
			}
		}

		public virtual void SaveContext()
		{
			foreach (ContextUnit unit in units.Values)
			{
				if (unit.IsOwn())
				{
					unit.Save();
				}
			}
		}

		public virtual void AddSpace(File file, bool isOwn)
		{
			AddSpace(string.Empty, file, isOwn, 0);
		}

		private void AddSpace(string path, File file, bool isOwn, int level)
		{
			if (file.IsDirectory())
			{
				if (level == 1)
				{
					path += file.GetName();
				}
				else if (level > 1)
				{
					path += "/" + file.GetName();
				}
				File[] files = file.ListFiles();
				if (files != null)
				{
					for (int i = files.Length - 1; i >= 0; i--)
					{
						AddSpace(path, files[i], isOwn, level + 1);
					}
				}
			}
			else
			{
				string filename = file.GetName();
				bool isArchive = false;
				try
				{
					if (filename.EndsWith(".jar"))
					{
						isArchive = true;
						AddArchive(path, file, ContextUnit.Type_Jar, isOwn);
					}
					else if (filename.EndsWith(".zip"))
					{
						isArchive = true;
						AddArchive(path, file, ContextUnit.Type_Zip, isOwn);
					}
				}
				catch (IOException ex)
				{
					string message = "Corrupted archive file: " + file;
					DecompilerContext.GetLogger().WriteMessage(message, ex);
				}
				if (isArchive)
				{
					return;
				}
				ContextUnit unit = units.GetOrNull(path);
				if (unit == null)
				{
					unit = new ContextUnit(ContextUnit.Type_Folder, null, path, isOwn, saver, decompiledData
						);
					Sharpen.Collections.Put(units, path, unit);
				}
				if (filename.EndsWith(".class"))
				{
					try
					{
						using (DataInputFullStream @in = loader.GetClassStream(file.GetAbsolutePath(), null
							))
						{
							StructClass cl = new StructClass(@in, isOwn, loader);
							Sharpen.Collections.Put(classes, cl.qualifiedName, cl);
							unit.AddClass(cl, filename);
							loader.AddClassLink(cl.qualifiedName, new LazyLoader.Link(file.GetAbsolutePath(), 
								null));
						}
					}
					catch (IOException ex)
					{
						string message = "Corrupted class file: " + file;
						DecompilerContext.GetLogger().WriteMessage(message, ex);
					}
				}
				else
				{
					unit.AddOtherEntry(file.GetAbsolutePath(), filename);
				}
			}
		}

		/// <exception cref="System.IO.IOException"/>
		private void AddArchive(string path, File file, int type, bool isOwn)
		{
			using (ZipFile archive = type == ContextUnit.Type_Jar ? new JarFile(file) : new ZipFile
				(file))
			{
				var entries = archive.Entries();
				while (entries.MoveNext())
				{
					var entry = entries.Current;
					ContextUnit unit = units.GetOrNull(path + "/" + file.GetName());
					if (unit == null)
					{
						unit = new ContextUnit(type, path, file.GetName(), isOwn, saver, decompiledData);
						if (type == ContextUnit.Type_Jar)
						{
							unit.SetManifest(((JarFile)archive).GetManifest());
						}
						Sharpen.Collections.Put(units, path + "/" + file.GetName(), unit);
					}
					string name = entry.GetName();
					if (!entry.IsDirectory())
					{
						if (name.EndsWith(".class"))
						{
							byte[] bytes = InterpreterUtil.GetBytes(archive, entry);
							StructClass cl = new StructClass(bytes, isOwn, loader);
							Sharpen.Collections.Put(classes, cl.qualifiedName, cl);
							unit.AddClass(cl, name);
							loader.AddClassLink(cl.qualifiedName, new LazyLoader.Link(file.GetAbsolutePath(), 
								name));
						}
						else
						{
							unit.AddOtherEntry(file.GetAbsolutePath(), name);
						}
					}
					else
					{
						unit.AddDirEntry(name);
					}
				}
			}
		}

		public virtual IDictionary<string, StructClass> GetClasses()
		{
			return classes;
		}
	}
}
