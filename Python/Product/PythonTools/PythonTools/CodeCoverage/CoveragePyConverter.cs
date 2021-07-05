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

namespace Microsoft.PythonTools.CodeCoverage {
    /// <summary>
    /// Loads code coverage data from the XML format saved by coverage.py.
    /// </summary>
    class CoveragePyConverter {
        private readonly Stream _input;
        private readonly string _coverageXmlBasePath;

        public CoveragePyConverter(string baseDir, Stream input) {
            _coverageXmlBasePath = baseDir;
            _input = input;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security.Xml", "CA3053:UseXmlSecureResolver")]
        public CoverageFileInfo[] Parse() {
            XmlDocument doc = new XmlDocument { XmlResolver = null };
            var settings = new XmlReaderSettings { XmlResolver = null };
            using (var reader = XmlReader.Create(_input, settings))
                doc.Load(reader);
            Dictionary<string, HashSet<int>> data = new Dictionary<string, HashSet<int>>();

            var root = doc.DocumentElement.CreateNavigator();
            string basePath = "";
            foreach (XPathNavigator source in root.Select("/coverage/sources/source")) {
                basePath = source.Value;
            }

            foreach (XPathNavigator node in root.Select("/coverage/packages/package/classes/class/lines/line")) {
                var hits = node.GetAttribute("hits", "");
                var number = node.GetAttribute("number", "");

                int hitsNo, lineNo;
                if (Int32.TryParse(hits, out hitsNo) &&
                    hitsNo != 0 &&
                    Int32.TryParse(number, out lineNo) &&
                    node.MoveToParent() &&
                    node.MoveToParent()) {

                    var filename = GetFilename(basePath, node);

                    HashSet<int> lineHits;
                    if (!data.TryGetValue(filename, out lineHits)) {
                        data[filename] = lineHits = new HashSet<int>();
                    }

                    lineHits.Add(lineNo);
                }
            }

            return data.Select(x => new CoverageFileInfo(x.Key, x.Value))
                .ToArray();
        }

        private string GetFilename(string basePath, XPathNavigator node) {
            // Try and find the source relative to the coverage file first...
            var filename = node.GetAttribute("filename", "").Replace("/", "\\");
            string relativePath = Path.Combine(_coverageXmlBasePath, filename);
            if (File.Exists(relativePath)) {
                return relativePath;
            }

            // Then try the absolute path.
            return Path.Combine(basePath, filename);
        }
    }
}
