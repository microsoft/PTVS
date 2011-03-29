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
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Repl {
    class ReplSpan {
        public readonly ITrackingSpan Span;
        public readonly ReplSpanKind Kind;

        public ReplSpan(ITrackingSpan span, ReplSpanKind kind) {
            Span = span;
            Kind = kind;
        }

        public override string ToString() {
            return String.Format("{0}: {1}", Kind, Span);
        }
    }

    enum ReplSpanKind {
        None,
        /// <summary>
        /// The span represents output from the program (standard output)
        /// </summary>
        Output,
        /// <summary>
        /// The span represents a prompt for input of code.
        /// </summary>
        Prompt,
        /// <summary>
        /// The span represents a 2ndary prompt for more code.
        /// </summary>
        SecondaryPrompt,
        /// <summary>
        /// The span represents code inputted after a prompt or secondary prompt.
        /// </summary>
        Language,
        /// <summary>
        /// The span represents the prompt for input for standard input (non code input)
        /// </summary>
        StandardInputPrompt,
        /// <summary>
        /// The span represents the input for a standard input (non code input)
        /// </summary>
        StandardInput,
    }
}
