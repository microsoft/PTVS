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

namespace Microsoft.PythonTools.Interpreter
{
	static class MSBuildConstants
	{

		// keys used for storing information about user defined interpreters
		public const string InterpreterItem = "Interpreter";
		public const string IdKey = "Id";
		public const string InterpreterPathKey = "InterpreterPath";
		public const string WindowsPathKey = "WindowsInterpreterPath";
		public const string LibraryPathKey = "LibraryPath";
		public const string ArchitectureKey = "Architecture";
		public const string VersionKey = "Version";
		public const string PathEnvVarKey = "PathEnvironmentVariable";
		public const string DescriptionKey = "Description";
		public const string BaseInterpreterKey = "BaseInterpreter";

		public const string InterpreterReferenceItem = "InterpreterReference";
		private static readonly Regex InterpreterReferencePath = new Regex(
			@"\{?(?<id>[a-f0-9\-]+)\}?
              \\
              (?<version>[23]\.[0-9])",
			RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase
		);

		public const string InterpreterIdProperty = "InterpreterId";
		internal const string InterpreterVersionProperty = "InterpreterVersion";
	}
}
