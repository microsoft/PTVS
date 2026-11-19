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

using System;
using System.Runtime.Serialization;
#if DEV18_OR_LATER
using System.Text.Json.Serialization;
using DocumentUriConverter = Microsoft.PythonTools.LanguageServerClient.ProtocolJsonSerialization.DocumentUriConverter;
#else
using Newtonsoft.Json;
using DocumentUriConverter = Microsoft.VisualStudio.LanguageServer.Protocol.DocumentUriConverter;
#endif

namespace Microsoft.PythonTools.LanguageServerClient.WorkspaceFolderChanged {
    [DataContract]
    class WorkspaceFolder {
        /**
         * The associated URI for this workspace folder.
         */
        [DataMember]
        [JsonConverter(typeof(DocumentUriConverter))]
        public Uri uri { get; set; }

        /**
         * The name of the workspace folder. Used to refer to this
         * workspace folder in the user interface.
         */
        [DataMember]
        public string name { get; set; }
    }
}
