// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace TestRunnerInterop
{
	internal static partial class FileUtils
	{
		/// <summary>
		/// Safely enumerates all subdirectories under a given root. If a
		/// subdirectory is inaccessible, it will not be returned (compare and
		/// contrast with Directory.GetDirectories, which will crash without
		/// returning any subdirectories at all).
		/// </summary>
		/// <param name="root">
		/// Directory to enumerate under. This is not returned from this
		/// function.
		/// </param>
		/// <param name="recurse">
		/// <c>true</c> to return subdirectories of subdirectories.
		/// </param>
		/// <param name="fullPaths">
		/// <c>true</c> to return full paths for all subdirectories. Otherwise,
		/// the relative path from <paramref name="root"/> is returned.
		/// </param>
		public static IEnumerable<string> EnumerateDirectories(
			string root,
			bool recurse = true,
			bool fullPaths = true
		)
		{
			var queue = new Queue<string>();
			if (!root.EndsWith("\\"))
			{
				root += "\\";
			}
			queue.Enqueue(root);

			while (queue.Any())
			{
				var path = queue.Dequeue();
				if (!path.EndsWith("\\"))
				{
					path += "\\";
				}

				IEnumerable<string> dirs = null;
				try
				{
					dirs = Directory.GetDirectories(path);
				}
				catch (UnauthorizedAccessException)
				{
				}
				catch (IOException)
				{
				}
				if (dirs == null)
				{
					continue;
				}

				foreach (var d in dirs)
				{
					if (!fullPaths && !d.StartsWith(root, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					if (recurse)
					{
						queue.Enqueue(d);
					}
					yield return fullPaths ? d : d.Substring(root.Length);
				}
			}
		}

		/// <summary>
		/// Safely enumerates all files under a given root. If a subdirectory is
		/// inaccessible, its files will not be returned (compare and contrast
		/// with Directory.GetFiles, which will crash without returning any
		/// files at all).
		/// </summary>
		/// <param name="root">
		/// Directory to enumerate.
		/// </param>
		/// <param name="pattern">
		/// File pattern to return. You may use wildcards * and ?.
		/// </param>
		/// <param name="recurse">
		/// <c>true</c> to return files within subdirectories.
		/// </param>
		/// <param name="fullPaths">
		/// <c>true</c> to return full paths for all subdirectories. Otherwise,
		/// the relative path from <paramref name="root"/> is returned.
		/// </param>
		public static IEnumerable<string> EnumerateFiles(string root,
												   string pattern = "*",
												   bool recurse = true,
												   bool fullPaths = true)
		{
			if (!root.EndsWith("\\"))
			{
				root += "\\";
			}

			var dirs = Enumerable.Repeat(root, 1);
			if (recurse)
			{
				dirs = dirs.Concat(EnumerateDirectories(root, true, false));
			}

			foreach (var dir in dirs)
			{
				var fullDir = Path.IsPathRooted(dir) ? dir : (root + dir);
				var dirPrefix = Path.IsPathRooted(dir) ? "" : (dir.EndsWith("\\") ? dir : (dir + "\\"));

				IEnumerable<string> files = null;
				try
				{
					files = Directory.GetFiles(fullDir, pattern);
				}
				catch (UnauthorizedAccessException)
				{
				}
				catch (IOException)
				{
				}
				if (files == null)
				{
					continue;
				}

				foreach (var f in files)
				{
					if (fullPaths)
					{
						yield return f;
					}
					else
					{
						var relPath = dirPrefix + Path.GetFileName(f);
						if (File.Exists(root + relPath))
						{
							yield return relPath;
						}
					}
				}
			}
		}

		public static void CopyDirectory(string sourceDir, string destDir)
		{
			CopyDirectory(sourceDir, destDir, false);
		}

		public static void CopyDirectory(string sourceDir, string destDir, bool tryHardLinkFirst)
		{
			sourceDir = sourceDir.TrimEnd('\\');
			destDir = destDir.TrimEnd('\\');
			try
			{
				Directory.CreateDirectory(destDir);
			}
			catch (IOException)
			{
			}

			var newDirectories = new HashSet<string>(EnumerateDirectories(sourceDir, fullPaths: false), StringComparer.OrdinalIgnoreCase);
			newDirectories.ExceptWith(EnumerateDirectories(destDir, fullPaths: false));

			foreach (var newDir in newDirectories.OrderBy(i => i.Length).Select(i => Path.Combine(destDir, i)))
			{
				try
				{
					Directory.CreateDirectory(newDir);
				}
				catch
				{
					Debug.WriteLine("Failed to create directory " + newDir);
				}
			}

			var newFiles = new HashSet<string>(EnumerateFiles(sourceDir, fullPaths: false), StringComparer.OrdinalIgnoreCase);
			newFiles.ExceptWith(EnumerateFiles(destDir, fullPaths: false));

			foreach (var newFile in newFiles)
			{
				var copyFrom = Path.Combine(sourceDir, newFile);
				var copyTo = Path.Combine(destDir, newFile);

				if (tryHardLinkFirst)
				{
					if (NativeMethods.CreateHardLink(copyTo, copyFrom, IntPtr.Zero))
					{
						continue;
					}
					Debug.WriteLine("Failed to hard link " + copyFrom + " to " + copyTo + ". Trying copy");
				}

				try
				{
					File.Copy(copyFrom, copyTo);
					File.SetAttributes(copyTo, FileAttributes.Normal);
				}
				catch
				{
					Debug.WriteLine("Failed to copy " + copyFrom + " to " + copyTo);
				}
			}
		}

		public static void DeleteDirectory(string path)
		{
			Trace.TraceInformation("Removing directory: {0}", path);
			NativeMethods.RecursivelyDeleteDirectory(path, silent: true);
		}

		public static void Delete(string path)
		{
			for (int retries = 10; retries > 0 && File.Exists(path); --retries)
			{
				try
				{
					File.SetAttributes(path, FileAttributes.Normal);
					File.Delete(path);
					return;
				}
				catch (IOException)
				{
				}
				catch (UnauthorizedAccessException)
				{
				}
				Thread.Sleep(100);
			}
		}

		public static IDisposable Backup(string path)
		{
			var backup = Path.GetTempFileName();
			File.Delete(backup);
			File.Copy(path, backup);
			return new FileRestorer(path, backup);
		}

		private sealed class FileDeleter : IDisposable
		{
			private readonly string _path;

			public FileDeleter(string path)
			{
				_path = path;
			}

			public void Dispose()
			{
				for (int retries = 10; retries > 0; --retries)
				{
					try
					{
						File.Delete(_path);
						return;
					}
					catch (IOException)
					{
					}
					catch (UnauthorizedAccessException)
					{
						try
						{
							File.SetAttributes(_path, FileAttributes.Normal);
						}
						catch (IOException)
						{
						}
						catch (UnauthorizedAccessException)
						{
						}
					}
					Thread.Sleep(100);
				}
			}
		}
	}
}
