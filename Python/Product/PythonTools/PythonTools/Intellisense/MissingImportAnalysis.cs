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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides information about names which are missing import statements but the
    /// name refers to an identifier in another module.
    /// 
    /// New in 1.1.
    /// </summary>
    sealed class MissingImportAnalysis {
        internal static MissingImportAnalysis Empty = new MissingImportAnalysis();
        private readonly ITrackingSpan _span;
        private readonly string _name;
        private readonly VsProjectAnalyzer _analyzer;
        private IEnumerable<ExportedMemberInfo> _imports;

        private MissingImportAnalysis() {
            _imports = Enumerable.Empty<ExportedMemberInfo>();
        }

        internal MissingImportAnalysis(string name, VsProjectAnalyzer state, ITrackingSpan span) {
            _span = span;
            _name = name;
            _analyzer = state;
        }

        /// <summary>
        /// The locations this name can be imported from.  The names are fully qualified with
        /// the module/package names and the name its self.  For example for "fob" defined in the "oar"
        ///  module the name here is oar.fob.  This list is lazily calculated (including loading of cached intellisense data) 
        ///  so that you can break from the enumeration early and save significant work.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public async Task<IEnumerable<ExportedMemberInfo>> GetAvailableImportsAsync(CancellationToken cancellationToken) {
            if (_imports != null) {
                return _imports;
            }

            var imports = await _analyzer.FindNameInAllModulesAsync(_name, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return Interlocked.CompareExchange(ref _imports, imports, null) ?? imports;
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
