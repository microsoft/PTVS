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

using System;
using System.Runtime.Serialization;

namespace Microsoft.PythonTools.Interpreter {
    [Serializable]
    public class NoInterpretersException : Exception {
        public NoInterpretersException() : base() { }
        public NoInterpretersException(string message) : base(message) { }
        public NoInterpretersException(string message, Exception inner) : base(message, inner) { }

        public NoInterpretersException(string message, string helpPage)
            : base(message) {
            HelpPage = helpPage;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context) {
            base.GetObjectData(info, context);
            if (!string.IsNullOrEmpty(HelpPage)) {
                try {
                    info.AddValue("HelpPage", HelpPage);
                } catch (SerializationException) {
                }
            }
        }

        public string HelpPage { get; }

        protected NoInterpretersException(SerializationInfo info, StreamingContext context)
            : base(info, context) {
            try {
                HelpPage = info.GetString("HelpPage");
            } catch (SerializationException) {
            }
        }
    }
}
