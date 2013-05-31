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

namespace Microsoft.PythonTools.Django.Project {
    class TemplateVariables {
        private readonly Dictionary<string, Dictionary<IPythonProjectEntry, ValuesAndVersion>> _values = new Dictionary<string, Dictionary<IPythonProjectEntry, ValuesAndVersion>>();

        public void UpdateVariable(string name, AnalysisUnit unit, IEnumerable<AnalysisValue> values) {
            Dictionary<IPythonProjectEntry, ValuesAndVersion> entryMappedValues;
            if (!_values.TryGetValue(name, out entryMappedValues)) {
                _values[name] = entryMappedValues = new Dictionary<IPythonProjectEntry, ValuesAndVersion>();
            }

            foreach (var value in values) {
                var module = value.DeclaringModule ?? unit.Project;
                ValuesAndVersion valsAndVersion;
                if (!entryMappedValues.TryGetValue(module, out valsAndVersion) || valsAndVersion.DeclaringVersion != module.AnalysisVersion) {
                    entryMappedValues[module] = valsAndVersion = new ValuesAndVersion(module.AnalysisVersion);
                }

                valsAndVersion.Values.Add(value);
            }
        }

        struct ValuesAndVersion {
            public readonly int DeclaringVersion;
            public readonly HashSet<AnalysisValue> Values;

            public ValuesAndVersion(int declaringVersion) {
                DeclaringVersion = declaringVersion;
                Values = new HashSet<AnalysisValue>();
            }
        }

        internal Dictionary<string, HashSet<AnalysisValue>> GetAllValues() {
            var res = new Dictionary<string, HashSet<AnalysisValue>>();

            foreach (var nameAndValues in _values) {
                HashSet<AnalysisValue> curValues = new HashSet<AnalysisValue>();
                res[nameAndValues.Key] = curValues;

                foreach (var projectAndValues in nameAndValues.Value) {
                    foreach (var analysisValue in projectAndValues.Value.Values) {
                        if (analysisValue.DeclaringModule == null ||
                            analysisValue.DeclaringModule.AnalysisVersion == projectAndValues.Value.DeclaringVersion) {
                            curValues.Add(analysisValue);
                        }
                    }
                }
            }
            return res;
        }
    }

}
