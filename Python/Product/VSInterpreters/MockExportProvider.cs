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
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Linq;

namespace Microsoft.PythonTools.Interpreter {
    class MockExportProvider : ExportProvider {
        readonly Dictionary<ExportDefinition, Export> _exports;

        private static ExportDefinition MakeDefinition(Type type) {
            return new ExportDefinition(
                type.FullName,
                new Dictionary<string, object> {
                        { "ExportTypeIdentity", type.FullName }
                    }
            );
        }

        public MockExportProvider() {
            _exports = new Dictionary<ExportDefinition, Export>();
        }

        public void SetExport(Type identity, Func<object> getter) {
            var definition = MakeDefinition(identity);
            _exports[definition] = new Export(definition, getter);
        }

        protected override IEnumerable<Export> GetExportsCore(
            ImportDefinition definition,
            AtomicComposition atomicComposition
        ) {

            return from kv in _exports
                   where definition.IsConstraintSatisfiedBy(kv.Key)
                   select kv.Value;
        }
    }
}
