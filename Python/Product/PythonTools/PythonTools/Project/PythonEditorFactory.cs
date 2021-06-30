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
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Navigation;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Factory for creating code editor.
    /// </summary>
    /// <remarks>
    /// While currently empty, editor factory has to be unique per language.
    /// </remarks>
    [Guid(PythonConstants.EditorFactoryGuid)]
    public class PythonEditorFactory : CommonEditorFactory {
        public PythonEditorFactory(CommonProjectPackage package) : base(package) { }

        public PythonEditorFactory(CommonProjectPackage package, bool promptForEncoding) : base(package, promptForEncoding) { }

        protected override void InitializeLanguageService(IVsTextLines textLines) {
            InitializeLanguageService(textLines, typeof(PythonLanguageInfo).GUID);
        }

        public override int CreateEditorInstance(uint createEditorFlags, string documentMoniker, string physicalView, IVsHierarchy hierarchy, uint itemid, IntPtr docDataExisting, out IntPtr docView, out IntPtr docData, out string editorCaption, out Guid commandUIGuid, out int createDocumentWindowFlags) {
            var res = base.CreateEditorInstance(createEditorFlags, documentMoniker, physicalView, hierarchy, itemid, docDataExisting, out docView, out docData, out editorCaption, out commandUIGuid, out createDocumentWindowFlags);
            commandUIGuid = new Guid(PythonConstants.EditorFactoryGuid);
            return res;
        }
    }

    [Guid(PythonConstants.EditorFactoryPromptForEncodingGuid)]
    public class PythonEditorFactoryPromptForEncoding : PythonEditorFactory {
        public PythonEditorFactoryPromptForEncoding(CommonProjectPackage package) : base(package, true) { }
    }
}
