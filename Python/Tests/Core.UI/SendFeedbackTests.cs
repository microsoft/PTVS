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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    public class SendFeedbackTests {
        public TestContext TestContext { get; set; }

        public void ReportAProblemDiagnostics(PythonVisualStudioApp app) {
            var startTime = DateTime.Now;

            // This command brings up the feedback window,
            // and generates the diagnostics file in the background.
            app.ExecuteCommand("Help.ReportaProblem");

            // Wait for the feedback window, and close it when done
            using (var window = WaitForSendFeedbackWindow(app)) {
                // Verify a file is created
                var filePath = WaitForDiagnosticsFile(startTime);
                Assert.IsTrue(File.Exists(filePath), "Diagnostics file was not found.");

                // Make sure it's a Python diagnostics
                var contents = ReadFileWithRetries(filePath);
                AssertUtil.Contains(contents, "Projects:", "Environments:", "Loaded assemblies:");
            }
        }

        private AutomationDialog WaitForSendFeedbackWindow(VisualStudioApp app) {
            Process process = null;
            for (int i = 0; i < 10; i++) {
                process = Process
                    .GetProcesses()
                    .Where(p => p.MainWindowTitle == "Visual Studio Feedback" && p.MainWindowHandle != IntPtr.Zero)
                    .FirstOrDefault();
                if (process != null) {
                    break;
                }

                Thread.Sleep(500);
            }

            Assert.IsNotNull(process, "Visual Studio Feedback window not found.");

            var element = AutomationElement.FromHandle(process.MainWindowHandle);
            return new AutomationDialog(app, element);
        }

        private string ReadFileWithRetries(string filePath) {
            int retries = 5;
            while (retries > 0) {
                retries--;
                try {
                    return File.ReadAllText(filePath, Encoding.UTF8);
                } catch (IOException) when (retries > 0) {
                    // File may still be written to in the background,
                    // so give it some more time.
                    Thread.Sleep(500);
                }
            }

            return null;
        }

        private string WaitForDiagnosticsFile(DateTime startTime) {
            for (int i = 0; i < 10; i++) {
                var currentFiles = Directory.GetFiles(Path.GetTempPath(), "PythonToolsDiagnostics_*.log")
                    .Where(f => File.GetCreationTime(f) > startTime)
                    .OrderBy(f => File.GetCreationTime(f));
                var filePath = currentFiles.LastOrDefault();
                if (filePath != null) {
                    return filePath;
                }

                Thread.Sleep(500);
            }

            return null;
        }
    }
}
