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

using System.Collections.Generic;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides information about names which are missing import statements but the
    /// name refers to an identifier in another module.
    /// 
    /// New in 1.1.
    /// </summary>
    public sealed class MissingImportAnalysis {
        internal static MissingImportAnalysis Empty = new MissingImportAnalysis(new ExportedMemberInfo[0], null);
        private readonly ITrackingSpan _span;
        private readonly IEnumerable<ExportedMemberInfo> _names;

        internal MissingImportAnalysis(IEnumerable<ExportedMemberInfo> names, ITrackingSpan span) {
            _span = span;
            _names = names;
        }

        /// <summary>
        /// The locations this name can be imported from.  The names are fully qualified with
        /// the module/package names and the name its self.  For example for "foo" defined in the "bar"
        ///  module the name here is bar.foo.  This list is lazily calculated (including loading of cached intellisense data) 
        ///  so that you can break from the enumeration early and save significant work.
        /// </summary>
        public IEnumerable<ExportedMemberInfo> AvailableImports {
            get {
                return _names;
            }
        }

        /// <summary>
        /// The span which covers the identifier used to trigger this missing import analysis.
        /// </summary>
        public ITrackingSpan ApplicableToSpan {
            get {
                return _span;
            }
        }
    }
}
