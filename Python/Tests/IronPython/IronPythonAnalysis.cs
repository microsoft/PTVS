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

namespace IronPythonTests
{
	class IronPythonAnalysis : PythonAnalysis
	{
		public IronPythonAnalysis(PythonLanguageVersion version) : base(version) { }

		public IronPythonAnalysis(string idOrDescription) : base(idOrDescription) { }

		public IronPythonAnalysis(IPythonInterpreterFactory factory) : base(factory)
		{
			((IronPythonInterpreter)Analyzer.Interpreter).Remote.AddAssembly(new ObjectHandle(typeof(IronPythonAnalysisTest).Assembly));
		}

		public override BuiltinTypeId BuiltinTypeId_Str => BuiltinTypeId.Unicode;

		// IronPython does not distinguish between string iterators, and
		// since BytesIterator < UnicodeIterator, it is the one returned
		// for iter("").
		public override BuiltinTypeId BuiltinTypeId_StrIterator => BuiltinTypeId.UnicodeIterator;
	}
}
