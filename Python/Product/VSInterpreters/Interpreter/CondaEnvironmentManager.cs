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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    sealed class CondaEnvironmentManager : ICondaEnvironmentManager, IDisposable {
        private readonly SemaphoreSlim _working = new SemaphoreSlim(1);
        private bool _isDisposed;

        public string CondaPath { get; }

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] {
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        private CondaEnvironmentManager(string condaPath) {
            CondaPath = condaPath;
        }

        public void Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;
               _working.Dispose();
            }
        }

        public static CondaEnvironmentManager Create(IInterpreterRegistryService registry) {
            if (registry == null) {
                throw new ArgumentNullException(nameof(registry));
            }

            var condaPath = CondaUtils.GetLatestCondaExecutablePath(registry.Interpreters);
            if (!string.IsNullOrEmpty(condaPath)) {
                return new CondaEnvironmentManager(condaPath);
            }

            return null;
        }

        public async Task<bool> CreateAsync(string newEnvNameOrPath, PackageSpec[] packageSpecs, ICondaEnvironmentManagerUI ui, CancellationToken ct) {
            bool success = false;
            using (await _working.LockAsync(ct)) {
                var args = new[] {
                    "create",
                    IsAbsolutePath(newEnvNameOrPath) ? "-p" : "-n",
                    ProcessOutput.QuoteSingleArgument(newEnvNameOrPath),
                    "-y",
                }.Union(packageSpecs.Select(s => s.FullSpec));

                var operation = "conda " + string.Join(" ", args);

                ui?.OnOperationStarted(this, operation);
                ui?.OnOutputTextReceived(this, Strings.CondaCreateStarted.FormatUI(newEnvNameOrPath));
                try {
                    if (!PathUtils.IsValidPath(newEnvNameOrPath)) {
                        ui?.OnErrorTextReceived(this, Strings.CondaCreateInvalidNameOrPath.FormatUI(newEnvNameOrPath));
                        success = false;
                        return success;
                    }

                    success = await DoOperationAsync(args, ui, ct);
                    return success;
                } finally {
                    var msg = success ? Strings.CondaCreateSuccess : Strings.CondaCreateFailed;
                    ui?.OnOutputTextReceived(this, msg.FormatUI(newEnvNameOrPath));
                    ui?.OnOperationFinished(this, operation, success);
                }
            }
        }

        public async Task<bool> CreateFromEnvironmentFileAsync(string newEnvNameOrPath, string sourceEnvFilePath, ICondaEnvironmentManagerUI ui, CancellationToken ct) {
            bool success = false;
            using (await _working.LockAsync(ct)) {
                var args = new[] {
                    "env",
                    "create",
                    IsAbsolutePath(newEnvNameOrPath) ? "-p" : "-n",
                    ProcessOutput.QuoteSingleArgument(newEnvNameOrPath),
                    "-f",
                    ProcessOutput.QuoteSingleArgument(sourceEnvFilePath),
                };

                var operation = "conda " + string.Join(" ", args);

                ui?.OnOperationStarted(this, operation);
                ui?.OnOutputTextReceived(this, Strings.CondaCreateStarted.FormatUI(newEnvNameOrPath));
                try {
                    if (!PathUtils.IsValidPath(newEnvNameOrPath)) {
                        ui?.OnErrorTextReceived(this, Strings.CondaCreateInvalidNameOrPath.FormatUI(newEnvNameOrPath));
                        success = false;
                        return success;
                    }

                    if (!File.Exists(sourceEnvFilePath)) {
                        ui?.OnErrorTextReceived(this, Strings.CondaFileNotFoundError.FormatUI(sourceEnvFilePath));
                        success = false;
                        return success;
                    }

                    success = await DoOperationAsync(args, ui, ct);
                    return success;
                } finally {
                    var msg = success ? Strings.CondaCreateSuccess : Strings.CondaCreateFailed;
                    ui?.OnOutputTextReceived(this, msg.FormatUI(newEnvNameOrPath));
                    ui?.OnOperationFinished(this, operation, success);
                }
            }
        }

        public async Task<bool> CreateFromExistingEnvironmentAsync(string newEnvNameOrPath, string sourceEnvPath, ICondaEnvironmentManagerUI ui, CancellationToken ct) {
            bool success = false;
            using (await _working.LockAsync(ct)) {
                var args = new[] {
                    "create",
                    Path.IsPathRooted(newEnvNameOrPath) ? "-p" : "-n",
                    ProcessOutput.QuoteSingleArgument(newEnvNameOrPath),
                    "--clone",
                    ProcessOutput.QuoteSingleArgument(sourceEnvPath),
                };

                var operation = "conda " + string.Join(" ", args);

                ui?.OnOperationStarted(this, operation);
                ui?.OnOutputTextReceived(this, Strings.CondaCreateStarted.FormatUI(newEnvNameOrPath));
                try {
                    if (!PathUtils.IsValidPath(newEnvNameOrPath)) {
                        ui?.OnErrorTextReceived(this, Strings.CondaCreateInvalidNameOrPath.FormatUI(newEnvNameOrPath));
                        success = false;
                        return success;
                    }

                    if (!Directory.Exists(sourceEnvPath)) {
                        ui?.OnErrorTextReceived(this, Strings.CondaFolderNotFoundError.FormatUI(sourceEnvPath));
                        success = false;
                        return success;
                    }

                    success = await DoOperationAsync(args, ui, ct);
                    return success;
                } finally {
                    var msg = success ? Strings.CondaCreateSuccess : Strings.CondaCreateFailed;
                    ui?.OnOutputTextReceived(this, msg.FormatUI(newEnvNameOrPath));
                    ui?.OnOperationFinished(this, operation, success);
                }
            }
        }

        public async Task<bool> ExportEnvironmentFileAsync(string envPath, string destinationEnvFilePath, ICondaEnvironmentManagerUI ui, CancellationToken ct) {
            var args = new[] {
                "env",
                "export",
                "-p",
                ProcessOutput.QuoteSingleArgument(envPath),
            };
            return await ExportAsync(envPath, destinationEnvFilePath, args, ui, ct);
        }

        public async Task<bool> ExportExplicitSpecificationFileAsync(string envPath, string destinationSpecFilePath, ICondaEnvironmentManagerUI ui, CancellationToken ct) {
            var args = new[] {
                "list",
                "--explicit",
                "-p",
                ProcessOutput.QuoteSingleArgument(envPath),
            };

            return await ExportAsync(envPath, destinationSpecFilePath, args, ui, ct);
        }

        private async Task<bool> ExportAsync(string envPath, string destinationSpecFilePath, string[] args, ICondaEnvironmentManagerUI ui, CancellationToken ct) {
            bool success = false;
            using (await _working.LockAsync(ct)) {
                var operation = "conda " + string.Join(" ", args);

                ui?.OnOperationStarted(this, operation);
                ui?.OnOutputTextReceived(this, Strings.CondaExportStarted.FormatUI(envPath));
                try {
                    if (!PathUtils.IsValidPath(envPath)) {
                        ui?.OnErrorTextReceived(this, Strings.CondaFolderNotFoundError.FormatUI(envPath));
                        success = false;
                        return success;
                    }

                    var entries = new List<string>();
                    var capture = new ListRedirector(entries);
                    var redirector = new TeeRedirector(CondaEnvironmentManagerUIRedirector.Get(this, ui), capture);

                    success = await DoOperationAsync(args, ui, ct, redirector);
                    if (success) {
                        try {
                            using (var writer = new StreamWriter(destinationSpecFilePath, false, Encoding.UTF8)) {
                                foreach (var line in entries) {
                                    await writer.WriteLineAsync(line);
                                }
                            }
                        } catch (IOException ex) {
                            ui?.OnErrorTextReceived(this, ex.Message);
                            success = false;
                        } catch (UnauthorizedAccessException ex) {
                            ui?.OnErrorTextReceived(this, ex.Message);
                            success = false;
                        } catch (ArgumentException ex) {
                            ui?.OnErrorTextReceived(this, ex.Message);
                            success = false;
                        }
                    }

                    return success;
                } finally {
                    var msg = success ? Strings.CondaExportSuccess : Strings.CondaExportFailed;
                    ui?.OnOutputTextReceived(this, msg.FormatUI(envPath));
                    ui?.OnOperationFinished(this, operation, success);
                }
            }
        }

        public async Task<bool> DeleteAsync(string envPath, ICondaEnvironmentManagerUI ui, CancellationToken ct) {
            bool success = false;
            using (await _working.LockAsync(ct)) {
                var args = new[] {
                    "remove",
                    "-p",
                    ProcessOutput.QuoteSingleArgument(envPath),
                    "--all",
                    "-y",
                };

                var operation = "conda " + string.Join(" ", args);

                ui?.OnOperationStarted(this, operation);
                ui?.OnOutputTextReceived(this, Strings.CondaDeleteStarted.FormatUI(envPath));
                try {
                    if (!Directory.Exists(envPath)) {
                        ui?.OnErrorTextReceived(this, Strings.CondaFolderNotFoundError.FormatUI(envPath));
                        success = false;
                        return success;
                    }

                    success = await DoOperationAsync(args, ui, ct);
                    return success;
                } finally {
                    var msg = success ? Strings.CondaDeleteSuccess : Strings.CondaDeleteFailed;
                    ui?.OnOutputTextReceived(this, msg.FormatUI(envPath));
                    ui?.OnOperationFinished(this, operation, success);
                }
            }
        }

        /// <summary>
        /// Determine if we should use the conda -p argument
        /// (it's an absolute path), rather than -n (it's a name).
        /// </summary>
        private static bool IsAbsolutePath(string path) {
            try {
                return Path.IsPathRooted(path);
            } catch (ArgumentException) {
                return false;
            }
        }

        private async Task<bool> DoOperationAsync(
            IEnumerable<string> args,
            ICondaEnvironmentManagerUI ui,
            CancellationToken ct,
            Redirector redirector = null
        ) {
            bool success = false;
            try {
                using (var output = ProcessOutput.Run(
                    CondaPath,
                    args,
                    Path.GetDirectoryName(CondaPath),
                    UnbufferedEnv,
                    false,
                    redirector ?? CondaEnvironmentManagerUIRedirector.Get(this, ui),
                    quoteArgs: false,
                    elevate: false
                )) {
                    if (!output.IsStarted) {
                        return false;
                    }
                    var exitCode = await output;
                    success = exitCode == 0;
                }
                return success;
            } catch (IOException) {
                return false;
            }
        }

        sealed class CondaEnvironmentManagerUIRedirector : Redirector {
            private readonly ICondaEnvironmentManager _sender;
            private readonly ICondaEnvironmentManagerUI _ui;

            public static Redirector Get(ICondaEnvironmentManager sender, ICondaEnvironmentManagerUI ui) {
                if (ui != null) {
                    return new CondaEnvironmentManagerUIRedirector(sender, ui);
                }
                return null;
            }

            private CondaEnvironmentManagerUIRedirector(ICondaEnvironmentManager sender, ICondaEnvironmentManagerUI ui) {
                _sender = sender;
                _ui = ui;
            }

            public override void WriteErrorLine(string line) {
                _ui.OnErrorTextReceived(_sender, line + Environment.NewLine);
            }

            public override void WriteLine(string line) {
                _ui.OnOutputTextReceived(_sender, line + Environment.NewLine);
            }
        }
    }
}
