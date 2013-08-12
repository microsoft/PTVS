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

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Implemented by Python interpreters which support generating a completion database ahead of time.
    /// 
    /// This interface is implemented on a class which also implements IPythonInterpreterFactory.
    /// </summary>
    public interface IInterpreterWithCompletionDatabase {
        /// <summary>
        /// Generates the completion database.
        /// </summary>
        void GenerateCompletionDatabase(GenerateDatabaseOptions options, Action<int> onExit = null);

        /// <summary>
        /// Generates the completion database if it has not already been
        /// generated.  Called only if the user has not disabled the option to
        /// automatically generate a completion database.
        /// 
        /// The database should be generated in the background.
        /// </summary>
        void AutoGenerateCompletionDatabase();

        /// <summary>
        /// Gets whether or not the completion database is currently up to date.
        /// </summary>
        /// <remarks>New in 1.1</remarks>
        bool IsCurrent {
            get;
        }

        /// <summary>
        /// Called to inform the interpreter that its database requires
        /// regeneration.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        void NotifyInvalidDatabase();

        /// <summary>
        /// Called to inform the interpreter that its database is being
        /// generated. This is called to update the interpreter's internal state
        /// when generation was already running or is aborted without any other
        /// notification.
        /// </summary>
        /// <param name="isGenerating">True if the database is being generated;
        /// otherwise, false.</param>
        /// <returns>The previous generating state.</returns>
        /// <remarks>New in 2.0</remarks>
        bool NotifyGeneratingDatabase(bool isGenerating);

        /// <summary>
        /// Called to inform the interpreter that its database has been
        /// replaced.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        void NotifyNewDatabase();

        /// <summary>
        /// Returns logged information about the analysis of the interpreter's library.
        /// 
        /// May return null if no information is available, or a string containing error
        /// text if an error occurs.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        string GetAnalysisLogContent(IFormatProvider culture);

        /// <summary>
        /// Raised when the value of IsCurrent changes.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        event EventHandler IsCurrentChanged;

        /// <summary>
        /// Raised when the value returned by GetFriendlyIsCurrentReason() or
        /// GetIsCurrentReason() changes.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        event EventHandler IsCurrentReasonChanged;

        /// <summary>
        /// Called to manually trigger a refresh of <see cref="IsCurrent"/>.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        void RefreshIsCurrent();

        /// <summary>
        /// Returns a string describing the reason why IsCurrent has its current
        /// value. The string is formatted for display according to the provided
        /// culture and may use localized resources if available.
        /// 
        /// May return null if no information is available, or a string
        /// containing error text if an error occurs.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        string GetFriendlyIsCurrentReason(IFormatProvider culture);

        /// <summary>
        /// Returns a string describing the reason why IsCurrent has its current
        /// value.
        /// 
        /// This string may not be suitable for displaying directly to the user.
        /// It is always formatted using the invariant culture, but may use
        /// resources localized to the provided culture if they are available.
        /// 
        /// May return null if no information is available, or a string
        /// containing detailed exception information if an error occurs.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        string GetIsCurrentReason(IFormatProvider culture);
    }
}
