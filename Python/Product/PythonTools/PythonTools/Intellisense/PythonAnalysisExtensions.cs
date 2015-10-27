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
