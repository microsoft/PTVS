using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools {
    static class ProjectEntryExtensions {
        private static readonly object _currentCodeKey = new object();

        public static StringBuilder GetCurrentCode(this IProjectEntry entry) {
            object res;
            if (entry.Properties.TryGetValue(_currentCodeKey, out res)) {
                return (StringBuilder)res;
            }
            return null;
        }

        public static void SetCurrentCode(this IProjectEntry entry, StringBuilder value) {
            entry.Properties[_currentCodeKey] = value;
        }
    }
}
