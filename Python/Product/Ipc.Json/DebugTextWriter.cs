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

using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.PythonTools.Ipc.Json {
    public class DebugTextWriter : TextWriter {
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value) {
            // Technically this is the only Write/WriteLine overload we need to
            // implement. We override the string versions for better performance.
            Debug.Write(value);
        }

        public override void Write(string value) {
            Debug.Write(value);
        }

        public override void WriteLine(string value) {
            Debug.WriteLine(value);
        }
    }
}
