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

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Threading;
using System.Xml.XPath;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestWindow.CodeCoverage;
using Microsoft.VisualStudio.TestWindow.Extensibility;

namespace Microsoft.PythonTools.TestAdapter {

    /// <summary>
    /// Configure run settings to disable the normal code coverage plugin.
    /// 
    /// This plugin will write out an empty file, and the user will end up seeing that 
    /// file (sometimes selected by default)  in the code coverage results.  We want to 
    /// keep the file that we're going to generate for code coverage information instead.
    /// </summary>
    [Export(typeof(IRunSettingsService))]
    class PythonRunSettings : IRunSettingsService {
        private readonly IComponentModel _compModel;
        private readonly Dispatcher _dispatcher;
        internal static Uri PythonCodeCoverageUri = new Uri("datacollector://Microsoft/PythonCodeCoverage/1.0");
        internal static string CodeCoverageUriString = @"datacollector://Microsoft/CodeCoverage/2.0";

        [ImportingConstructor]
        public PythonRunSettings([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider) {
            _compModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
            var opState = _compModel.GetService<IOperationState>();
            opState.StateChanged += StateChange;
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        private void StateChange(object sender, OperationStateChangedEventArgs e) {
            if (e.State == TestOperationStates.TestExecutionFinished) {
                var resultUris = e.Operation.GetRunSettingsDataCollectorResultUri(PythonCodeCoverageUri);
                if (resultUris != null) {
                    foreach (var eachAttachment in resultUris) {
                        string filePath = eachAttachment.LocalPath;
                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) {
                            _dispatcher.BeginInvoke(
                                new Action(() => {
                                    CodeCoverageHost.OpenFile(filePath);
                                }
                            ));
                        }
                    }
                }

            }
        }

        public ICodeCoverageSettingsService CodeCoverage {
            get {
                return _compModel.GetService<ICodeCoverageSettingsService>();
            }
        }

        public ICodeCoverageHost CodeCoverageHost {
            get {
                return _compModel.GetService<ICodeCoverageHost>();
            }
        }

        public string Name {
            get {
                return "Python Run Settings";
            }
        }

        public IXPathNavigable AddRunSettings(IXPathNavigable inputRunSettingDocument, IRunSettingsConfigurationInfo configurationInfo, ILogger log) {
            // We only care about tweaking the settings for execution...
            if (configurationInfo.RequestState != RunSettingConfigurationInfoState.Execution) {
                return null;
            }

            // And we also only care about doing it when we're all Python containers
            bool allPython = true, anyPython = false;
            foreach (var container in configurationInfo.TestContainers) {
                if (!(container.Discoverer is TestContainerDiscoverer)) {
                    allPython = false;
                } else {
                    anyPython = true;
                }
            }

            if (!anyPython) {
                // Don't mess with code coverage settings if we're not running Python tests
                return null;
            }

            var codeCov = CodeCoverage;
            if (codeCov.Enabled) {
                // Code coverage is currently enabled.  We don't want it adding it's data 
                // collector if ICodeCoverageSettingsService IRunSettingsService runs 
                // after ours.  So we tell it that it's been disabled to prevent that 
                // from happening.
                XPathNavigator navigator = inputRunSettingDocument.CreateNavigator();

                var settings = navigator.Select("/RunSettings");
                if (settings.MoveNext()) {
                    settings.Current.AppendChild("<Python><EnableCoverage>true</EnableCoverage></Python>");
                }

                if (allPython) {
                    // Disable normal code coverage...
                    codeCov.Enabled = false;

                    XPathNodeIterator nodes = navigator.Select("/RunSettings/DataCollectionRunSettings/DataCollectors/DataCollector");
                    XPathNavigator codeCoverageNode = null;
                    foreach (XPathNavigator dataCollectorNavigator in nodes) {
                        string uri = dataCollectorNavigator.GetAttribute("uri", string.Empty);
                        if (string.Equals(CodeCoverageUriString, uri, StringComparison.OrdinalIgnoreCase)) {
                            codeCoverageNode = dataCollectorNavigator;
                            break;
                        }
                    }

                    if (codeCoverageNode != null &&
                        codeCoverageNode.GetAttribute("x-keep-ptvs", null) == null) {
                        // Code coverage has been added, which means we (likely) came after 
                        // ICodeCoverageSettingsService in the MEF import order.  Let's remove 
                        // the node (we allow the user to define x-keep-ptvs to prevent us 
                        // from doing this if they've manually patched their runsettings file)
                        codeCoverageNode.DeleteSelf();
                    }
                }

                return inputRunSettingDocument;
            }

            return null;
        }
    }
}
