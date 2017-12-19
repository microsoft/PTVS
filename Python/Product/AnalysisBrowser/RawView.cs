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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Interpreter.LegacyDB;

namespace Microsoft.PythonTools.Analysis.Browser {
    class RawView : IAnalysisItemView {
        readonly string _name;
        readonly string _displayType;
        readonly string _filename;
        readonly object _item;

        public static RawView FromFile(string filename) {
            object contents = null;
            try {
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    try {
                        contents = Unpickle.Load(stream);
                    } catch (ArgumentException) {
                    } catch (InvalidOperationException) {
                    }
                }
            } catch (Exception ex) {
                contents = ex;
            }
            return new RawView(Path.GetFileName(filename), filename, contents);
        }

        public RawView(string name, string filename, object item) {
            _name = name ?? "(null)";
            _filename = filename;
            _item = item ?? "(null)";
            _displayType = "Raw " + GetTypeName(item);
        }

        private static string GetTypeName(object item) {
            if (item == null) {
                return "null";
            } else if (item is string) {
                return "string";
            } else if (item is Dictionary<string, object>) {
                return "dict(str -> object)";
            } else if (item is List<Dictionary<string, object>>) {
                return "list(dict)";
            } else if (item is List<object>) {
                return "list(object)";
            } else if (item is Dictionary<string, object>[]) {
                return "tuple(dict(str -> object))";
            } else if (item is object[]) {
                return "tuple(object)";
            } else {
                return item.GetType().Name;
            }
        }
        
        public string Name {
            get { return _name; }
        }

        public string Value {
            get {
                var s = FullValue ?? string.Empty;
                var ts = s.TrimStart();
                int firstNewLine = ts.IndexOfAny(new[] { '\r', '\n' });
                if (firstNewLine > 0) {
                    return ts.Remove(firstNewLine) + "...";
                }
                return s;
            }
        }

        public string FullValue {
            get {
                string s;
                int? i;
                bool? b;
                if ((s = _item as string) != null) {
                    return s;
                } else if ((i = _item as int?) != null) {
                    return string.Format("{0} ({0:X})", i.Value);
                } else if ((b = _item as bool?) != null) {
                    return b.Value.ToString();
                } else {
                    return GetTypeName(_item);
                }
            }
        }

        public string SortKey {
            get { return "0"; }
        }

        public string DisplayType {
            get { return _displayType; }
        }

        public string SourceLocation {
            get { return _filename; }
        }

        public IEnumerable<IAnalysisItemView> Children {
            get {
                Dictionary<string, object> d;
                IEnumerable<object> e;
                if ((d = _item as Dictionary<string, object>) != null) {
                    foreach (var kv in d) {
                        yield return new RawView(kv.Key, _filename, kv.Value);
                    }
                } else if ((e = _item as IEnumerable<object>) != null) {
                    int count = 0;
                    foreach (var o in e) {
                        yield return new RawView(count.ToString(), _filename, o);
                        count += 1;
                    }
                }
            }
        }

        public IEnumerable<IAnalysisItemView> SortedChildren {
            get {
                return Children.OrderBy(c => c.SortKey).ThenBy(c => c.Name);
            }
        }

        public IEnumerable<KeyValuePair<string, object>> Properties {
            get {
                yield return new KeyValuePair<string, object>("Value", FullValue);
            }
        }

        public void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine("{0}{1}={2}", currentIndent, Name, Value);
            exportChildren = SortedChildren;
        }

        public void ExportToDiffable(
            TextWriter writer,
            string currentIndent,
            string indent,
            Stack<IAnalysisItemView> exportStack,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            exportChildren = null;
        }
    }
}
