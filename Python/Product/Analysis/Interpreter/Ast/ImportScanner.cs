using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Analysis.Interpreter.Ast {
    class ImportScanner {
        private readonly string _cacheFile;

        private readonly object _lock = new object();
        private readonly Dictionary<string, PathEntry> _data;

        public ImportScanner(string cacheFile) {
            _cacheFile = cacheFile;
            _data = new Dictionary<string, PathEntry>(PathEqualityComparer.Instance);
        }

        public void SetSearchPaths(IEnumerable<string> paths) {
            lock (_lock) {
                var toRemove = new HashSet<string>(_data.Keys, _data.Comparer);

                foreach (var p in paths) {
                    if (!toRemove.Remove(p)) {
                        _data[p] = new PathEntry();
                    }
                }

                foreach (var p in toRemove) {
                    _data.Remove(p);
                }
            }
        }

        public IReadOnlyList<string> GetImportsForName(string name) {
            var result = new List<string>();
            foreach (var entry in _data.Values) {
                result.AddRange(entry.GetImportsForName(name));
            }
        }

        class PathEntry {
            private readonly object _lock = new object();
            private readonly 

            private bool _upToDate = false;

            public IEnumerable<string> GetImportsForName(string name) {
                return Enumerable.Empty<string>();
            }
        }
    }
}
