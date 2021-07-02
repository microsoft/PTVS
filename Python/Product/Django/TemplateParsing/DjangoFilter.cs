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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Django.TemplateParsing
{
    /// <summary>
    /// Captures information about a Django variable expression's filter and its associate argument if one was present.
    /// </summary>
    class DjangoFilter
    {
        public readonly int FilterStart, ArgStart;
        public readonly string Filter;
        public readonly DjangoVariableValue Arg;

        public DjangoFilter(string filterName, int filterStart, DjangoVariableValue arg = null, int groupStart = 0)
        {
            Filter = filterName;
            FilterStart = filterStart;
            ArgStart = groupStart;
            Arg = arg;
        }

        public static DjangoFilter Variable(string filterName, int filterStart, string variable, int groupStart)
        {
            return new DjangoFilter(filterName, filterStart, new DjangoVariableValue(variable, DjangoVariableKind.Variable), groupStart);
        }

        public static DjangoFilter Constant(string filterName, int filterStart, string variable, int groupStart)
        {
            return new DjangoFilter(filterName, filterStart, new DjangoVariableValue(variable, DjangoVariableKind.Constant), groupStart);
        }

        public static DjangoFilter Number(string filterName, int filterStart, string variable, int groupStart)
        {
            return new DjangoFilter(filterName, filterStart, new DjangoVariableValue(variable, DjangoVariableKind.Number), groupStart);
        }

        internal IEnumerable<BlockClassification> GetSpans(int expressionStart = 0)
        {
            yield return new BlockClassification(
                new Span(FilterStart + expressionStart, Filter.Length),
                Classification.Identifier
            );

            if (Arg != null)
            {
                foreach (var span in Arg.GetSpans(ArgStart + expressionStart))
                {
                    yield return span;
                }
            }
        }
    }

}
