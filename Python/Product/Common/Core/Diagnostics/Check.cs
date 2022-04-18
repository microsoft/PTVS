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
using System.Runtime.CompilerServices;
using static System.FormattableString;

namespace Microsoft.PythonTools.Common.Core.Diagnostics {
    public static class Check {
        [DebuggerStepThrough]
        public static void FieldType<T>(string fieldName, object fieldValue) {
            if (!(fieldValue is T)) {
                throw new InvalidOperationException($"Field {fieldName} must be of type {fieldValue}");
            }
        }

        [DebuggerStepThrough]
        public static void ArgumentOfType<T>(string argumentName, object argument, [CallerMemberName] string callerName = null) {
            ArgumentNotNull(argumentName, argument);

            if (!(argument is T)) {
                throw new ArgumentException($"Argument {argumentName} of method {callerName} must be of type {typeof(T)}");
            }
        }

        [DebuggerStepThrough]
        public static void ArgumentNotNull(string argumentName, object argument) {
            if (argument is null) {
                throw new ArgumentNullException(argumentName);
            }
        }

        [DebuggerStepThrough]
        public static void ArgumentNotNullOrEmpty(string argumentName, string argument) {
            ArgumentNotNull(argumentName, argument);

            if (string.IsNullOrEmpty(argument)) {
                throw new ArgumentException(argumentName);
            }
        }

        [DebuggerStepThrough]
        public static void ArgumentOutOfRange(string argumentName, Func<bool> predicate) {
            if (predicate()) {
                throw new ArgumentOutOfRangeException(argumentName);
            }
        }

        [DebuggerStepThrough]
        public static void ArgumentOutOfRange(string argumentName, bool isInRange) {
            if (isInRange) {
                throw new ArgumentOutOfRangeException(argumentName);
            }
        }

        [DebuggerStepThrough]
        public static void ArgumentOutOfRange<T>(string argumentName, T value, params T[] allowedValues) where T : Enum {
            if (Array.IndexOf(allowedValues, value) == -1) {
                throw new ArgumentOutOfRangeException(argumentName);
            }
        }

        [DebuggerStepThrough]
        public static void InvalidOperation(Func<bool> predicate, string message = null) {
            if (!predicate()) {
                throw new InvalidOperationException(message ?? string.Empty);
            }
        }

        [DebuggerStepThrough]
        public static void InvalidOperation(bool condition, string message = null) {
            if (!condition) {
                throw new InvalidOperationException(message ?? string.Empty);
            }
        }

        [DebuggerStepThrough]
        public static void Argument(string argumentName, Func<bool> predicate) {
            if (!predicate()) {
                throw new ArgumentException(Invariant($"{argumentName} is not valid"));
            }
        }
    }
}
