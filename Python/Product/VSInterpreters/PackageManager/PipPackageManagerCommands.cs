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

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Interpreter {
    class PipPackageManagerCommands {
        public virtual IEnumerable<string> Base() => new[] { "-m", "pip" };
        public virtual IEnumerable<string> CheckIsReady() => new[] { "-c", "import pip" };
        public virtual IEnumerable<string> Prepare() => new[] { "-m", "ensurepip" };
        public virtual IEnumerable<string> Install(string package) => Base().Concat(new[] { "install", "-U", package });
        public virtual IEnumerable<string> Uninstall(string package) => Base().Concat(new[] { "uninstall", "-y", package });
        public virtual IEnumerable<string> ListJson() => Base().Concat(new[] { "list", "--format=json" });
        public virtual IEnumerable<string> List() => Base().Concat(new[] { "list" });
    }
}
