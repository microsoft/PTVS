﻿// Visual Studio Shared Project
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
using System.Collections.Generic;
using EnvDTE;

namespace Microsoft.VisualStudioTools.MockVsTests {
    internal class MockDTE : EnvDTE.DTE {
        internal readonly MockVs _vs;

        public MockDTE(MockVs vs) {
            _vs = vs;
        }

        public Document ActiveDocument {
            get {
                throw new NotImplementedException();
            }
        }

        public object ActiveSolutionProjects {
            get {
                throw new NotImplementedException();
            }
        }

        public Window ActiveWindow {
            get {
                throw new NotImplementedException();
            }
        }

#if DEV18_OR_LATER
        [Obsolete("AddIn related extension points are no longer supported in Visual Studio.")]
#endif
        public AddIns AddIns {
            get {
                throw new NotImplementedException();
            }
        }

        public DTE Application {
            get {
                throw new NotImplementedException();
            }
        }

        public object CommandBars {
            get {
                throw new NotImplementedException();
            }
        }

        public string CommandLineArguments {
            get {
                throw new NotImplementedException();
            }
        }

        public Commands Commands {
            get {
                throw new NotImplementedException();
            }
        }

        public ContextAttributes ContextAttributes {
            get {
                throw new NotImplementedException();
            }
        }

        public Debugger Debugger {
            get {
                throw new NotImplementedException();
            }
        }

        public vsDisplay DisplayMode {
            get {
                throw new NotImplementedException();
            }

            set {
                throw new NotImplementedException();
            }
        }

        public Documents Documents {
            get {
                throw new NotImplementedException();
            }
        }

        public DTE DTE {
            get {
                return this;
            }
        }

        public string Edition {
            get {
                throw new NotImplementedException();
            }
        }

        public Events Events {
            get {
                throw new NotImplementedException();
            }
        }

        public string FileName {
            get {
                throw new NotImplementedException();
            }
        }

        public Find Find {
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

        public ItemOperations ItemOperations {
            get {
                throw new NotImplementedException();
            }
        }

        public int LocaleID {
            get {
                throw new NotImplementedException();
            }
        }

        public Macros Macros {
            get {
                throw new NotImplementedException();
            }
        }

        public DTE MacrosIDE {
            get {
                throw new NotImplementedException();
            }
        }

        public Window MainWindow {
            get {
                throw new NotImplementedException();
            }
        }

        public vsIDEMode Mode {
            get {
                throw new NotImplementedException();
            }
        }

        public string Name {
            get {
                throw new NotImplementedException();
            }
        }

        public ObjectExtenders ObjectExtenders {
            get {
                throw new NotImplementedException();
            }
        }

        public string RegistryRoot {
            get {
                throw new NotImplementedException();
            }
        }

        public SelectedItems SelectedItems {
            get {
                throw new NotImplementedException();
            }
        }

        public Solution Solution {
            get {
                return new MockDTESolution(this);
            }
        }

        public SourceControl SourceControl {
            get {
                throw new NotImplementedException();
            }
        }

        public StatusBar StatusBar {
            get {
                throw new NotImplementedException();
            }
        }

        public bool SuppressUI {
            get {
                throw new NotImplementedException();
            }

            set {
                throw new NotImplementedException();
            }
        }

        public UndoContext UndoContext {
            get {
                throw new NotImplementedException();
            }
        }

        public bool UserControl {
            get {
                throw new NotImplementedException();
            }

            set {
                throw new NotImplementedException();
            }
        }

        public string Version {
            get {
                throw new NotImplementedException();
            }
        }

        public WindowConfigurations WindowConfigurations {
            get {
                throw new NotImplementedException();
            }
        }

        public EnvDTE.Windows Windows {
            get {
                throw new NotImplementedException();
            }
        }

        public void ExecuteCommand(string CommandName, string CommandArgs = "") {
            throw new NotImplementedException();
        }

        public object GetObject(string Name) {
            throw new NotImplementedException();
        }

        public bool get_IsOpenFile(string ViewKind, string FileName) {
            throw new NotImplementedException();
        }

        public Properties get_Properties(string Category, string Page) {
            Properties res;
            Dictionary<string, Properties> pages;
            if (_properties.TryGetValue(Category, out pages) &&
                pages.TryGetValue(Page, out res)) { 
                return res;
            }
            return null;
        }

        public wizardResult LaunchWizard(string VSZFile, ref object[] ContextParams) {
            throw new NotImplementedException();
        }

        public Window OpenFile(string ViewKind, string FileName) {
            throw new NotImplementedException();
        }

        public void Quit() {
            throw new NotImplementedException();
        }

        public string SatelliteDllPath(string Path, string Name) {
            throw new NotImplementedException();
        }

        Dictionary<string, Dictionary<string, Properties>> _properties = new Dictionary<string, Dictionary<string, Properties>>() {
            {
                "Environment",
                new Dictionary<string, Properties>() {
                    {
                        "ProjectsAndSolution",
                        new MockDTEProperties() {
                            { "MSBuildOutputVerbosity", 2 }
                        }
                    }
                }
            }
        };
    }
}