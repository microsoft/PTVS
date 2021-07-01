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

namespace Microsoft.PythonTools.Repl
{
    interface IMultipleScopeEvaluator
    {
        /// <summary>
        /// Sets the current scope to the given name.
        /// </summary>
        void SetScope(string scopeName);

        /// <summary>
        /// Gets the list of scopes which can be changed to.
        /// </summary>
        IEnumerable<string> GetAvailableScopes();

        /// <summary>
        /// Gets the current scope name.
        /// </summary>
        string CurrentScopeName { get; }

        /// <summary>
        /// Gets the path to the file that defines the current scope. May be
        /// null if no file exists.
        /// </summary>
        string CurrentScopePath { get; }

        /// <summary>
        /// Event is fired when the list of available scopes changes.
        /// </summary>
        event EventHandler<EventArgs> AvailableScopesChanged;

        /// <summary>
        /// Event is fired when support of multiple scopes has changed.
        /// </summary>
        event EventHandler<EventArgs> MultipleScopeSupportChanged;

        /// <summary>
        /// Returns true if multiple scope support is currently enabled, false if not.
        /// </summary>
        bool EnableMultipleScopes
        {
            get;
        }
    }
}
