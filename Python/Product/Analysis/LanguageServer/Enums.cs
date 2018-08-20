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

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed class SerializeAsAttribute : Attribute {
        public object Value { get; }

        public SerializeAsAttribute(object value) {
            Value = value;
        }
    }

    public enum TraceLevel {
        [SerializeAs("off")]
        Off,
        [SerializeAs("messages")]
        Messages,
        [SerializeAs("verbose")]
        Verbose
    }

    public enum SymbolKind {
        None = 0,
        File = 1,
        Module = 2,
        Namespace = 3,
        Package = 4,
        Class = 5,
        Method = 6,
        Property = 7,
        Field = 8,
        Constructor = 9,
        Enum = 10,
        Interface = 11,
        Function = 12,
        Variable = 13,
        Constant = 14,
        String = 15,
        Number = 16,
        Boolean = 17,
        Array = 18,
        Object = 19,
        Key = 20,
        Null = 21,
        EnumMember = 22,
        Struct = 23,
        Event = 24,
        Operator = 25,
        TypeParameter = 26
    }

    public enum TextDocumentSyncKind : int {
        None = 0,
        Full = 1,
        Incremental = 2
    }

    public enum MessageType : int {
        /// <summary>
        /// General language server output relevant to the user
        /// such as information on Python interpreter type.
        /// Does not conform to LSP definitions, Python LS specific.
        /// </summary>
        _General = 0,

        /// <summary>
        /// Language server errors.
        /// </summary>
        Error = 1,

        /// <summary>
        /// Language server warnings.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Language server internal information.
        /// </summary>
        Info = 3,

        /// <summary>
        /// Language server log-level diagnostic messages.
        /// </summary>
        Log = 4
    }

    public enum FileChangeType : int {
        Created = 1,
        Changed = 2,
        Deleted = 3
    }

    public enum WatchKind : int {
        Create = 1,
        Change = 2,
        Delete = 4
    }

    public enum TextDocumentSaveReason : int {
        Manual = 1,
        AfterDelay = 2,
        FocusOut = 3
    }

    public enum CompletionTriggerKind : int {
        Invoked = 1,
        TriggerCharacter = 2
    }

    public enum InsertTextFormat : int {
        PlainText = 1,
        Snippet = 2
    }

    public enum CompletionItemKind : int {
        None = 0,
        Text = 1,
        Method = 2,
        Function = 3,
        Constructor = 4,
        Field = 5,
        Variable = 6,
        Class = 7,
        Interface = 8,
        Module = 9,
        Property = 10,
        Unit = 11,
        Value = 12,
        Enum = 13,
        Keyword = 14,
        Snippet = 15,
        Color = 16,
        File = 17,
        Reference = 18,
        Folder = 19,
        EnumMember = 20,
        Constant = 21,
        Struct = 22,
        Event = 23,
        Operator = 24,
        TypeParameter = 25
    }

    public enum DocumentHighlightKind : int {
        Text = 1,
        Read = 2,
        Write = 3
    }

    // Not in the LSP spec.
    public enum ReferenceKind : int {
        Definition = 1,
        Reference = 2,
        Value = 3
    }
}
