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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace ReplWindowUITests {
    #region Python 32-bit tests

    [TestClass]
    public class ReplWindowPython25Tests : ReplWindowPythonSmokeTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python25,
                    IntFirstMember = "__abs__",
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython26Tests : ReplWindowPythonTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python26,
                    IntFirstMember = "conjugate",
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython27Tests : ReplWindowPythonIPythonTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python27,
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowIronPython27Tests : ReplWindowPythonTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.IronPython27,
                    SourceFileName = "string"
                };
            }
        }
        public override void AttachReplTest() { }
    }

    [TestClass]
    public class ReplWindowPython30Tests : ReplWindowPythonSmokeTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python30,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython31Tests : ReplWindowPythonSmokeTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python31,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython32Tests : ReplWindowPythonSmokeTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python32,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython33Tests : ReplWindowPythonTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python33,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython34Tests : ReplWindowPythonIPythonTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python34,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    #endregion

    #region Python 64-bit tests

    [TestClass]
    public class ReplWindowPython27x64Tests : ReplWindowPythonIPythonTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python27_x64,
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowIronPython27x64Tests : ReplWindowPythonTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.IronPython27_x64,
                    SourceFileName = "string",
                };
            }
        }

        public override void AttachReplTest() { }
    }

    [TestClass]
    public class ReplWindowPython31x64Tests : ReplWindowPythonSmokeTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python31_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython32x64Tests : ReplWindowPythonSmokeTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python32_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython33x64Tests : ReplWindowPythonSmokeTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python33_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowPython34x64Tests : ReplWindowPythonIPythonTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python34_x64,
                    RawInput = "input",
                    IPythonIntDocumentation = ReplWindowProxySettings.Python3IntDocumentation,
                    ImportError = "ImportError: No module named '{0}'"
                };
            }
        }
    }

    #endregion
}
