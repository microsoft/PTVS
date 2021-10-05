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

namespace Microsoft.IronPythonTools.Interpreter
{
	internal class IronPythonResolver
	{
		private readonly string _installDir;

		public IronPythonResolver(string installDir)
		{
			_installDir = installDir;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001")]
		public Assembly domain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			var asmName = new AssemblyName(args.Name);
			var asmPath = Path.Combine(_installDir, asmName.Name + ".dll");
			if (File.Exists(asmPath))
			{
				return Assembly.LoadFile(asmPath);
			}
			return null;
		}

		internal static void Initialize(string[] args)
		{
			if (args.Length > 0 && Directory.Exists(args[0]))
			{
				var resolver = new IronPythonResolver(args[0]);
				AppDomain.CurrentDomain.AssemblyResolve += resolver.domain_AssemblyResolve;
			}
		}

		internal static string GetPythonInstallDir()
		{
			// IronPython 2.7.7 and earlier use 32-bit registry
			var installPath = ReadInstallPathFromRegistry(RegistryView.Registry32);
			if (!string.IsNullOrEmpty(installPath))
			{
				return installPath;
			}

			// IronPython 2.7.8 and later use 64-bit registry
			installPath = ReadInstallPathFromRegistry(RegistryView.Registry64);
			if (!string.IsNullOrEmpty(installPath))
			{
				return installPath;
			}

			var paths = Environment.GetEnvironmentVariable("PATH");
			if (paths != null)
			{
				foreach (string dir in paths.Split(Path.PathSeparator))
				{
					try
					{
						if (IronPythonExistsIn(dir))
						{
							return dir;
						}
					}
					catch
					{
						// ignore
					}
				}
			}

			return null;
		}

		private static string ReadInstallPathFromRegistry(RegistryView view)
		{
			try
			{
				using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
				using (var pathKey = baseKey.OpenSubKey("SOFTWARE\\IronPython\\2.7\\InstallPath"))
				{
					return pathKey?.GetValue("") as string;
				}
			}
			catch (ArgumentException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}

			return null;
		}

		private static bool IronPythonExistsIn(string/*!*/ dir)
		{
			return File.Exists(Path.Combine(dir, "ipy.exe"));
		}
	}
}
