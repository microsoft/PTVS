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

namespace Microsoft.PythonTools {
    [Serializable]
    public class MissingInterpreterException : Exception {
        private readonly string _helpPage;

        public MissingInterpreterException(string message) : base(message) { }
        public MissingInterpreterException(string message, Exception inner) : base(message, inner) { }

        public MissingInterpreterException(string message, string helpPage)
            : base(message) {
            _helpPage = helpPage;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context) {
            base.GetObjectData(info, context);
            if (!string.IsNullOrEmpty(_helpPage)) {
                try {
                    info.AddValue("HelpPage", _helpPage);
                } catch (SerializationException) {
                }
            }
        }

        public string HelpPage { get { return _helpPage; } }

        protected MissingInterpreterException(SerializationInfo info, StreamingContext context)
            : base(info, context) {
            try {
                _helpPage = info.GetString("HelpPage");
            } catch (SerializationException) {
            }
        }
    }
}
