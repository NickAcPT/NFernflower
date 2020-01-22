// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Java.Util.Jar;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Struct.Lazy;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct
{
	public class ContextUnit
	{
		public const int Type_Folder = 0;

		public const int Type_Jar = 1;

		public const int Type_Zip = 2;

		private readonly int type;

		private readonly bool own;

		private readonly string archivePath;

		private readonly string filename;

		private readonly IIResultSaver resultSaver;

		private readonly IIDecompiledData decompiledData;

		private readonly List<string> classEntries = new List<string>();

		private readonly List<string> dirEntries = new List<string>();

		private readonly List<string[]> otherEntries = new List<string[]>();

		private List<StructClass> classes = new List<StructClass>();

		private Manifest manifest;

		public ContextUnit(int type, string archivePath, string filename, bool own, IIResultSaver
			 resultSaver, IIDecompiledData decompiledData)
		{
			// relative path to jar/zip
			// folder: relative path, archive: file name
			// class file or jar/zip entry
			this.type = type;
			this.own = own;
			this.archivePath = archivePath;
			this.filename = filename;
			this.resultSaver = resultSaver;
			this.decompiledData = decompiledData;
		}

		public virtual void AddClass(StructClass cl, string entryName)
		{
			classes.Add(cl);
			classEntries.Add(entryName);
		}

		public virtual void AddDirEntry(string entry)
		{
			dirEntries.Add(entry);
		}

		public virtual void AddOtherEntry(string fullPath, string entry)
		{
			otherEntries.Add(new string[] { fullPath, entry });
		}

		/// <exception cref="System.IO.IOException"/>
		public virtual void Reload(LazyLoader loader)
		{
			List<StructClass> lstClasses = new List<StructClass>();
			foreach (StructClass cl in classes)
			{
				string oldName = cl.qualifiedName;
				StructClass newCl;
				using (DataInputFullStream @in = loader.GetClassStream(oldName))
				{
					newCl = new StructClass(@in, cl.IsOwn(), loader);
				}
				lstClasses.Add(newCl);
				LazyLoader.Link lnk = loader.GetClassLink(oldName);
				loader.RemoveClassLink(oldName);
				loader.AddClassLink(newCl.qualifiedName, lnk);
			}
			classes = lstClasses;
		}

		public virtual void Save()
		{
			switch (type)
			{
				case Type_Folder:
				{
					// create folder
					resultSaver.SaveFolder(filename);
					// non-class files
					foreach (string[] pair in otherEntries)
					{
						resultSaver.CopyFile(pair[0], filename, pair[1]);
					}
					// classes
					for (int i = 0; i < classes.Count; i++)
					{
						StructClass cl = classes[i];
						string entryName = decompiledData.GetClassEntryName(cl, classEntries[i]);
						if (entryName != null)
						{
							string content = decompiledData.GetClassContent(cl);
							if (content != null)
							{
								int[] mapping = null;
								if (DecompilerContext.GetOption(IIFernflowerPreferences.Bytecode_Source_Mapping))
								{
									mapping = DecompilerContext.GetBytecodeSourceMapper().GetOriginalLinesMapping();
								}
								resultSaver.SaveClassFile(filename, cl.qualifiedName, entryName, content, mapping
									);
							}
						}
					}
					break;
				}

				case Type_Jar:
				case Type_Zip:
				{
					// create archive file
					resultSaver.SaveFolder(archivePath);
					resultSaver.CreateArchive(archivePath, filename, manifest);
					// directory entries
					foreach (string dirEntry in dirEntries)
					{
						resultSaver.SaveDirEntry(archivePath, filename, dirEntry);
					}
					// non-class entries
					foreach (string[] pair in otherEntries)
					{
						if (type != Type_Jar || !Sharpen.Runtime.EqualsIgnoreCase(JarFile.Manifest_Name, 
							pair[1]))
						{
							resultSaver.CopyEntry(pair[0], archivePath, filename, pair[1]);
						}
					}
					// classes
					for (int i = 0; i < classes.Count; i++)
					{
						StructClass cl = classes[i];
						string entryName = decompiledData.GetClassEntryName(cl, classEntries[i]);
						if (entryName != null)
						{
							string content = decompiledData.GetClassContent(cl);
							resultSaver.SaveClassEntry(archivePath, filename, cl.qualifiedName, entryName, content
								);
						}
					}
					resultSaver.CloseArchive(archivePath, filename);
					break;
				}
			}
		}

		public virtual void SetManifest(Manifest manifest)
		{
			this.manifest = manifest;
		}

		public virtual bool IsOwn()
		{
			return own;
		}

		public virtual List<StructClass> GetClasses()
		{
			return classes;
		}
	}
}
