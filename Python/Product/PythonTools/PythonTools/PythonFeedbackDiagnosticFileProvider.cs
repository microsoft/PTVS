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
using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using Microsoft.Internal.VisualStudio.Shell.Embeddable.Feedback;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools {
    [Export(typeof(IFeedbackDiagnosticFileProvider))]
    public class PythonFeedbackDiagnosticFileProvider : IFeedbackDiagnosticFileProvider {
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public PythonFeedbackDiagnosticFileProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public IReadOnlyCollection<string> GetFiles() {
            // A null UIThreadBase means the VS PythonTools package hasn't been initialized,
            // which means the user is not using Python, so we should not
            // write Python diagnostics file.
            // Note: we do not use the GetUIThread() extension method which
            // returns a mock UI thread instead of returning null.
            var uiThread = (UIThreadBase)_serviceProvider.GetService(typeof(UIThreadBase));
            if (uiThread == null) {
                return new string[0];
            }

            var filePath = PathUtils.GetAvailableFilename(
                Path.GetTempPath(),
                "PythonToolsDiagnostics_{0:yyyyMMddHHmmss}".FormatInvariant(DateTime.Now),
                ".log"
            );

            // Generate the file in the background and return the path
            // immediately, to avoid delay opening the feedback dialog.
            Task.Run(() => GenerateFile(uiThread, filePath));

            return new string[] { filePath };
        }

        private void GenerateFile(UIThreadBase uiThread, string filePath) {
            try {
                uiThread.Invoke(() => {
                    using (var writer = new StreamWriter(filePath, false, Encoding.UTF8)) {
                        try {
                            _serviceProvider.GetPythonToolsService().GetDiagnosticsLog(writer, false);
                        } catch (Exception ex) when (!ex.IsCriticalException()) {
                            // Append the error to the log we're writing
                            writer.WriteLine(ex.ToUnhandledExceptionMessage(GetType()));
                        }
                    }
                });
            } catch (Exception ex) when (!ex.IsCriticalException()) {
            }
        }
    }
}
