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
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Analysis {
#if FULL_VALIDATION || DEBUG
    [Serializable]
    public class ValidationException : Exception {
        public ValidationException() { }
        public ValidationException(string message) : base(message) { }
        public ValidationException(string message, Exception inner) : base(message, inner) { }
        protected ValidationException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }

    [Serializable]
    public class ChangeCountExceededException : ValidationException {
        public ChangeCountExceededException() { }
        public ChangeCountExceededException(string message) : base(message) { }
        public ChangeCountExceededException(string message, Exception inner) : base(message, inner) { }
        protected ChangeCountExceededException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }

    static class Validation {
        public static void Assert(bool expression) {
            if (!expression) {
                System.Diagnostics.Debugger.Launch();
                try {
                    throw new ValidationException();
                } catch (ValidationException ex) {
                    Console.Error.WriteLine(ex.ToString());
                }
            }
        }

        public static void Assert<T>(bool expression) where T : ValidationException, new() {
            if (!expression) {
                System.Diagnostics.Debugger.Launch();
                try {
                    throw new T();
                } catch (ValidationException ex) {
                    Console.Error.WriteLine(ex.ToString());
                }
            }
        }

        public static void Assert(bool expression, string message, params object[] args) {
            if (!expression) {
                System.Diagnostics.Debugger.Launch();
                try {
                    throw new ValidationException(message.FormatInvariant(args));
                } catch (ValidationException ex) {
                    Console.Error.WriteLine(ex.ToString());
                }
            }
        }
    }
#endif
}
