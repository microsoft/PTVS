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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.IO;
using System.Linq;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstTypingModule : AstCachedPythonModule {
        public AstTypingModule() : base("typing", FindTypingStub()) { }

        private static string FindTypingStub() {
            if (InstallPath.TryGetFile("typing-stub.pyi", out var fullPath)) {
                return fullPath;
            }
            throw new FileNotFoundException("typing-stub.pyi");
        }

        public static bool IsTypingType(IMember type) {
            if (type is IPythonType pyType) {
                return pyType.DeclaringModule is AstTypingModule;
            }

            if (type is IPythonMultipleMembers mm) {
                return mm.Members.Any(IsTypingType);
            }

            return false;
        }
    }
}
