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
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools {
    [Serializable]
    public class MissingInterpreterException : Exception {
        private readonly string _helpPage;

        public MissingInterpreterException(string message) : base(message) { }
        public MissingInterpreterException(string message, Exception inner) : base(message, inner) { }

        public MissingInterpreterException(string message, string helpPage)
            : base(message) {
            _helpPage = helpPage;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context) {
            base.GetObjectData(info, context);
            if (!string.IsNullOrEmpty(_helpPage)) {
                try {
                    info.AddValue("HelpPage", _helpPage);
                } catch (SerializationException) {
                }
            }
        }

        public string HelpPage { get { return _helpPage; } }

        protected MissingInterpreterException(SerializationInfo info, StreamingContext context)
            : base(info, context) {
            try {
                _helpPage = info.GetString("HelpPage");
            } catch (SerializationException) {
            }
        }
    }
}
