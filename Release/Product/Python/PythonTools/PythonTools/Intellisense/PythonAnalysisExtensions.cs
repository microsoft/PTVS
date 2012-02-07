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

namespace Microsoft.PythonTools.Intellisense {
    public static class PythonAnalysisExtensions {
        /// <summary>
        /// Gets a ExpressionAnalysis for the expression at the provided span.  If the span is in
        /// part of an identifier then the expression is extended to complete the identifier.
        /// </summary>
        public static ExpressionAnalysis AnalyzeExpression(this ITextSnapshot snapshot, ITrackingSpan span, bool forCompletion = true) {
            return ProjectAnalyzer.AnalyzeExpression(snapshot, span, forCompletion);
        }

        /// <summary>
        /// Gets a list of signatuers available for the expression at the provided location in the snapshot.
        /// </summary>
        public static SignatureAnalysis GetSignatures(this ITextSnapshot snapshot, ITrackingSpan span) {
            return ProjectAnalyzer.GetSignatures(snapshot, span);
        }

        /// <summary>
        /// Gets a CompletionAnalysis providing a list of possible members the user can dot through.
        /// </summary>
        public static CompletionAnalysis GetCompletions(this ITextSnapshot snapshot, ITrackingSpan span, CompletionOptions options) {
            return ProjectAnalyzer.GetCompletions(snapshot, span, options);
        }

        /// <summary>
        /// Gets a CompletionAnalysis providing a list of possible members the user can dot through.
        /// </summary>
        [Obsolete("Use GetCompletions with a CompletionOptions instance")]
        public static CompletionAnalysis GetCompletions(this ITextSnapshot snapshot, ITrackingSpan span, bool intersectMembers = true, bool hideAdvancedMembers = false) {
            return ProjectAnalyzer.GetCompletions(snapshot, span, intersectMembers, hideAdvancedMembers);
        }

        /// <summary>
        /// Gets a ImportAnalysis providing a list of imports for the selected identifer if the identifier is 
        /// currently undefined.
        /// 
        /// New in v1.1.
        /// </summary>
        public static MissingImportAnalysis GetMissingImports(this ITextSnapshot snapshot, ITrackingSpan span) {
            return ProjectAnalyzer.GetMissingImports(snapshot, span);
        }
    }
}
