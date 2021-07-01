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

namespace Microsoft.PythonTools.Intellisense
{
    class FileCookie : IIntellisenseCookie
    {
        private readonly string _path;
        private string[] _allLines;

        public FileCookie(string path)
        {
            _path = path;
        }

        public string Path
        {
            get
            {
                return _path;
            }
        }

        #region IFileCookie Members

        public string GetLine(int lineNo)
        {
            if (_allLines == null)
            {
                try
                {
                    _allLines = File.ReadAllLines(Path);
                }
                catch (IOException)
                {
                    _allLines = new string[0];
                }
            }

            if (lineNo - 1 < _allLines.Length)
            {
                return _allLines[lineNo - 1];
            }

            return String.Empty;
        }

        #endregion
    }

    class ZipFileCookie : IIntellisenseCookie
    {
        private readonly string _zipFileName;
        private readonly string _pathInZip;
        private List<string> _allLines;

        public ZipFileCookie(string zipFileName, string pathInZip)
        {
            _zipFileName = zipFileName;
            _pathInZip = pathInZip;
        }

        public string Path
        {
            get
            {
                return System.IO.Path.Combine(_zipFileName, _pathInZip);
            }
        }

        private void Load()
        {
        }

        #region IFileCookie Members

        public string GetLine(int lineNo)
        {
            if (_allLines == null)
            {
                _allLines = new List<string>();
                try
                {
                    using (ZipArchive archive = ZipFile.Open(_zipFileName, ZipArchiveMode.Read))
                    {
                        var entry = archive.GetEntry(_pathInZip.Replace('\\', '/'));
                        if (entry != null)
                        {
                            using (var reader = new StreamReader(entry.Open()))
                            {
                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    _allLines.Add(line);
                                }
                            }
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (InvalidDataException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            if (lineNo - 1 < _allLines.Count)
            {
                return _allLines[lineNo - 1];
            }

            return String.Empty;
        }

        #endregion
    }
}
