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

using System.Windows.Automation;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities.UI.Python {
    class PythonProjectDebugProperties : AutomationWrapper {
        public PythonProjectDebugProperties(AutomationElement element)
            : base(element) {
        }

        public string LaunchMode {
            get {
                var launchMode = new ComboBox(FindByAutomationId("_launchModeCombo"));
                return launchMode.GetSelectedItemName();
            }
            set {
                var launchMode = new ComboBox(FindByAutomationId("_launchModeCombo"));
                launchMode.SelectItem(value);
            }
        }

        private string GetValue(string automationId) {
            var textBox = new TextBox(FindByAutomationId(automationId));
            return textBox.Value;
        }

        private void SetValue(string automationId, string value) {
            var textBox = new TextBox(FindByAutomationId(automationId));
            textBox.Value = value;
        }

        public string SearchPaths { get { return GetValue("_searchPaths"); } set { SetValue("_searchPaths", value); } }
        public string CommandLineArguments { get { return GetValue("_arguments"); } set { SetValue("_arguments", value); } }
        public string InterpreterArguments { get { return GetValue("_interpreterPath"); } set { SetValue("_interpreterPath", value); } }
        public string InterpreterPath { get { return GetValue("_interpArgs"); } set { SetValue("_interpArgs", value); } }
        public string WebBrowserUrl { get { return GetValue("_launchUrl"); } set { SetValue("_launchUrl", value); } }
        public string WebBrowserPort { get { return GetValue("_portNumber"); } set { SetValue("_portNumber", value); } }

        public string RunWebServerTarget { get { return GetValue("_runServerTarget"); } set { SetValue("_runServerTarget", value); } }
        public string RunWebServerArguments { get { return GetValue("_runServerArguments"); } set { SetValue("_runServerArguments", value); } }
        public string RunWebServerEnvironment { get { return GetValue("_runServerEnvironment"); } set { SetValue("_runServerEnvironment", value); } }
        public string DebugWebServerTarget { get { return GetValue("_debugServerTarget"); } set { SetValue("_debugServerTarget", value); } }
        public string DebugWebServerArguments { get { return GetValue("_debugServerArguments"); } set { SetValue("_debugServerArguments", value); } }
        public string DebugWebServerEnvironment { get { return GetValue("_debugServerEnvironment"); } set { SetValue("_debugServerEnvironment", value); } }

        public string RunWebServerTargetType {
            get {
                var comboBox = new ComboBox(FindByAutomationId("_runServerTargetType"));
                return comboBox.GetSelectedItemName();
            }
            set {
                var comboBox = new ComboBox(FindByAutomationId("_runServerTargetType"));
                comboBox.SelectItem(value);
            }
        }

        public string DebugWebServerTargetType {
            get {
                var comboBox = new ComboBox(FindByAutomationId("_debugServerTargetType"));
                return comboBox.GetSelectedItemName();
            }
            set {
                var comboBox = new ComboBox(FindByAutomationId("_debugServerTargetType"));
                comboBox.SelectItem(value);
            }
        }

        private static string PropertyValue(IPythonProject project, string property, string defaultValue = "") {
            return ((PythonProjectNode)project).Site.GetUIThread().Invoke(() => project.GetUnevaluatedProperty(property) ?? defaultValue);
        }

        public void AssertMatchesProject(IPythonProject project) {
            Assert.AreEqual(PropertyValue(project, PythonConstants.SearchPathSetting), SearchPaths, "SearchPaths does not match");
            Assert.AreEqual(PropertyValue(project, PythonConstants.CommandLineArgumentsSetting), CommandLineArguments, "CommandLineArguments does not match");
            Assert.AreEqual(PropertyValue(project, PythonConstants.InterpreterArgumentsSetting), InterpreterArguments, "InterpreterArguments does not match");
            Assert.AreEqual(PropertyValue(project, PythonConstants.InterpreterPathSetting), InterpreterPath, "InterpreterPath does not match");
            Assert.AreEqual(PropertyValue(project, PythonConstants.WebBrowserUrlSetting), WebBrowserUrl, "WebBrowserUrl does not match");
            Assert.AreEqual(PropertyValue(project, PythonConstants.WebBrowserPortSetting), WebBrowserPort, "WebBrowserPort does not match");
            Assert.AreEqual(PropertyValue(project, PythonWebLauncher.RunWebServerTargetProperty), RunWebServerTarget, "RunWebServerTarget does not match");
            Assert.AreEqual(PropertyValue(project, PythonWebLauncher.RunWebServerTargetTypeProperty, "script"), RunWebServerTargetType, "RunWebServerTargetType does not match");
            Assert.AreEqual(PropertyValue(project, PythonWebLauncher.RunWebServerArgumentsProperty), RunWebServerArguments, "RunWebServerArguments does not match");
            Assert.AreEqual(PropertyValue(project, PythonWebLauncher.RunWebServerEnvironmentProperty), RunWebServerEnvironment, "RunWebServerEnvironment does not match");
            Assert.AreEqual(PropertyValue(project, PythonWebLauncher.DebugWebServerTargetProperty), DebugWebServerTarget, "DebugWebServerTarget does not match");
            Assert.AreEqual(PropertyValue(project, PythonWebLauncher.DebugWebServerTargetTypeProperty, "script"), DebugWebServerTargetType, "DebugWebServerTargetType does not match");
            Assert.AreEqual(PropertyValue(project, PythonWebLauncher.DebugWebServerArgumentsProperty), DebugWebServerArguments, "DebugWebServerArguments does not match");
            Assert.AreEqual(PropertyValue(project, PythonWebLauncher.DebugWebServerEnvironmentProperty), DebugWebServerEnvironment, "DebugWebServerEnvironment does not match");
        }
    }
}
