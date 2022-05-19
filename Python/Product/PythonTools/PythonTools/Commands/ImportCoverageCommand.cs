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
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.PythonTools.CodeCoverage;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using Microsoft.VisualStudioTools;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for importing code coverage information from a coverage.py XML file.
    /// </summary>
    internal sealed class ImportCoverageCommand : Command {
        private readonly IServiceProvider _serviceProvider;

        public ImportCoverageCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            
        }

        public override void DoCommand(object sender, EventArgs args) {
            var oe = args as OleMenuCmdEventArgs;
            string file = oe.InValue as string;
            PythonLanguageVersion? version = null;
            if (file == null) {
                object[] inp = oe.InValue as object[];
                if (inp != null && inp.Length == 2) {
                    file = inp[0] as string;
                    if (inp[1] is PythonLanguageVersion) {
                        version = (PythonLanguageVersion)inp[1];
                    }
                }
            }

            if (file == null) {
                file = _serviceProvider.BrowseForFileOpen(
                    IntPtr.Zero,
                    Strings.ImportCoverageCommandFileFilter
                );
            }

            if (file != null) {
                try {
                    _serviceProvider.GetPythonToolsService().UIThread.Invoke(async () => await DoConvert(file, version));
                } catch (Exception ex) {
                    ex.ReportUnhandledException(_serviceProvider, GetType());
                }
            }
        }

        private async Task DoConvert(string file, PythonLanguageVersion? version) {
            var outFilename = Path.ChangeExtension(file, ".coveragexml");

            try {
                await ConvertCoveragePyAsync(file, outFilename, version);
            } catch (IOException ioex) {
                MessageBox.Show(String.Format(Strings.FailedToConvertCoverageFile, ioex.Message));
            }

            _serviceProvider.GetDTE().ItemOperations.OpenFile(outFilename);
        }

        private async Task ConvertCoveragePyAsync(string inputFile, string outputFile, PythonLanguageVersion? version) {
            var baseDir = Path.GetDirectoryName(inputFile);
            using (FileStream tmp = new FileStream(inputFile, FileMode.Open))
            using (FileStream outp = new FileStream(outputFile, FileMode.Create)) {
                // Read in the data from coverage.py's XML file
                CoverageFileInfo[] fileInfo = new CoveragePyConverter(baseDir, tmp).Parse();

                // Discover what version we should use for this if one hasn't been provided...
                if (version == null) {
                    foreach (var file in fileInfo) {
                        var factory = GetFactory(_serviceProvider, file.Filename);
                        version = factory?.Configuration.Version.ToLanguageVersion();
                        if (version.HasValue && version != PythonLanguageVersion.None) {
                            break;
                        }
                    }
                }

                // Convert that into offsets within the actual code
                var covInfo = Import(fileInfo, version ?? PythonLanguageVersion.None);

                // Then export as .coveragexml
                new CoverageExporter(outp, covInfo).Export();
            }
        }

        private static IPythonInterpreterFactory GetFactory(IServiceProvider serviceProvider, string filePath) {
            var project = serviceProvider.GetProjectContainingFile(filePath);
            if (project != null) {
                return project.ActiveInterpreter;
            } else {
                var componentModel = serviceProvider.GetComponentModel();
                var workspaceService = componentModel.GetService<IVsFolderWorkspaceService>();
                var registryService = componentModel.GetService<IInterpreterRegistryService>();
                var optionsService = componentModel.GetService<IInterpreterOptionsService>();

                if (workspaceService.CurrentWorkspace != null) {
                    return workspaceService.CurrentWorkspace.GetInterpreterFactory(registryService, optionsService);
                } else {
                    return optionsService.DefaultInterpreter;
                }
            }
        }

        internal static Dictionary<CoverageFileInfo, CoverageMapper>  Import(CoverageFileInfo[] fileInfo, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            Dictionary<CoverageFileInfo, CoverageMapper> files = new Dictionary<CoverageFileInfo, CoverageMapper>();
            foreach (var file in fileInfo) {
                using (var stream = new FileStream(file.Filename, FileMode.Open)) {
                    var parser = Parser.CreateParser(stream, version);
                    var ast = parser.ParseFile();

                    var collector = new CoverageMapper(ast, file.Filename, file.Hits);
                    ast.Walk(collector);

                    files[file] = collector;
                }
            }
            return files;
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidImportCoverage; }
        }
    }
}
