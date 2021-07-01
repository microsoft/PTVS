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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    class ProvidePeekSupportedContentTypeAttribute : RegistrationAttribute
    {
        private readonly string _contentType, _mappedType;

        /// <summary>
        /// contentType            mappedType
        /// ".aspx;.settings"   -> ".txt;"      .aspx & .settings files will use the .txt editor factory but set their content types normally.
        ///  ".fob"              -> ".oar"       .fob files will use the .oar exitor factory and the .oar content type
        ///  ".baz"              -> ";.txt"      .baz files will use normal editor factory and the .txt content type
        ///  ".foboar"           -> ";"          .foboar files will use the normal editor factory & content type (but be forced to open as copy). 
        ///  ".foboar"           -> ""
        /// </summary>
        public ProvidePeekSupportedContentTypeAttribute(string contentType, string mappedType)
        {
            _contentType = contentType;
            _mappedType = mappedType;
        }

        public override void Register(RegistrationContext context)
        {
            using (Key key = context.CreateKey("Peek\\SupportedContentTypes"))
            {
                key.SetValue(_contentType, _mappedType);
            }
        }

        public override void Unregister(RegistrationContext context)
        {
        }
    }
}
