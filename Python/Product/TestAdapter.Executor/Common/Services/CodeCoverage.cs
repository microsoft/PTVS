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
using System.Xml;
using System.Xml.XPath;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.PythonTools.TestAdapter.Services {
    internal static class CodeCoverage {

        internal static bool EnableCodeCoverage(IRunContext runContext) {
            var doc = Read(runContext.RunSettings.SettingsXml);
            XPathNodeIterator nodes = doc.CreateNavigator().Select("/RunSettings/Python/EnableCoverage");
            bool enableCoverage;
            if (nodes.MoveNext()) {
                if (Boolean.TryParse(nodes.Current.Value, out enableCoverage)) {
                    return enableCoverage;
                }
            }
            return false;
        }

        internal static void AttachCoverageResults(IFrameworkHandle frameworkHandle, string covPath) {
            if (File.Exists(covPath + ".xml")) {
                var set = new AttachmentSet(PythonConstants.PythonCodeCoverageUri, "CodeCoverage");

                set.Attachments.Add(
                    new UriDataAttachment(new Uri(covPath + ".xml"), "Coverage Data")
                );
                frameworkHandle.RecordAttachments(new[] { set });

                File.Delete(covPath);
            } else {
                frameworkHandle.SendMessage(TestMessageLevel.Warning, Strings.Test_NoCoverageProduced);
            }
        }

        internal static string UpdateBestFile(string bestFile, string testFile) {
            if (bestFile == null || bestFile == testFile) {
                bestFile = testFile;
            } else if (!string.IsNullOrEmpty(bestFile)) {
                // Get common directory name, trim to the last \\ where we 
                // have things in common
                int lastSlash = 0;
                for (int i = 0; i < bestFile.Length && i < testFile.Length; i++) {
                    if (bestFile[i] != testFile[i]) {
                        bestFile = bestFile.Substring(0, lastSlash);
                        break;
                    } else if (bestFile[i] == '\\' || bestFile[i] == '/') {
                        lastSlash = i;
                    }
                }
            }

            return bestFile;
        }

        internal static string GetCoveragePath(IEnumerable<TestCase> tests) {
            string bestFile = null, bestClass = null, bestMethod = null;

            // Try and generate a friendly name for the coverage report.  We use
            // the filename, class, and method.  We include each one if we're
            // running from a single filename/class/method.  When we have multiple
            // we drop the identifying names.  If we have multiple files we
            // go to the top level directory...  If all else fails we do "pycov".
            foreach (var test in tests) {
                string testFile, testClass, testMethod;
                TestReader.ParseFullyQualifiedTestName(
                    test.FullyQualifiedName,
                    out testFile,
                    out testClass,
                    out testMethod
                );

                bestFile = CodeCoverage.UpdateBestFile(bestFile, test.CodeFilePath);
                if (bestFile != test.CodeFilePath) {
                    // Different files, don't include class/methods even
                    // if they happen to be the same.
                    bestClass = bestMethod = "";
                }

                bestClass = CodeCoverage.UpdateBest(bestClass, testClass);
                bestMethod = CodeCoverage.UpdateBest(bestMethod, testMethod);
            }

            string filename = "";

            if (!String.IsNullOrWhiteSpace(bestFile)) {
                if (ModulePath.IsPythonSourceFile(bestFile)) {
                    filename = ModulePath.FromFullPath(bestFile).ModuleName;
                } else {
                    filename = Path.GetFileName(bestFile);
                }
            } else {
                filename = "pycov";
            }

            if (!String.IsNullOrWhiteSpace(bestClass)) {
                filename += "_" + bestClass;
            }

            if (!String.IsNullOrWhiteSpace(bestMethod)) {
                filename += "_" + bestMethod;
            }

            filename += "_" + DateTime.Now.ToString("s").Replace(':', '_');

            return Path.Combine(Path.GetTempPath(), filename);
        }

        private static string UpdateBest(string best, string test) {
            if (best == null || best == test) {
                best = test;
            } else if (!string.IsNullOrEmpty(best)) {
                best = "";
            }

            return best;
        }

        private static XPathDocument Read(string xml) {
            var settings = new XmlReaderSettings();
            settings.XmlResolver = null;
            return new XPathDocument(XmlReader.Create(new StringReader(xml), settings));
        }
    }
}
