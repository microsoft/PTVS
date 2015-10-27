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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudioTools.Navigation {
    /// <summary>
    /// Class storing the data about a parsing task on a language module.
    /// A module in dynamic languages is a source file, so here we use the file name to
    /// identify it.
    /// </summary>
    public class LibraryTask {
        private string _fileName;
        private ITextBuffer _textBuffer;
        private ModuleId _moduleId;

        public LibraryTask(string fileName, ITextBuffer textBuffer, ModuleId moduleId) {
            _fileName = fileName;
            _textBuffer = textBuffer;
            _moduleId = moduleId;
        }

        public string FileName {
            get { return _fileName; }
        }

        public ModuleId ModuleID {
            get { return _moduleId; }
            set { _moduleId = value; }
        }

        public ITextBuffer TextBuffer {
            get { return _textBuffer; }
        }
    }

}
