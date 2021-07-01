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

namespace Microsoft.PythonTools.Intellisense
{
    [Export(typeof(IInterpreterLog))]
    class InterpreterLog : IInterpreterLog
    {
        private readonly IVsActivityLog _activityLog;

        [ImportingConstructor]
        public InterpreterLog([Import(typeof(SVsServiceProvider))] IServiceProvider provider)
        {
            _activityLog = (IVsActivityLog)provider.GetService(typeof(SVsActivityLog));
        }

        public void Log(string msg)
        {
            _activityLog.LogEntry(
                (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
                "Python Tools", // TODO: Localization - use ProductTitle for this?
                msg
            );
        }
    }
}
