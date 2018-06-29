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

using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public interface ILanguageServerExtension {
        /// <summary>
        /// The name of the extension. Used to look up the current instance
        /// when processing extension command messages. If null or empty,
        /// the extension cannot be sent messages and may be garbage collected
        /// if it does not manage its own lifetime against the <see cref="IServer"/>
        /// instance provided to its provider.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Called when an extension command arrives for this extension.
        /// </summary>
        IReadOnlyDictionary<string, object> ExecuteCommand(string command, IReadOnlyDictionary<string, object> properties);
    }
}
