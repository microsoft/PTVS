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

using System.Threading.Tasks;
using System.Windows.Input;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Defines a command that also has an ExecuteAsync method.
    /// </summary>
    public interface IAsyncCommand : ICommand {
        /// <summary>
        /// Defines the method to be called when the command is invoked
        /// asynchronously.
        /// </summary>
        /// <param name="parameter">
        /// Data used by the command. If the command does not require data to be
        /// passed, this object can be set to null.
        /// </param>
        /// <returns>
        /// A task that will complete when the command has completed or faulted.
        /// </returns>
        Task ExecuteAsync(object parameter);
    }
}
