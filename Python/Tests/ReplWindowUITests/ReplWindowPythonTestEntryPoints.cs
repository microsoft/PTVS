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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace ReplWindowUITests {
    #region Python 32-bit tests

    [TestClass]
    public class ReplWindowPython25Tests : ReplWindowPythonSmokeTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python25,
                    IntFirstMember = "__abs__",
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython26Tests : ReplWindowPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python26,
                    IntFirstMember = "conjugate",
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython27Tests : ReplWindowPythonIPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python27,
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowIronPython27Tests : ReplWindowPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.IronPython27,
                    SourceFileName = "string",
                    ExitHelp = PythonReplWindowProxySettings.IronPython27ExitHelp
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython30Tests : ReplWindowPythonSmokeTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python30,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python3ExitHelp
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython31Tests : ReplWindowPythonSmokeTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python31,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python3ExitHelp
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython32Tests : ReplWindowPythonSmokeTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python32,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python3ExitHelp
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython33Tests : ReplWindowPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python33,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python3ExitHelp,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython34Tests : ReplWindowPythonIPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python34,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python34ExitHelp,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython35Tests : ReplWindowPythonIPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python35,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython36Tests : ReplWindowPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python36,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    #endregion

    #region Python 64-bit tests

    [TestClass]
    public class ReplWindowPython27x64Tests : ReplWindowPythonIPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python27_x64,
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowIronPython27x64Tests : ReplWindowPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.IronPython27_x64,
                    SourceFileName = "string",
                    ExitHelp = PythonReplWindowProxySettings.IronPython27ExitHelp
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython31x64Tests : ReplWindowPythonSmokeTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python31_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python3ExitHelp
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython32x64Tests : ReplWindowPythonSmokeTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python32_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python3ExitHelp
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython33x64Tests : ReplWindowPythonSmokeTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python33_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python3ExitHelp,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython34x64Tests : ReplWindowPythonIPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python34_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python34ExitHelp,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython35x64Tests : ReplWindowPythonIPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python35_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython36x64Tests : ReplWindowPythonTests {
        internal override PythonReplWindowProxySettings Settings {
            get {
                return new PythonReplWindowProxySettings {
                    Version = PythonPaths.Python36_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = PythonReplWindowProxySettings.Python3IntDocumentation,
                    ExitHelp = PythonReplWindowProxySettings.Python35ExitHelp,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    #endregion
}
