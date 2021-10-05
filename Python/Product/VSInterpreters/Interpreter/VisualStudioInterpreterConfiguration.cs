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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Interpreter
{
	public class VisualStudioInterpreterConfiguration : InterpreterConfiguration, IEquatable<VisualStudioInterpreterConfiguration>
	{
		public string PrefixPath { get; }

		/// <summary>
		/// Returns the path to the interpreter executable for launching Python
		/// applications which are windows applications (pythonw.exe, ipyw.exe).
		/// </summary>
		public string WindowsInterpreterPath { get; }

		/// <summary>
		/// The UI behavior of the interpreter.
		/// </summary>
		public InterpreterUIMode UIMode { get; }

		/// <summary>
		/// Reconstructs an interpreter configuration from a dictionary.
		/// </summary>
		public static VisualStudioInterpreterConfiguration CreateFromDictionary(Dictionary<string, object> properties)
		{
			var id = Read(properties, nameof(InterpreterConfiguration.Id));
			var description = Read(properties, nameof(InterpreterConfiguration.Description)) ?? "";
			var prefixPath = Read(properties, nameof(PrefixPath));
			var interpreterPath = Read(properties, nameof(InterpreterConfiguration.InterpreterPath));
			var windowsInterpreterPath = Read(properties, nameof(WindowsInterpreterPath));
			var pathEnvironmentVariable = Read(properties, nameof(InterpreterConfiguration.PathEnvironmentVariable));
			var architecture = InterpreterArchitecture.TryParse(Read(properties, nameof(InterpreterConfiguration.Architecture)));

			var version = default(Version);
			try
			{
				version = Version.Parse(Read(properties, nameof(Version)));
			}
			catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
			{
				version = new Version();
			}

			InterpreterUIMode uiMode = 0;
			foreach (var bit in (Read(properties, nameof(UIMode)) ?? "").Split('|'))
			{
				if (Enum.TryParse(bit, out InterpreterUIMode m))
				{
					uiMode |= m;
				}
			}

			var configuration = new VisualStudioInterpreterConfiguration(id, description, prefixPath, interpreterPath, windowsInterpreterPath, pathEnvironmentVariable, architecture, version, uiMode);

			if (properties.TryGetValue(nameof(InterpreterConfiguration.SearchPaths), out object o))
			{
				configuration.SearchPaths.Clear();
				switch (o)
				{
					case string s:
						configuration.SearchPaths.AddRange(s.Split(';'));
						break;
					case IEnumerable<string> ss:
						configuration.SearchPaths.AddRange(ss);
						break;
				}
			}

			return configuration;
		}

		private static string Read(Dictionary<string, object> d, string k)
			=> d.TryGetValue(k, out var o) ? o as string : null;

		public VisualStudioInterpreterConfiguration(
			string id,
			string description,
			string prefixPath = null,
			string pythonExePath = null,
			string winPath = "",
			string pathVar = "",
			InterpreterArchitecture architecture = default(InterpreterArchitecture),
			Version version = null,
			InterpreterUIMode uiMode = InterpreterUIMode.Normal
		) : base(id, description, pythonExePath, pathVar, string.Empty, string.Empty, architecture, version)
		{
			PrefixPath = prefixPath;
			WindowsInterpreterPath = string.IsNullOrEmpty(winPath) ? pythonExePath : winPath;
			UIMode = uiMode;
		}

		public bool Equals(VisualStudioInterpreterConfiguration other)
		{
			if (other == null)
			{
				return false;
			}

			var cmp = StringComparer.OrdinalIgnoreCase;
			return cmp.Equals(PrefixPath, other.PrefixPath) &&
				   cmp.Equals(Id, other.Id) &&
				   cmp.Equals(Description, other.Description) &&
				   cmp.Equals(InterpreterPath, other.InterpreterPath) &&
				   cmp.Equals(WindowsInterpreterPath, other.WindowsInterpreterPath) &&
				   cmp.Equals(PathEnvironmentVariable, other.PathEnvironmentVariable) &&
				   Architecture == other.Architecture &&
				   Version == other.Version &&
				   UIMode == other.UIMode;
		}

		public override int GetHashCode()
		{
			var cmp = StringComparer.OrdinalIgnoreCase;
			return cmp.GetHashCode(PrefixPath ?? "") ^
				   Id.GetHashCode() ^
				   cmp.GetHashCode(Description) ^
				   cmp.GetHashCode(InterpreterPath ?? "") ^
				   cmp.GetHashCode(WindowsInterpreterPath ?? "") ^
				   cmp.GetHashCode(PathEnvironmentVariable ?? "") ^
				   Architecture.GetHashCode() ^
				   Version.GetHashCode() ^
				   UIMode.GetHashCode();
		}
	}
}
