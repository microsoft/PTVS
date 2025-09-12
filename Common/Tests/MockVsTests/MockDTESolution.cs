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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections;
using EnvDTE;
using Microsoft.VisualStudio;

namespace Microsoft.VisualStudioTools.MockVsTests {
    internal class MockDTESolution : Solution {
        private readonly MockDTE _dte;

        public MockDTESolution(MockDTE dte) {
            _dte = dte;
        }

#if DEV18_OR_LATER
        [Obsolete("AddIn related extension points are no longer supported in Visual Studio.")]
#endif
        public AddIns AddIns {
            get {
                throw new NotImplementedException();
            }
        }

        public int Count {
            get {
                throw new NotImplementedException();
            }
        }

        public DTE DTE {
            get {
                return _dte;
            }
        }

        public string ExtenderCATID {
            get {
                throw new NotImplementedException();
            }
        }

        public object ExtenderNames {
            get {
                throw new NotImplementedException();
            }
        }

        public string FileName {
            get {
                throw new NotImplementedException();
            }
        }

        public string FullName {
            get {
                throw new NotImplementedException();
            }
        }

        public Globals Globals {
            get {
                throw new NotImplementedException();
            }
        }

        public bool IsDirty {
            get {
                throw new NotImplementedException();
            }

            set {
                throw new NotImplementedException();
            }
        }

        public bool IsOpen {
            get {
                throw new NotImplementedException();
            }
        }

        public DTE Parent {
            get {
                throw new NotImplementedException();
            }
        }

        public Projects Projects {
            get {
                throw new NotImplementedException();
            }
        }

        public Properties Properties {
            get {
                throw new NotImplementedException();
            }
        }

        public bool Saved {
            get {
                throw new NotImplementedException();
            }

            set {
                throw new NotImplementedException();
            }
        }

        public SolutionBuild SolutionBuild {
            get {
                throw new NotImplementedException();
            }
        }

        public Project AddFromFile(string FileName, bool Exclusive = false) {
            throw new NotImplementedException();
        }

        public Project AddFromTemplate(string FileName, string Destination, string ProjectName, bool Exclusive = false) {
            throw new NotImplementedException();
        }

        public void Close(bool SaveFirst = false) {
            ErrorHandler.ThrowOnFailure(_dte._vs.Solution.Close());
        }

        public void Create(string Destination, string Name) {
            throw new NotImplementedException();
        }

        public ProjectItem FindProjectItem(string FileName) {
            throw new NotImplementedException();
        }

        public IEnumerator GetEnumerator() {
            throw new NotImplementedException();
        }

        public object get_Extender(string ExtenderName) {
            throw new NotImplementedException();
        }

        public string get_TemplatePath(string ProjectType) {
            throw new NotImplementedException();
        }

        public Project Item(object index) {
            throw new NotImplementedException();
        }

        public void Open(string FileName) {
            throw new NotImplementedException();
        }

        public string ProjectItemsTemplatePath(string ProjectKind) {
            throw new NotImplementedException();
        }

        public void Remove(Project proj) {
            throw new NotImplementedException();
        }

        public void SaveAs(string FileName) {
            throw new NotImplementedException();
        }
    }
}