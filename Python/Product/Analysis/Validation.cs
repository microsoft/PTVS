/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;

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
                try {
                    throw new ValidationException();
                } catch (ValidationException ex) {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        public static void Assert<T>(bool expression) where T : ValidationException, new() {
            if (!expression) {
                try {
                    throw new T();
                } catch (ValidationException ex) {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        public static void Assert(bool expression, string message, params object[] args) {
            if (!expression) {
                try {
                    throw new ValidationException(string.Format(message, args));
                } catch (ValidationException ex) {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
#endif
}
