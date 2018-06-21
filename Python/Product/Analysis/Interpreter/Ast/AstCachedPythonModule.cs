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
using System.IO;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstCachedPythonModule : AstScrapedPythonModule {
        private readonly string _cachePath;

        public AstCachedPythonModule(string name, string cachePath) : base(name, null) {
            _cachePath = cachePath;
        }

        protected override Stream LoadCachedCode(AstPythonInterpreter interpreter) {
            return PathUtils.OpenWithRetry(_cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        protected override List<string> GetScrapeArguments(IPythonInterpreterFactory factory) {
            // Cannot scrape this module
            return null;
        }

        protected override void SaveCachedCode(AstPythonInterpreter interpreter, Stream code) {
            // Cannot save
        }
    }
}
