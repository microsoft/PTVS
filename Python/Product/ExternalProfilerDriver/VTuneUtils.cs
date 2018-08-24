// Python Tools for Visual Studio
// Copyright(c) 2018 Intel Corporation.  All rights reserved.
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver {
    public static class Utils {
        // from an idea in https://github.com/dotnet/corefx/issues/3093
        public static IEnumerable<T> Emit<T>(T element) {
          return Enumerable.Repeat(element, 1);
        }

        /// <summary>
        /// Finds file name <param>fname</param> under directory <param>rootDir</param> (recursively)
        /// </summary>
        public static string FindFileInDir(string fname, string rootDir) {
            var candidates = Directory.GetFiles(rootDir, fname, SearchOption.AllDirectories);
            if (candidates.Length <= 0) {
                throw new FileNotFoundException($"{Strings.ErrorMsgFileDoesNotExist} : {rootDir}/{fname}");
            } else {
                return candidates[0];
            }
        }

        public static IEnumerable<string> ReadFromFile(string filePath) {
            string line;
            using (var reader = File.OpenText(filePath)) {
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        public static IEnumerable<string> QuickExecute(string cmd, string args) {
            ProcessStartInfo psi = new ProcessStartInfo(cmd) {
                UseShellExecute = false,
                Arguments = args,
                RedirectStandardOutput = true
            };

            Process process = new Process {
                StartInfo = psi,
            };

            process.Start();
            while (!process.StandardOutput.EndOfStream) {
                yield return process.StandardOutput.ReadLine();
            }
            process.WaitForExit();
        }
    }
}