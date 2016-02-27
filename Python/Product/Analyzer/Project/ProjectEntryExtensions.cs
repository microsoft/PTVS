using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools {
    static class ProjectEntryExtensions {
        private static readonly object _currentCodeKey = new object();

        public static StringBuilder GetCurrentCode(this IProjectEntry entry, int buffer = 0) {
            object dict;
            StringBuilder curCode;
            if (entry.Properties.TryGetValue(_currentCodeKey, out dict) &&
                ((SortedDictionary<int, StringBuilder>)dict).TryGetValue(buffer, out curCode)) {
                return curCode;
            }
            return null;
        }

        public static void SetCurrentCode(this IProjectEntry entry, StringBuilder value, int buffer = 0) {
            object dictTmp;
            SortedDictionary<int, StringBuilder> dict;
            if (!entry.Properties.TryGetValue(_currentCodeKey, out dictTmp)) {
                entry.Properties[_currentCodeKey] = dict = new SortedDictionary<int, StringBuilder>();
            } else {
                dict = (SortedDictionary<int, StringBuilder>)dictTmp;
            }
            dict[buffer] = value;
        }
    }
}
