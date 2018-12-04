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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.PythonTools.Infrastructure {
    static class ExceptionExtensions {
        /// <summary>
        /// Returns true if an exception should not be handled by logging code.
        /// </summary>
        public static bool IsCriticalException(this Exception ex) {
            return ex is StackOverflowException ||
                ex is OutOfMemoryException ||
                ex is ThreadAbortException ||
                ex is AccessViolationException ||
                ex is CriticalException;
        }

        public static string ToUnhandledExceptionMessage(
            this Exception ex,
            Type callerType,
            [CallerFilePath] string callerFile = null,
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerName = null
        ) {
            if (string.IsNullOrEmpty(callerName)) {
                callerName = callerType != null ? callerType.FullName : string.Empty;
            } else if (callerType != null) {
                callerName = callerType.FullName + "." + callerName;
            }

            try {
                return string.Format(
                    Strings.UnhandledException,
                    ex,
                    callerFile ?? String.Empty,
                    callerLineNumber,
                    callerName
                );
            } catch (Exception ex2) {
                // Never throw out of this function.
                try {
                    return ex2.ToString();
                } catch (Exception) {
                    // Never -- NEVER -- throw out of this function.
                    return "Unhandled exception constructing exception message.";
                }
            }
        }

    }

    /// <summary>
    /// An exception that should not be silently handled and logged.
    /// </summary>
    [Serializable]
    class CriticalException : Exception {
        public CriticalException() { }
        public CriticalException(string message) : base(message) { }
        public CriticalException(string message, Exception inner) : base(message, inner) { }
        protected CriticalException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}