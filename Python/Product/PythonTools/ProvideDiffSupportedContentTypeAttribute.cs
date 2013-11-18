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
using Microsoft.VisualStudio.Shell;

namespace Microsoft {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    class ProvideDiffSupportedContentTypeAttribute : RegistrationAttribute {
        private readonly string _contentType, _mappedType;
        
        /// <summary>
        /// contentType            mappedType
        /// ".aspx;.settings"   -> ".txt;"      .aspx & .settings files will use the .txt editor factory but set their content types normally.
        ///  ".fob"              -> ".oar"       .fob files will use the .oar exitor factory and the .oar content type
        ///  ".baz"              -> ";.txt"      .baz files will use normal editor factory and the .txt content type
        ///  ".foboar"           -> ";"          .foboar files will use the normal editor factory & content type (but be forced to open as copy). 
        ///  ".foboar"           -> ""
        /// </summary>
        public ProvideDiffSupportedContentTypeAttribute(string contentType, string mappedType) {
            _contentType = contentType;
            _mappedType = mappedType;
        }

        public override void Register(RegistrationAttribute.RegistrationContext context) {
            using (Key key = context.CreateKey("Diff\\SupportedContentTypes")) {
                key.SetValue(_contentType, _mappedType);
            }
        }

        public override void Unregister(RegistrationAttribute.RegistrationContext context) {
        }
    }
}
