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
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Options {
    // TODO: We should switch to a scheme which takes strings / returns object for options so they're extensible w/o reving the interface
    [Guid("BACA2500-5EA7-4075-8D02-647EAC0BC6E3")]
    public interface IPythonOptions {
        IPythonIntellisenseOptions Intellisense {
            get;
        }

        /// <summary>
        /// Gets interactive options for the given interpreter.  The interpreter should be either the
        /// interpreter description for a user installed interpreter or the description plus the 
        /// version.
        /// </summary>
        IPythonInteractiveOptions GetInteractiveOptions(string interpreterName);

        bool PromptBeforeRunningWithBuildErrorSetting {
            get;
            set;
        }

        bool AutoAnalyzeStandardLibrary {
            get;
            set;
        }

        Severity IndentationInconsistencySeverity {
            get;
            set;
        }

        bool TeeStandardOutput {
            get;
            set;
        }

        bool WaitOnAbnormalExit {
            get;
            set;
        }

        bool WaitOnNormalExit {
            get;
            set;
        }
    }

    [Guid("77179244-BBD7-4AA2-B27B-F2CCC679953A")]
    public interface IPythonIntellisenseOptions {
        bool AddNewLineAtEndOfFullyTypedWord { get; set; }
        bool EnterCommitsCompletion { get; set; }
        bool UseMemberIntersection { get; set; }
        string CompletionCommittedBy { get; set; }
    }

    [Guid("6DCCD6E9-FAC4-4EFA-9243-AE1A71D8923D")]
    public interface IPythonInteractiveOptions {
        string PrimaryPrompt {
            get;
            set;
        }

        string SecondaryPrompt {
            get;
            set;
        }

        bool UseInterpreterPrompts {
            get;
            set;
        }

        bool InlinePrompts {
            get;
            set;
        }

        bool ReplSmartHistory {
            get;
            set;
        }

        string ReplIntellisenseMode {
            get;
            set;
        }

        string StartupScript {
            get;
            set;
        }

        string ExecutionMode {
            get;
            set;
        }

        string InterpreterArguments {
            get;
            set;
        }
    }
}
