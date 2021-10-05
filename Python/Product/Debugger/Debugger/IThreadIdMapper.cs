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

namespace Microsoft.PythonTools.Debugger
{
	/// <summary>
	/// Provides mapping between Python thread IDs (which can be 64-bit if running on a 64-bit Linux system), and
	/// VS 32-bit thread IDs (which are 32-bit, and are faked if a 64-bit Python ID does not fit into 32 bits).
	/// </summary>
	internal interface IThreadIdMapper
	{
		long? GetPythonThreadId(uint vsThreadId);
	}
}
