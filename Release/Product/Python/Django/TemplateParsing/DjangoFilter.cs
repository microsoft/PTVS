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

namespace Microsoft.PythonTools.Django.TemplateParsing {
    /// <summary>
    /// Captures information about a Django variable expression's filter and its associate argument if one was present.
    /// </summary>
    class DjangoFilter {
        public readonly int FilterStart, ArgStart;
        public readonly string Filter;
        public readonly DjangoVariableValue Arg;

        public DjangoFilter(string filterName, int filterStart, DjangoVariableValue arg = null, int groupStart = 0) {
            Filter = filterName;
            FilterStart = filterStart;
            ArgStart = groupStart;
            Arg = arg;
        }

        public static DjangoFilter Variable(string filterName, int filterStart, string variable, int groupStart) {
            return new DjangoFilter(filterName, filterStart, new DjangoVariableValue(variable, DjangoVariableKind.Variable), groupStart);
        }

        public static DjangoFilter Constant(string filterName, int filterStart, string variable, int groupStart) {
            return new DjangoFilter(filterName, filterStart, new DjangoVariableValue(variable, DjangoVariableKind.Constant), groupStart);
        }

        public static DjangoFilter Number(string filterName, int filterStart, string variable, int groupStart) {
            return new DjangoFilter(filterName, filterStart, new DjangoVariableValue(variable, DjangoVariableKind.Number), groupStart);
        }
    }

}
