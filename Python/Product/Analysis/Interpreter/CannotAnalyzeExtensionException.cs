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
using System.Runtime.Serialization;

namespace Microsoft.PythonTools.Interpreter {
    [Serializable]
    public class CannotAnalyzeExtensionException : Exception {
        public CannotAnalyzeExtensionException() : base() { }
        public CannotAnalyzeExtensionException(string msg) : base(msg) { }
        public CannotAnalyzeExtensionException(string message, Exception innerException)
            : base(message, innerException) {
        }

        protected CannotAnalyzeExtensionException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
