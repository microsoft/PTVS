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
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools {
    [Serializable]
    public class NoInterpretersException : Exception {
        private readonly string _helpPage;

        public NoInterpretersException() : this(SR.GetString(SR.NoInterpretersAvailable)) { }
        public NoInterpretersException(string message) : base(message) { }
        public NoInterpretersException(string message, Exception inner) : base(message, inner) { }

        public NoInterpretersException(string message, string helpPage)
            : base(message) {
            _helpPage = helpPage;
        }

        public string HelpPage { get { return _helpPage; } }

        protected NoInterpretersException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
