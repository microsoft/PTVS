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

namespace Microsoft.PythonTools.Debugger.Remote
{
    internal class PythonRemoteEnumDebugPrograms : PythonRemoteEnumDebug<IDebugProgram2>, IEnumDebugPrograms2
    {

        public readonly PythonRemoteDebugProcess _process;

        public PythonRemoteEnumDebugPrograms(PythonRemoteDebugProcess process)
            : base(new PythonRemoteDebugProgram(process))
        {
            this._process = process;
        }

        public int Clone(out IEnumDebugPrograms2 ppEnum)
        {
            ppEnum = new PythonRemoteEnumDebugPrograms(_process);
            return 0;
        }
    }
}
