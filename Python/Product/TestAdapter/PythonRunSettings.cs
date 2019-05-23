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
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Xml.XPath;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.TestAdapter;

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
        private readonly IServiceProvider _serviceProvider;
        internal static Uri PythonCodeCoverageUri = new Uri("datacollector://Microsoft/PythonCodeCoverage/1.0");

        private const string CodeCoverageImportName = "Microsoft.VisualStudio.TestWindow.CodeCoverage.ICodeCoverageSettingsService";
        internal const string CodeCoverageUriString = @"datacollector://Microsoft/CodeCoverage/2.0";
        internal const int cmdidImportCoverage = 0x10f;

        [ImportingConstructor]
        public PythonRunSettings([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider) {
            _compModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
            var opState = _compModel.GetService<IOperationState>();
            opState.StateChanged += StateChange;
            _serviceProvider = serviceProvider;
        }

        private void StateChange(object sender, OperationStateChangedEventArgs e) {
            if (e.State == TestOperationStates.TestExecutionFinished) {
                var resultUris = e.Operation.GetRunSettingsDataCollectorResultUri(PythonCodeCoverageUri);
                if (resultUris != null) {
                    foreach (var eachAttachment in resultUris) {
                        string filePath = eachAttachment.LocalPath;

                        if (File.Exists(filePath)) {
                            object inObj = filePath;
                            object outObj = null;

                            var dte = (EnvDTE.DTE)_serviceProvider.GetService(typeof(EnvDTE.DTE));
                            dte.Commands.Raise(
                                GuidList.guidPythonToolsCmdSet.ToString("B"),
                                cmdidImportCoverage,
                                ref inObj,
                                ref outObj
                            );
                        }
                    }
                }
            }
        }

        private bool CodeCoverageEnabled {
            get {
                try {
                    dynamic service = _compModel.DefaultExportProvider.GetExport<object>(CodeCoverageImportName)?.Value;
                    return (bool)(service?.Enabled ?? false);
                } catch (Exception) {
                    return false;
                }
            }
            set {
                try {
                    dynamic service = _compModel.DefaultExportProvider.GetExport<object>(CodeCoverageImportName)?.Value;
                    service.Enabled = value;
                } catch (Exception) {
                }
            }
        }

        public string Name {
            get {
                return "Python Run Settings";
            }
        }

        private bool UseLegacyDebugger {
            get {
                try {
                    bool useLegacyDebugger = false;

                    _serviceProvider.GetUIThread().Invoke(() => {
                        var dte = (EnvDTE.DTE)_serviceProvider.GetService(typeof(EnvDTE.DTE));
                        dynamic automationObject = dte.GetObject("VsPython");
                        useLegacyDebugger = automationObject.UseLegacyDebugger;
                    });

                    return useLegacyDebugger;
                } catch (Exception) {
                    return false;
                }
            }
        }

        public IXPathNavigable AddRunSettings(IXPathNavigable inputRunSettingDocument, IRunSettingsConfigurationInfo configurationInfo, ILogger log) {
            XPathNavigator navigator = inputRunSettingDocument.CreateNavigator();
            var python = navigator.Select("/RunSettings");
            if (python.MoveNext()) {
                using (var writer = python.Current.AppendChild()) {
                    var pyContainers = configurationInfo.TestContainers
                        .OfType<TestContainer>()
                        .GroupBy(x => x.Project);

                    writer.WriteStartElement("Python");
                    writer.WriteStartElement("TestCases");

                    foreach (var project in pyContainers) {
                        foreach (var container in project) {
                            writer.WriteStartElement("Project");
                            writer.WriteAttributeString("home", project.Key);
                            
                            string nativeCode = "", djangoSettings = "";

                            TestContainerDiscoverer discoverer = container.Discoverer as TestContainerDiscoverer;
                            if (discoverer == null) {
                                continue;
                            }

                            LaunchConfiguration config = null;
                            ThreadHelper.JoinableTaskFactory.Run(async () => {
                                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                                try {
                                    config = discoverer.GetLaunchConfigurationOrThrow(project.Key);
                                    
                                } catch {
                                }
                                //nativeCode = project.Key.GetProperty(PythonConstants.EnableNativeCodeDebugging);
                                //djangoSettings = project.Key.GetProperty("DjangoSettingsModule");
                            });

                            if (config == null) {
                                log.Log(
                                    MessageLevel.Warning,
                                    Strings.TestDiscoveryFailedMissingLaunchConfiguration.FormatUI(project.Key)
                                );
                                continue;
                            }
                            writer.WriteAttributeString("useLegacyDebugger", UseLegacyDebugger ? "1" : "0");
                            writer.WriteAttributeString("nativeDebugging", nativeCode);
                            writer.WriteAttributeString("djangoSettingsModule", djangoSettings);

                            writer.WriteAttributeString("workingDir", config.WorkingDirectory);
                            writer.WriteAttributeString("interpreter", config.GetInterpreterPath());
                            writer.WriteAttributeString("pathEnv", config.Interpreter.PathEnvironmentVariable);

                            writer.WriteStartElement("Environment");
                           
                            foreach (var keyValue in config.Environment) {
                                writer.WriteStartElement("Variable");
                                writer.WriteAttributeString("name", keyValue.Key);
                                writer.WriteAttributeString("value", keyValue.Value);
                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement(); // Environment

                            writer.WriteStartElement("SearchPaths");
                            foreach (var path in config.SearchPaths) {
                                writer.WriteStartElement("Search");
                                writer.WriteAttributeString("value", path);
                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement(); // SearchPaths

                            writer.WriteStartElement("Test");
                            writer.WriteAttributeString("file", container.Source);
                          
                            writer.WriteEndElement(); // Test

                            writer.WriteEndElement();  // Project
                        }
                    }

                    writer.WriteEndElement(); // TestCases
                    writer.WriteEndElement(); // Python
                }
            }

            // We only care about tweaking the settings for execution...
            if (configurationInfo.RequestState != RunSettingConfigurationInfoState.Execution) {
                return inputRunSettingDocument;
            }

            // And we also only care about doing it when we're all Python containers
#pragma warning disable CS0219 // Variable is assigned but its value is never used
            bool allPython = true, anyPython = false;
#pragma warning restore CS0219 // Variable is assigned but its value is never used
            foreach (var container in configurationInfo.TestContainers) {
                if (container is TestContainer) {
                    anyPython = true;
                } else {
                    allPython = false;
                }
            }

            if (!anyPython) {
                // Don't mess with code coverage settings if we're not running Python tests
                return inputRunSettingDocument;
            }

            if (CodeCoverageEnabled) {
                // Code coverage is currently enabled.  We don't want it adding it's data 
                // collector if ICodeCoverageSettingsService IRunSettingsService runs 
                // after ours.  So we tell it that it's been disabled to prevent that 
                // from happening.
                navigator = inputRunSettingDocument.CreateNavigator();

                var pythonNode = navigator.Select("/RunSettings/Python");
                if (pythonNode.MoveNext()) {
                    pythonNode.Current.AppendChild("<EnableCoverage>true</EnableCoverage>");
                }

                if (allPython) {
                    // Disable normal code coverage...
                    CodeCoverageEnabled = false;

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
                        String.IsNullOrWhiteSpace(codeCoverageNode.GetAttribute("x-keep-ptvs", null))) {
                        // Code coverage has been added, which means we (likely) came after 
                        // ICodeCoverageSettingsService in the MEF import order.  Let's remove 
                        // the node (we allow the user to define x-keep-ptvs to prevent us 
                        // from doing this if they've manually patched their runsettings file)
                        codeCoverageNode.DeleteSelf();
                    }
                }
            }

            return inputRunSettingDocument;
        }
    }
}
