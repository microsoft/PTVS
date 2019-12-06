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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal class PythonLanguageServerDotNetCore : PythonLanguageServer {
        private readonly IServiceProvider _site;

        private const string ExeName = "Microsoft.Python.LanguageServer.exe";
        private const string DllName = "Microsoft.Python.LanguageServer.dll";

        public PythonLanguageServerDotNetCore(IServiceProvider site) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
        }

#if DEBUG
        // Since VS is 32-bit process, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        // gives us 32-bit program files so can't use that
        private static string DotNetExeFilePath = Path.Combine(
            @"C:\Program Files",
            "dotnet",
            "dotnet.exe"
        );
#endif

        public async override Task<Connection> ActivateAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var serverFolderPath = GetServerLocation();

            await Task.Yield();

            var info = CreateStartInfo(serverFolderPath);

            var process = new Process {
                StartInfo = info
            };

            if (process.Start()) {
                return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            }

            return null;
        }

        public override object CreateInitializationOptions(string interpreterPath, string interpreterVersion, string rootPath, IEnumerable<string> searchPaths) {
            var serverFolderPath = GetServerLocation();

            return new PythonInitializationOptions {
                // we need to read from the workspace settings in order to populate this correctly
                // (or from the project)
                interpreter = new PythonInitializationOptions.Interpreter {
                    properties = new PythonInitializationOptions.Interpreter.InterpreterProperties {
                        InterpreterPath = interpreterPath,
                        Version = interpreterVersion,
                        DatabasePath = serverFolderPath,
                    }
                },
                searchPaths = searchPaths.ToArray(),
                typeStubSearchPaths = new[] {
                        Path.Combine(serverFolderPath, "Typeshed")
                    },
                excludeFiles = new[] {
                        "**/Lib/**",
                        "**/site-packages/**",
                        "**/node_modules",
                        "**/bower_components",
                        "**/.git",
                        "**/.svn",
                        "**/.hg",
                        "**/CVS",
                        "**/.DS_Store",
                        "**/.git/objects/**",
                        "**/.git/subtree-cache/**",
                        "**/node_modules/*/**",
                        ".vscode/*.py",
                        "**/site-packages/**/*.py"
                    },
                rootPathOverride = rootPath
            };
        }

        private static ProcessStartInfo CreateStartInfo(string folderPath) {
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

        private string GetServerLocation() {
            ThreadHelper.ThrowIfNotOnUIThread();

            var shell = _site.GetService<SVsShell, IVsShell>();
            shell.GetProperty((int)__VSSPROPID4.VSSPROPID_LocalAppDataDir, out object localAppDataFolderPath);

            var lsZipFolderPath = Path.GetDirectoryName(typeof(PythonLanguageServerDotNetCore).Assembly.Location);
            var lsZipFileName = "Python-Language-Server-win-{0}.zip".FormatInvariant(Environment.Is64BitOperatingSystem ? "x64" : "x86");
            var lsZipFilePath = Path.Combine(lsZipFolderPath, lsZipFileName);

            var lsExtractedFolderPath = Path.Combine((string)localAppDataFolderPath, "PythonLanguageServer");
            if (!Directory.Exists(lsExtractedFolderPath)) {
                if (File.Exists(lsZipFilePath)) {
                    ZipFile.ExtractToDirectory(lsZipFilePath, lsExtractedFolderPath);
                } else {
                    throw new FileNotFoundException("Python Language Server archive file not found.", lsZipFilePath);
                }
            }

            return lsExtractedFolderPath;
        }

        /// <summary>
        /// Required layout for the initializationOptions member of initializeParams
        /// Match PythonInitializationOptions in https://github.com/microsoft/python-language-server/blob/master/src/LanguageServer/Impl/Protocol/Classes.cs
        /// </summary>
        [Serializable]
        public sealed class PythonInitializationOptions {
            [Serializable]
            public struct Interpreter {
                public sealed class InterpreterProperties {
                    public string Version;
                    public string InterpreterPath;
                    public string DatabasePath;
                }
                public InterpreterProperties properties;
            }
            public Interpreter interpreter;

            /// <summary>
            /// Paths to search when attempting to resolve module imports.
            /// </summary>
            public string[] searchPaths = Array.Empty<string>();

            /// <summary>
            /// Paths to search for module stubs.
            /// </summary>
            public string[] typeStubSearchPaths = Array.Empty<string>();

            /// <summary>
            /// Glob pattern of files and folders to exclude from loading
            /// into the Python analysis engine.
            /// </summary>
            public string[] excludeFiles = Array.Empty<string>();

            /// <summary>
            /// Glob pattern of files and folders under the root folder that
            /// should be loaded into the Python analysis engine.
            /// </summary>
            public string[] includeFiles = Array.Empty<string>();

            /// <summary>
            /// Path to a writable folder where analyzer can cache its data.
            /// </summary>
            public string cacheFolderPath;

            /// <summary>
            /// Root path override (used by PTVS).
            /// </summary>
            public string rootPathOverride;
        }

        public sealed class InformationDisplayOptions {
            public string preferredFormat;
            public bool trimDocumentationLines;
            public int maxDocumentationLineLength;
            public bool trimDocumentationText;
            public int maxDocumentationTextLength;
            public int maxDocumentationLines;
        }
    }
}
