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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI.Python;

namespace ReplWindowUITests {
    internal static class ReplWindowSettings {
        private static Dictionary<string, ReplWindowProxySettings> _allSettings = new Dictionary<string, ReplWindowProxySettings> {
            {
                "Python27",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Python27,
                }
            },
            {
                "Python27_x64",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Python27_x64,
                }
            },
            {
                "Anaconda27",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Anaconda27,
                }
            },
            {
                "Anaconda27_x64",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Anaconda27_x64,
                }
            },
            {
                "IronPython27",
                new ReplWindowProxySettings {
                    Version = PythonPaths.IronPython27,
                    SourceFileName = "string",
                    ExitHelp = ReplWindowProxySettings.IronPython27ExitHelp,
                }
            },
            {
                "IronPython27_x64",
                new ReplWindowProxySettings {
                    Version = PythonPaths.IronPython27_x64,
                    SourceFileName = "string",
                    ExitHelp = ReplWindowProxySettings.IronPython27ExitHelp,
                }
            },
            {
                "Python35",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Python35,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = ReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "ImportError: No module named '{0}'",
                }
            },
            {
                "Python35_x64",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Python35_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = ReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "ImportError: No module named '{0}'",
                }
            },
            {
                "Python36",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Python36,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = ReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "Traceback (most recent call last):\n  File \"<stdin>\", line 1, in <module>\nModuleNotFoundError: No module named '{0}'",
                }
            },
            {
                "Python36_x64",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Python36_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = ReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "Traceback (most recent call last):\n  File \"<stdin>\", line 1, in <module>\nModuleNotFoundError: No module named '{0}'",
                }
            },
            {
                "Python37",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Python37,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = ReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "Traceback (most recent call last):\n  File \"<stdin>\", line 1, in <module>\nModuleNotFoundError: No module named '{0}'",
                }
            },
            {
                "Python37_x64",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Python37_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = ReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "Traceback (most recent call last):\n  File \"<stdin>\", line 1, in <module>\nModuleNotFoundError: No module named '{0}'",
                }
            },
            {
                "Anaconda36",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Anaconda36,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = ReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "Traceback (most recent call last):\n  File \"<stdin>\", line 1, in <module>\nModuleNotFoundError: No module named '{0}'",
                }
            },
            {
                "Anaconda36_x64",
                new ReplWindowProxySettings {
                    Version = PythonPaths.Anaconda36_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = ReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "Traceback (most recent call last):\n  File \"<stdin>\", line 1, in <module>\nModuleNotFoundError: No module named '{0}'",
                }
            },
        };

        public static ReplWindowProxySettings FindSettingsForInterpreter(string pythonVersion) {
            // Examples of values for pythonVersion:
            // "Python27"
            // "Python27|Python27_x64"
            // "Anaconda27|Anaconda27_x64|Python27|Python27_x64"
            var settings = pythonVersion.Split('|')
                .Select(v => { _allSettings.TryGetValue(v, out ReplWindowProxySettings cur); return cur; })
                .Where(s => s != null && s.Version != null)
                .FirstOrDefault();

            if (settings == null) {
                Assert.Inconclusive($"Interpreter '{pythonVersion}' not installed.");
            }

            return settings;
        }
    }
}
