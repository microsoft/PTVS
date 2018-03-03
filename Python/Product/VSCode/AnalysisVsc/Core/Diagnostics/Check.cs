// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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