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
using System.Linq;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class ModuleRefView : MemberView {
        public ModuleRefView(IModuleContext context, string name, IPythonModule module)
            : base(context, name, module) {
        }

        public override string SortKey {
            get { return "8"; }
        }

        public override string DisplayType {
            get { return "Module reference"; }
        }

        public override void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine("{0}import {1}", currentIndent, Name);
            exportChildren = null;
        }

        public override void ExportToDiffable(
            TextWriter writer,
            string currentIndent,
            string indent,
            Stack<IAnalysisItemView> exportStack,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine("{0}{2} ({1})", currentIndent, DisplayType, Name);
            exportChildren = null;
        }
    }
}
