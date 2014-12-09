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
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Options;

namespace TestUtilities.Python {
    public sealed class MockPythonToolsOptionsService : IPythonToolsOptionsService {
        private Dictionary<string, Dictionary<string, string>> _options = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public void SaveString(string name, string value, string cat) {
            Dictionary<string, string> category;
            if (!_options.TryGetValue(cat, out category)) {
                _options[cat] = category = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            category[name] = value;
        }

        public string LoadString(string name, string cat) {
            Dictionary<string, string> category;
            string res;
            if (!_options.TryGetValue(cat, out category) ||
                !category.TryGetValue(name, out res)) {
                return null;
            }

            return res;
        }
    }
}
