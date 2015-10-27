// Visual Studio Shared Project
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
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools.Project;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudioTools {
    static class VsTaskExtensions {
        private static readonly HashSet<string> _displayedMessages = new HashSet<string>();

        /// <summary>
        /// Logs an unhandled exception. May display UI to the user informing
        /// them that an error has been logged.
        /// </summary>
        public static void ReportUnhandledException(
            Exception ex,
            string productTitle,
            Type callerType = null,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerName = null
        ) {
            var message = SR.GetUnhandledExceptionString(ex, callerType, callerFile, callerLineNumber, callerName);
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
                EventLog.WriteEntry(productTitle, message, EventLogEntryType.Error, 9999);
            } catch (ArgumentException) {
                // Misconfigured source or the message is too long.
            } catch (SecurityException) {
                // Source does not exist and user cannot create it
            } catch (InvalidOperationException) {
                // Unable to open the registry key for the log
            } catch (Win32Exception) {
                // Unknown error prevented writing to the log
            }

            lock (_displayedMessages) {
                if (!string.IsNullOrEmpty(logFile) &&
                    _displayedMessages.Add(string.Format("{0}:{1}", callerFile, callerLineNumber))) {
                    // First time we've seen this error, so let the user know
                    MessageBox.Show(SR.GetString(SR.SeeActivityLog, logFile), productTitle);
                }
            }

            try {
                ActivityLog.LogError(productTitle, message);
            } catch (InvalidOperationException) {
                // Activity Log is unavailable.
            }
        }

        /// <summary>
        /// Waits for a task to complete and logs all exceptions except those
        /// that return true from <see cref="IsCriticalException"/>, which are
        /// rethrown.
        /// </summary>
        public static T WaitAndHandleAllExceptions<T>(
            this Task<T> task,
            string productTitle,
            Type callerType = null,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerName = null
        ) {
            return task.HandleAllExceptions(productTitle, callerType, callerFile, callerLineNumber, callerName)
                .WaitAndUnwrapExceptions();
        }


        /// <summary>
        /// Logs all exceptions from a task except those that return true from
        /// <see cref="IsCriticalException"/>, which are rethrown.
        /// If an exception is thrown, <c>default(T)</c> is returned.
        /// </summary>
        public static async Task<T> HandleAllExceptions<T>(
            this Task<T> task,
            string productTitle,
            Type callerType = null,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerName = null
        ) {
            var result = default(T);
            try {
                result = await task;
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }

                ReportUnhandledException(ex, productTitle, callerType, callerFile, callerLineNumber, callerName);
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
            string productTitle,
            Type callerType = null,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerName = null
        ) {
            task.HandleAllExceptions(productTitle, callerType, callerFile, callerLineNumber, callerName)
                .WaitAndUnwrapExceptions();
        }


        /// <summary>
        /// Logs all exceptions from a task except those that return true from
        /// <see cref="IsCriticalException"/>, which are rethrown.
        /// </summary>
        public static async Task HandleAllExceptions(
            this Task task,
            string productTitle,
            Type callerType = null,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerName = null
        ) {
            try {
                await task;
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }

                ReportUnhandledException(ex, productTitle, callerType, callerFile, callerLineNumber, callerName);
            }
        }
    }
}