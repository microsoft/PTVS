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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Values {
    internal static class Utils {
        internal static T[] RemoveFirst<T>(this T[] array) {
            if (array.Length < 1) {
                return new T[0];
            }
            T[] result = new T[array.Length - 1];
            Array.Copy(array, 1, result, 0, array.Length - 1);
            return result;
        }

        internal static string StripDocumentation(string doc) {
            if (doc == null) {
                return String.Empty;
            }
            StringBuilder result = new StringBuilder(doc.Length);
            foreach (string line in doc.Split('\n')) {
                if (result.Length > 0) {
                    result.AppendLine();
                }
                result.Append(line.TrimEnd());
            }
            return result.ToString();
        }

        internal static IAnalysisSet GetReturnTypes(IPythonFunction func, PythonAnalyzer projectState) {
            return AnalysisSet.UnionAll(func.Overloads
                .Where(fn => fn.ReturnType != null)
                .Select(fn => projectState.GetAnalysisSetFromObjects(fn.ReturnType)));
        }

        internal static T First<T>(IEnumerable<T> sequence) where T : class {
            if (sequence == null) {
                return null;
            }
            var enumerator = sequence.GetEnumerator();
            if (enumerator == null) {
                return null;
            }
            try {
                if (enumerator.MoveNext()) {
                    return enumerator.Current;
                } else {
                    return null;
                }
            } finally {
                enumerator.Dispose();
            }
        }

        internal static T[] Concat<T>(T firstArg, T[] args) {
            if (args == null) {
                return new[] { firstArg };
            }
            var newArgs = new T[args.Length + 1];
            args.CopyTo(newArgs, 1);
            newArgs[0] = firstArg;
            return newArgs;
        }

        internal static T Peek<T>(this List<T> stack) {
            return stack[stack.Count - 1];
        }

        internal static void Push<T>(this List<T> stack, T value) {
            stack.Add(value);
        }

        internal static T Pop<T>(this List<T> stack) {
            int pos = stack.Count - 1;
            var result = stack[pos];
            stack.RemoveAt(pos);
            return result;
        }
    }

    internal class ReferenceComparer<T> : IEqualityComparer<T> where T : class {
        int IEqualityComparer<T>.GetHashCode(T obj) {
            return RuntimeHelpers.GetHashCode(obj);
        }

        bool IEqualityComparer<T>.Equals(T x, T y) {
            return Object.ReferenceEquals(x, y);
        }
    }
}
