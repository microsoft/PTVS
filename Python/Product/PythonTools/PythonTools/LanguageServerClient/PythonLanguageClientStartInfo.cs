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
using System.IO.Compression;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.LanguageServerClient {
    static class PythonLanguageClientStartInfo {
#if DEBUG
        // Since VS is 32-bit process, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        // gives us 32-bit program files so can't use that
        private static string DotNetExeFilePath = Path.Combine(
            @"C:\Program Files",
            "dotnet",
            "dotnet.exe"
        );
#endif
        //private static string FolderName {
        //    get {
        //        return Environment.Is64BitOperatingSystem ? "LanguageServer\\x64" : "LanguageServer\\x86";
        //    }
        //}

        private const string ExeName = "Microsoft.Python.LanguageServer.exe";
        private const string DllName = "Microsoft.Python.LanguageServer.dll";

        public static ProcessStartInfo Create(string folderPath) {
            var serverDllFilePath = Path.Combine(folderPath, DllName);
            var serverExeFilePath = Path.Combine(folderPath, ExeName);

            if (File.Exists(serverExeFilePath)) {
                return new ProcessStartInfo {
                    FileName = serverExeFilePath,
                    WorkingDirectory = folderPath,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
#if DEBUG
            } else if (File.Exists(serverDllFilePath)) {
                return new ProcessStartInfo {
                    FileName = DotNetExeFilePath,
                    WorkingDirectory = folderPath,
                    Arguments = '"' + serverDllFilePath + '"',
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
#endif
            } else {
                Debug.Fail("Could not find language server exe or dll");
                throw new FileNotFoundException("Could not find language server exe or dll", serverDllFilePath);
            }
        }

        //public static string DatabaseFolderPath =>
        //    GetLanguageServerFolder(FolderName);

        //public static string TypeshedFolderPath =>
        //    Path.Combine(GetLanguageServerFolder(FolderName), "Typeshed");

        public static string EnsureLanguageServer(string localAppDataFolderPath) {
            var lsZipFolderPath = Path.GetDirectoryName(typeof(PythonLanguageClientStartInfo).Assembly.Location);
            var lsZipFileName = "Python-Language-Server-win-{0}.zip".FormatInvariant(Environment.Is64BitOperatingSystem ? "x64" : "x86");
            var lsZipFilePath = Path.Combine(lsZipFolderPath, lsZipFileName);

            var lsExtractedFolderPath = Path.Combine(localAppDataFolderPath, "PythonLanguageServer");
            if (!Directory.Exists(lsExtractedFolderPath)) {
                if (File.Exists(lsZipFilePath)) {
                    ZipFile.ExtractToDirectory(lsZipFilePath, lsExtractedFolderPath);
                } else {
                    throw new FileNotFoundException("Python Language Server archive file not found.", lsZipFilePath);
                }
            }

            return lsExtractedFolderPath;
        }

        private static string GetLanguageServerFolder(string folder) {
            // To use your own build of the language server for developement purpose
            // - git clone https://github.com/microsoft/python-language-server/
            // - to debug initialization and get extra time to attach to the executable
            //   uncomment WAIT_FOR_DEBUGGER in language server's Program.cs
            // - in src folder, run: dotnet build
            // - return the absolute path to "python-language-server\output\bin\Debug"
            //return @"C:\Projects\GitHub\huguesv\python-language-server\output\bin\Debug";
            //return @"C:\Projects\GitHub\Microsoft\python-language-server\output\bin\Debug";
            return Path.Combine(
                Path.GetDirectoryName(typeof(PythonLanguageClientStartInfo).Assembly.Location),
                folder
            );
        }
    }
}
