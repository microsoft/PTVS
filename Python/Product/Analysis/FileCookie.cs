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

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Microsoft.PythonTools.Analysis {
    public class FileCookie : IAnalysisCookie {
        private readonly string _path;
        private string[] _allLines;

        public FileCookie(string path) {
            _path = path;
        }

        public string Path {
            get {
                return _path;
            }
        }

        #region IFileCookie Members

        public string GetLine(int lineNo) {
            if (_allLines == null) {
                try {
                    _allLines = File.ReadAllLines(Path);
                } catch (IOException) {
                    _allLines = new string[0];
                }
            }

            if (lineNo - 1 < _allLines.Length) {
                return _allLines[lineNo - 1];
            }

            return String.Empty;
        }

        #endregion
    }

    public class ZipFileCookie : IAnalysisCookie {
        private readonly string _zipFileName;
        private readonly string _pathInZip;
        private List<string> _allLines;

        public ZipFileCookie(string zipFileName, string pathInZip) {
            _zipFileName = zipFileName;
            _pathInZip = pathInZip;
        }

        public string Path {
            get {
                return System.IO.Path.Combine(_zipFileName, _pathInZip);
            }
        }

        private void Load() {
        }

        #region IFileCookie Members

        public string GetLine(int lineNo) {
            if (_allLines == null) {
                _allLines = new List<string>();
                try {
                    using (ZipArchive archive = ZipFile.Open(_zipFileName, ZipArchiveMode.Read)) {
                        var entry = archive.GetEntry(_pathInZip.Replace('\\', '/'));
                        if (entry != null) {
                            using (Stream stream = entry.Open())
                            using (var reader = new StreamReader(stream)) {
                                string line;
                                while ((line = reader.ReadLine()) != null) {
                                    _allLines.Add(line);
                                }
                            }
                        }
                    }
                } catch (IOException) {
                } catch (InvalidDataException) {
                } catch (UnauthorizedAccessException) {
                }
            }

            if (lineNo - 1 < _allLines.Count) {
                return _allLines[lineNo - 1];
            }

            return String.Empty;
        }

        #endregion
    }
}
