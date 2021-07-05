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

namespace Microsoft.CookiecutterTools.Interpreters {
    /// <summary>
    /// Provides constants used to identify interpreters that are detected from
    /// the CPython registry settings.
    /// 
    /// This class used by Microsoft.PythonTools.dll to register the
    /// interpreters.
    /// </summary>
    public static class CPythonInterpreterFactoryConstants {
        public const string ConsoleExecutable = "python.exe";
        public const string WindowsExecutable = "pythonw.exe";
        public const string LibrarySubPath = "lib";
        public const string PathEnvironmentVariableName = "PYTHONPATH";

        private static readonly Regex IdParser = new Regex(
            "^(?<provider>.+?)\\|(?<company>.+?)\\|(?<tag>.+?)$",
            RegexOptions.None,
            TimeSpan.FromSeconds(1)
        );

        public static string GetInterpreterId(string company, string tag) {
            return String.Join(
                "|",
                CPythonInterpreterFactoryProvider.FactoryProviderName,
                company,
                tag
            );
        }

        public static bool TryParseInterpreterId(string id, out string company, out string tag) {
            tag = company = null;
            try {
                var m = IdParser.Match(id);
                if (m.Success && m.Groups["provider"].Value == CPythonInterpreterFactoryProvider.FactoryProviderName) {
                    tag = m.Groups["tag"].Value;
                    company = m.Groups["company"].Value;
                    return true;
                }
                return false;
            } catch (RegexMatchTimeoutException) {
                return false;
            }
        }
    }
}
