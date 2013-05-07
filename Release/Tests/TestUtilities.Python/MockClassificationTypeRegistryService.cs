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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;

namespace TestUtilities.Mocks {
    public class MockClassificationTypeRegistryService : IClassificationTypeRegistryService {
        private static Dictionary<string, MockClassificationType> _types = new Dictionary<string,MockClassificationType>();

        public MockClassificationTypeRegistryService() {
            foreach (FieldInfo fi in typeof(PredefinedClassificationTypeNames).GetFields()) {
                string name = (string)fi.GetValue(null);
                _types[name] = new MockClassificationType(name, new IClassificationType[0]);
            }

            foreach (FieldInfo fi in typeof(PythonPredefinedClassificationTypeNames).GetFields()) {
                string name = (string)fi.GetValue(null);
                _types[name] = new MockClassificationType(name, new IClassificationType[0]);
            }
        }

        public IClassificationType CreateClassificationType(string type, IEnumerable<IClassificationType> baseTypes) {
            return _types[type] = new MockClassificationType(type, baseTypes.ToArray());
        }

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
    }
}
