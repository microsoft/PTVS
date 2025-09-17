// Visual Studio Shared Project
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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;

namespace TestUtilities.Mocks {
    [Export(typeof(IClassificationTypeRegistryService))]
    public class MockClassificationTypeRegistryService : IClassificationTypeRegistryService {
        static Dictionary<string, MockClassificationType> _types = new Dictionary<string, MockClassificationType>();

        public MockClassificationTypeRegistryService() {
            foreach (FieldInfo fi in typeof(PredefinedClassificationTypeNames).GetFields()) {
                string name = (string)fi.GetValue(null);
                _types[name] = new MockClassificationType(name, new IClassificationType[0]);
            }
        }

        [ImportingConstructor]
        public MockClassificationTypeRegistryService([ImportMany]IEnumerable<Lazy<ClassificationTypeDefinition, IClassificationTypeDefinitionMetadata>> classTypeDefs)
            : this() {
            foreach (var def in classTypeDefs) {
                string name = def.Metadata.Name;
                var type = GetClasificationType(name);
                foreach (var baseType in def.Metadata.BaseDefinition ?? Enumerable.Empty<string>()) {
                    type.AddBaseType(GetClasificationType(baseType));
                }
            }
        }

        private static MockClassificationType GetClasificationType(string name) {
            MockClassificationType type;
            if (!_types.TryGetValue(name, out type)) {
                _types[name] = type = new MockClassificationType(name, new IClassificationType[0]);
            }
            return type;
        }

        public IClassificationType CreateClassificationType(string type, IEnumerable<IClassificationType> baseTypes) {
            return _types[type] = new MockClassificationType(type, baseTypes.ToArray());
        }

#if DEV18_OR_LATER
        public ILayeredClassificationType CreateClassificationType(ClassificationLayer layer, string type, IEnumerable<IClassificationType> baseTypes)
        {
            return null;
        }
#endif

        public IClassificationType CreateTransientClassificationType(params IClassificationType[] baseTypes) {
            return new MockClassificationType(String.Empty, baseTypes);
        }

        public IClassificationType CreateTransientClassificationType(IEnumerable<IClassificationType> baseTypes) {
            return new MockClassificationType(String.Empty, baseTypes.ToArray());
        }

        public IClassificationType GetClassificationType(string type) {
            MockClassificationType result;
            return _types.TryGetValue(type, out result) ? result : null;
        }

#if DEV18_OR_LATER
        public ILayeredClassificationType GetClassificationType(ClassificationLayer layer, string type)
        {
            return null;
        }
#endif
    }
}
