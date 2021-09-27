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

namespace TestRunnerInterop
{
    class TestFailedException : Exception
    {
        private readonly string _innerType;
        private readonly string _stackTrace;

        public TestFailedException(string innerType, string message, string stackTrace)
        {
            _innerType = innerType;
            Message = $"{_innerType.Substring(_innerType.LastIndexOf('.') + 1)}: {message}";
            _stackTrace = stackTrace;
        }

        public override string ToString()
        {
            return base.ToString().Replace(GetType().FullName, _innerType);
        }

        public override string Message { get; }

        public override string StackTrace => _stackTrace ?? base.StackTrace;
    }
}
