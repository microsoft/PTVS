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

namespace Microsoft.PythonTools.Interpreter {
    public interface IPythonWorkspaceContextProvider {
        /// <summary>
        /// Workspace has been closed in VS but the workspace context has not
        /// been disposed yet.
        /// </summary>
        event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceClosing;

        /// <summary>
        /// Workspace context is done closing and has been disposed.
        /// </summary>
        event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceClosed;

        /// <summary>
        /// Workspace context is created but not fully initialized yet.
        /// The settings can be accessed but the current factory is not set yet.
        /// </summary>
        event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceOpening;

        /// <summary>
        /// Workspace context is done initializing (current factory is set).
        /// </summary>
        event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceInitialized;

        /// <summary>
        /// The currently loaded workspace.
        /// </summary>
        IPythonWorkspaceContext Workspace { get; }
    }
}
