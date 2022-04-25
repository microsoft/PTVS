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
using Microsoft.PythonTools.Common.Core.IO;
using Microsoft.PythonTools.Common.Core.Logging;

namespace Microsoft.PythonTools.Common.Core.Extensions {
    public static class IOExtensions {
        /// <summary>
        /// Deletes a file, making multiple attempts and suppressing any
        /// IO-related errors.
        /// </summary>
        /// <param name="fs">File system object.</param>
        /// <param name="path">The full path of the file to delete.</param>
        /// <returns>True if the file was successfully deleted.</returns>
        public static bool DeleteFileWithRetries(this IFileSystem fs, string path, int maxRetries = 5) {
            for (var retries = maxRetries; retries > 0; --retries) {
                try {
                    if (fs.FileExists(path)) {
                        fs.SetFileAttributes(path, FileAttributes.Normal);
                        fs.DeleteFile(path);
                        return true;
                    }
                } catch (UnauthorizedAccessException) {
                } catch (IOException) {
                }
                Thread.Sleep(10);
            }
            return !fs.FileExists(path);
        }

        /// <summary>
        /// Recursively deletes a directory, making multiple attempts
        /// and suppressing any IO-related errors.
        /// </summary>
        /// <param name="path">The full path of the directory to delete.</param>
        /// <returns>True if the directory was successfully deleted.</returns>
        public static bool DeleteDirectoryWithRetries(this IFileSystem fs, string path, int maxRetries = 2) {
            for (var retries = maxRetries; retries > 0; --retries) {
                try {
                    fs.DeleteDirectory(path, true);
                    return true;
                } catch (UnauthorizedAccessException) {
                } catch (IOException) {
                }
            }

            // Regular delete failed, so let's start removing the contents ourselves
            var directories = fs.GetDirectories(path).OrderByDescending(p => p.Length).ToArray();
            foreach (var dir in directories) {
                foreach (var f in fs.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)) {
                    fs.DeleteFile(f);
                }

                try {
                    fs.DeleteDirectory(dir, true);
                } catch (UnauthorizedAccessException) {
                } catch (IOException) {
                }
            }

            // If we get to this point and the directory still exists, there's
            // likely nothing we can do.
            return !fs.DirectoryExists(path);
        }

        public static FileStream OpenWithRetry(this IFileSystem fs, string file, FileMode mode, FileAccess access, FileShare share, ILogger log = null) {
            // Retry for up to one second
            var create = mode != FileMode.Open;
            for (var retries = 100; retries > 0; --retries) {
                try {
                    return new FileStream(file, mode, access, share);
                } catch (FileNotFoundException) when (!create) {
                    return null;
                } catch (DirectoryNotFoundException) when (!create) {
                    return null;
                } catch (UnauthorizedAccessException uaex) {
                    log?.Log(TraceEventType.Verbose, "Unable to open file ", file, uaex.Message);
                    Thread.Sleep(10);
                } catch (IOException) {
                    if (create) {
                        var dir = Path.GetDirectoryName(file);
                        try {
                            fs.CreateDirectory(dir);
                        } catch (IOException ioex) {
                            log?.Log(TraceEventType.Verbose, "Unable to create directory ", dir, ioex.Message);
                            Thread.Sleep(10);
                        }
                    } else {
                        Thread.Sleep(10);
                    }
                } catch (NotSupportedException nsx) {
                    log?.Log(TraceEventType.Verbose, "Unable to open file ", file, nsx.Message);
                    return null;
                }
            }
            return null;
        }

        public static string ReadTextWithRetry(this IFileSystem fs, string file) {
            // Retry for up to one second
            for (var retries = 100; retries > 0; --retries) {
                try {
                    return fs.ReadAllText(file);
                } catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is ObjectDisposedException) {
                    Thread.Sleep(10);
                }
            }
            return null;
        }

        public static void WriteTextWithRetry(this IFileSystem fs, string filePath, string text, ILogger log = null) {
            Exception ex = null;
            for (var retries = 100; retries > 0; --retries) {
                try {
                    using (var stream = fs.OpenWithRetry(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, log)) {
                        if (stream != null) {
                            var bytes = Encoding.UTF8.GetBytes(text);
                            stream.Write(bytes, 0, bytes.Length);
                            return;
                        }
                    }
                } catch (IOException ioex) {
                    ex = ioex;
                } catch (UnauthorizedAccessException uaex) {
                    ex = uaex;
                }
                Thread.Sleep(10);
            }

            log?.Log(TraceEventType.Verbose, "Unable to write to ", filePath, ex?.Message ?? "Unknown exception");

            try {
                fs.DeleteFile(filePath);
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
