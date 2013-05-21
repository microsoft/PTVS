using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Values;

namespace Microsoft.PythonTools.Analysis {
    static class IEnumerableExtensions {
        private static T Identity<T>(T source) {
            return source;
        }

        public static IEnumerable<T> SelectMany<T>(this IEnumerable<IEnumerable<T>> source) {
            return source.SelectMany(Identity<IEnumerable<T>>);
        }

        public static bool AnyContains<T>(this IEnumerable<IEnumerable<T>> source, T value) {
            foreach (var set in source) {
                if (set.Contains(value)) {
                    return true;
                }
            }
            return false;
        }

        public static bool AnyContains(this IEnumerable<IAnalysisSet> source, AnalysisValue value) {
            foreach (var set in source) {
                if (set.Contains(value)) {
                    return true;
                }
            }
            return false;
        }
    }
}
