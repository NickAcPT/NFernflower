// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using Java.Util.Jar;
using Sharpen;

namespace JetBrainsDecompiler.Main.Extern
{
	public interface IIResultSaver
	{
		void SaveFolder(string path);

		void CopyFile(string source, string path, string entryName);

		void SaveClassFile(string path, string qualifiedName, string entryName, string content
			, int[] mapping);

		void CreateArchive(string path, string archiveName, Manifest manifest);

		void SaveDirEntry(string path, string archiveName, string entryName);

		void CopyEntry(string source, string path, string archiveName, string entry);

		void SaveClassEntry(string path, string archiveName, string qualifiedName, string
			 entryName, string content);

		void CloseArchive(string path, string archiveName);
	}
}
