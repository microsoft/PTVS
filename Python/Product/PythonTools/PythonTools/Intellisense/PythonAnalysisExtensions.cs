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
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Intellisense {
    public static class PythonAnalysisExtensions {
        /// <summary>
        /// Gets a ExpressionAnalysis for the expression at the provided span.  If the span is in
        /// part of an identifier then the expression is extended to complete the identifier.
        /// </summary>
        [Obsolete("A IServiceProvider should be provided")]
        public static ExpressionAnalysis AnalyzeExpression(this ITextSnapshot snapshot, ITrackingSpan span, bool forCompletion = true) {
#pragma warning disable 0618
            return VsProjectAnalyzer.AnalyzeExpression(PythonToolsPackage.Instance, snapshot, span, forCompletion);
#pragma warning restore 0618
        }

        /// <summary>
        /// Gets a ExpressionAnalysis for the expression at the provided span.  If the span is in
        /// part of an identifier then the expression is extended to complete the identifier.
        /// </summary>
        public static ExpressionAnalysis AnalyzeExpression(this ITextSnapshot snapshot, IServiceProvider serviceProvider, ITrackingSpan span, bool forCompletion = true) {
            return VsProjectAnalyzer.AnalyzeExpression(serviceProvider, snapshot, span, forCompletion);
        }

        /// <summary>
        /// Gets a list of signatures available for the expression at the provided location in the snapshot.
        /// </summary>
        [Obsolete("A IServiceProvider should be provided")]
        public static SignatureAnalysis GetSignatures(this ITextSnapshot snapshot, ITrackingSpan span) {
#pragma warning disable 0618
            return VsProjectAnalyzer.GetSignatures(PythonToolsPackage.Instance, snapshot, span);
#pragma warning restore 0618
        }

        /// <summary>
        /// Gets a list of signatures available for the expression at the provided location in the snapshot.
        /// </summary>
        internal static SignatureAnalysis GetSignatures(this ITextSnapshot snapshot, IServiceProvider serviceProvider, ITrackingSpan span) {
            return VsProjectAnalyzer.GetSignatures(serviceProvider, snapshot, span);
        }

        /// <summary>
        /// Gets a CompletionAnalysis providing a list of possible members the user can dot through.
        /// </summary>
        [Obsolete("A IServiceProvider should be provided")]
        public static CompletionAnalysis GetCompletions(this ITextSnapshot snapshot, ITrackingSpan span, ITrackingPoint point, CompletionOptions options) {
#pragma warning disable 0618
            return VsProjectAnalyzer.GetCompletions(PythonToolsPackage.Instance, snapshot, span, point, options);
#pragma warning restore 0618
        }

        /// <summary>
        /// Gets a CompletionAnalysis providing a list of possible members the user can dot through.
        /// </summary>
        public static CompletionAnalysis GetCompletions(this ITextSnapshot snapshot, IServiceProvider serviceProvider, ITrackingSpan span, ITrackingPoint point, CompletionOptions options) {
            return VsProjectAnalyzer.GetCompletions(serviceProvider, snapshot, span, point, options);
        }

        /// <summary>
        /// Gets a ImportAnalysis providing a list of imports for the selected identifer if the identifier is 
        /// currently undefined.
        /// 
        /// New in v1.1.
        /// </summary>
        [Obsolete("A IServiceProvider should be provided")]
        public static MissingImportAnalysis GetMissingImports(this ITextSnapshot snapshot, ITrackingSpan span) {
#pragma warning disable 0618
            return VsProjectAnalyzer.GetMissingImports(PythonToolsPackage.Instance, snapshot, span);
#pragma warning restore 0618
        }

        /// <summary>
        /// Gets a ImportAnalysis providing a list of imports for the selected identifer if the identifier is 
        /// currently undefined.
        /// 
        /// New in v1.1.
        /// </summary>        
        public static MissingImportAnalysis GetMissingImports(this ITextSnapshot snapshot, IServiceProvider serviceProvider, ITrackingSpan span) {
            return VsProjectAnalyzer.GetMissingImports(serviceProvider, snapshot, span);
        }
    }
}
