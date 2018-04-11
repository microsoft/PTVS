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
using System.Diagnostics;
using static System.FormattableString;

namespace Microsoft.DsTools.Core.Diagnostics {
    public static class Check {
        [DebuggerStepThrough]
        public static void ArgumentNull(string argumentName, object argument) {
            if (argument == null) {
                throw new ArgumentNullException(argumentName);
            }
        }

        [DebuggerStepThrough]
        public static void ArgumentStringNullOrEmpty(string argumentName, string argument) {
            Check.ArgumentNull(argumentName, argument);

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
        public static void InvalidOperation(Func<bool> predicate, string message = null) {
            if (!predicate()) {
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