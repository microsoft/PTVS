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
using System.Collections.Generic;
using System.IO;
using Microsoft.PythonTools.CodeCoverage;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for starting a file or the start item of a project in the REPL window.
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
                    "Coverage XML (*.xml)|*.xml|All Files (*.*)|*.*"
                );
            }

            if (file != null) {
                var outFilename = Path.Combine(
                    Path.GetDirectoryName(file), 
                    Path.GetFileNameWithoutExtension(file) + ".coveragexml"
                );

                ConvertCoveragePy(file, outFilename, version);

                _serviceProvider.GetDTE().ItemOperations.OpenFile(outFilename);
            }
        }

        private void ConvertCoveragePy(string inputFile, string outputFile, PythonLanguageVersion? version) {
            var baseDir = Path.GetDirectoryName(inputFile);
            using (FileStream tmp = new FileStream(inputFile, FileMode.Open))
            using (FileStream outp = new FileStream(outputFile, FileMode.Create)) {
                // Read in the data from coverage.py's XML file
                CoverageFileInfo[] fileInfo = new CoveragePyConverter(baseDir, tmp).Parse();

                // Discover what version we should use for this if one hasn't been provided...
                if (version == null) {
                    foreach (var file in fileInfo) {
                        var project = _serviceProvider.GetProjectFromFile(file.Filename);
                        if (project != null) {
                            version = project.ActiveInterpreter.Configuration.Version.ToLanguageVersion();
                            break;
                        }
                    }
                }

                if (version == null) {
                    version = _serviceProvider
                                .GetPythonToolsService()
                                .DefaultInterpreterConfiguration
                                .Version
                                .ToLanguageVersion();
                }

                // Convert that into offsets within the actual code
                var covInfo = Import(fileInfo, version.Value);

                // Then export as .coveragexml
                new CoverageExporter(outp, covInfo).Export();
            }
        }

        internal static Dictionary<CoverageFileInfo, CoverageMapper>  Import(CoverageFileInfo[] fileInfo, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            Dictionary<CoverageFileInfo, CoverageMapper> files = new Dictionary<CoverageFileInfo, CoverageMapper>();
            foreach (var file in fileInfo) {
                using (var parser = Parser.CreateParser(
                   new FileStream(file.Filename, FileMode.Open),
                   version
                )) {
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
