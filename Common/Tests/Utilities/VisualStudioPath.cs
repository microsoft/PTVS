// Visual Studio Shared Project
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

namespace TestUtilities
{
	public static class VisualStudioPath
	{
		private static Lazy<string> RootLazy { get; } = new Lazy<string>(GetVsRoot);
		private static Lazy<string> CommonExtensionsLazy { get; } = new Lazy<string>(() => Root == null ? null : Path.Combine(Root, @"CommonExtensions\"));
		private static Lazy<string> PrivateAssembliesLazy { get; } = new Lazy<string>(() => Root == null ? null : Path.Combine(Root, @"PrivateAssemblies\"));
		private static Lazy<string> PublicAssembliesLazy { get; } = new Lazy<string>(() => Root == null ? null : Path.Combine(Root, @"PublicAssemblies\"));

		public static string Root => RootLazy.Value;
		public static string CommonExtensions => CommonExtensionsLazy.Value;
		public static string PrivateAssemblies => PrivateAssembliesLazy.Value;
		public static string PublicAssemblies => PublicAssembliesLazy.Value;

		private static string GetVsRoot()
		{
			try
			{
				var configuration = (ISetupConfiguration2)new SetupConfiguration();
				var current = (ISetupInstance2)configuration.GetInstanceForCurrentProcess();
				var path = current.ResolvePath(current.GetProductPath());
				return Path.GetDirectoryName(path);
			}
			catch (COMException)
			{
				var path = Environment.GetEnvironmentVariable($"VisualStudio_IDE_{AssemblyVersionInfo.VSVersion}");
				if (string.IsNullOrEmpty(path))
				{
					path = Environment.GetEnvironmentVariable("VisualStudio_IDE");
				}
				return path;
			}
		}
	}
}
