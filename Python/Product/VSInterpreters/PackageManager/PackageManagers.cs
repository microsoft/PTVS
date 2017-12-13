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

using System.Collections.Generic;

namespace Microsoft.PythonTools.Interpreter {
    public static class PackageManagers {
        // This avoids changing the IPythonInterpreterFactory for now, but
        // it's not extensible, so we either need to change the interface or
        // find another way to extend. There may be other reasons we want
        // it exposed on the interface too.
        public static IEnumerable<IPackageManager> GetAlternatePackageManagers(IPythonInterpreterFactory factory) {
            if (factory.PackageManager is CondaPackageManager) {
                var pip = BuiltInPackageManagers.Pip;
                pip.SetInterpreterFactory(factory);
                yield return pip;
            }
        }
    }
}
