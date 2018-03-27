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
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Telemetry;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Infrastructure {
    static class VSTaskExtensions {
        private static readonly HashSet<string> _displayedMessages = new HashSet<string>();
        //internal static IPythonToolsLogger _logger;

        /// <summary>
        /// Logs an unhandled exception. May display UI to the user informing
        /// them that an error has been logged.
        /// </summary>
        public static void ReportUnhandledException(
            this Exception ex,
            IServiceProvider site,
            Type callerType = null,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerName = null,
            bool allowUI = true
        ) {
            var message = ex.ToUnhandledExceptionMessage(callerType, callerFile, callerLineNumber, callerName);

            // Send the message to the trace listener in case there is
            // somebody out there listening.
            Trace.TraceError(message);

            string logFile;
            try {
                logFile = ActivityLog.LogFilePath;
            } catch (InvalidOperationException) {
                logFile = null;
            }

            // In debug builds let the user know immediately
            Debug.Fail(message);

            // Log to Windows Event log. If this fails, there is nothing we can
            // do. In debug builds we have already asserted by this point.
            try {
                EventLog.WriteEntry(Strings.ProductTitle, message, EventLogEntryType.Error, 9999);
            } catch (ArgumentException) {
                // Misconfigured source or the message is too long.
            } catch (SecurityException) {
                // Source does not exist and user cannot create it
            } catch (InvalidOperationException) {
                // Unable to open the registry key for the log
            } catch (Win32Exception) {
                // Unknown error prevented writing to the log
            }

            try {
                ActivityLog.LogError(Strings.ProductTitle, message);
            } catch (InvalidOperationException) {
                // Activity Log is unavailable.
                logFile = null;
            }

            try {
                var telemetry = TelemetryService.DefaultSession;
                if (telemetry != null) {
                    var evt = new TelemetryEvent("vs/python/UnhandledException");
                    evt.Properties["VS.Python.FullExceptionName"] = ex.GetType().FullName;
                    evt.Properties["VS.Python.Details"] = message;
                    evt.Properties["VS.Python.UserNotified"] = allowUI;
                    telemetry.PostEvent(evt);
                }
            } catch (Exception e) {
                Debug.Fail(e.Message);
            }

            //try {
            //    _logger?.LogEvent(PythonLogEvent.UnhandledException, new UnhandledExceptionInfo() {
            //        FullName  = ex.GetType().FullName,
            //        Details = message,
            //        UserNotified = allowUI,
            //    });
            //} catch (Exception e) {
            //    Debug.Fail(e.Message);
            //}

            if (allowUI) {
                lock (_displayedMessages) {
                    var key = "{0}:{1}:{2}".FormatInvariant(callerFile, callerLineNumber, ex.GetType().Name);
                    if (_displayedMessages.Add(key)) {
                        // First time we've seen this error, so let the user know
                        // Prefer the dialog with our issue tracker and exception
                        // details, but if we don't have a site available then
                        // refer the user to ActivityLog.xml.
                        if (site != null) {
                            TaskDialog.ForException(site, ex, issueTrackerUrl: Strings.IssueTrackerUrl).ShowModal();
                        } else if (!string.IsNullOrEmpty(logFile)) {
                            MessageBox.Show(Strings.SeeActivityLog.FormatUI(logFile), Strings.ProductTitle);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Waits for a task to complete and logs all exceptions except those
        /// that return true from <see cref="IsCriticalException"/>, which are
        /// rethrown.
        /// </summary>
        public static T WaitAndHandleAllExceptions<T>(
            this Task<T> task,
            IServiceProvider site,
            Type callerType = null,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerName = null,
            bool allowUI = true
        ) {
            return task.HandleAllExceptions(site, callerType, callerFile, callerLineNumber, callerName, allowUI)
                .WaitAndUnwrapExceptions();
        }


        /// <summary>
        /// Logs all exceptions from a task except those that return true from
        /// <see cref="IsCriticalException"/>, which are rethrown.
        /// If an exception is thrown, <c>default(T)</c> is returned.
        /// </summary>
        public static async Task<T> HandleAllExceptions<T>(
            this Task<T> task,
            IServiceProvider site,
            Type callerType = null,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerName = null,
            bool allowUI = true
        ) {
            var result = default(T);
            try {
                result = await task;
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }

                ex.ReportUnhandledException(site, callerType, callerFile, callerLineNumber, callerName, allowUI);
            }
            return result;
        }

        /// <summary>
        /// Waits for a task to complete and logs all exceptions except those
        /// that return true from <see cref="IsCriticalException"/>, which are
        /// rethrown.
        /// </summary>
        public static void WaitAndHandleAllExceptions(
            this Task task,
            IServiceProvider site,
            Type callerType = null,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerName = null,
            bool allowUI = true
        ) {
            task.HandleAllExceptions(site, callerType, callerFile, callerLineNumber, callerName, allowUI)
                .WaitAndUnwrapExceptions();
        }


        /// <summary>
        /// Logs all exceptions from a task except those that return true from
        /// <see cref="IsCriticalException"/>, which are rethrown.
        /// </summary>
        public static async Task HandleAllExceptions(
            this Task task,
            IServiceProvider site,
            Type callerType = null,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerName = null,
            bool allowUI = true
        ) {
            try {
                await task;
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }

                ex.ReportUnhandledException(site, callerType, callerFile, callerLineNumber, callerName, allowUI);
            }
        }
    }
}