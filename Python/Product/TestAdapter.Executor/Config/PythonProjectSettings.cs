using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Microsoft.PythonTools.TestAdapter.Config {
    public class PythonProjectSettings : IEquatable<PythonProjectSettings> {
        public readonly string ProjectName, ProjectHome, WorkingDirectory, InterpreterPath, PathEnv, UnitTestPattern, UnitTestRootDir;
        public readonly bool EnableNativeCodeDebugging;
        public readonly List<string> SearchPath;
        public readonly Dictionary<string, string> Environment;
        public readonly List<string> Sources;
        public readonly bool IsWorkspace;
        public readonly bool UseLegacyDebugger;
        public readonly TestFrameworkType TestFramwork;
        public readonly int DiscoveryWaitTimeInSeconds;
        public PythonProjectSettings(string projectName,
            string projectHome,
            string workingDir,
            string interpreter,
            string pathEnv,
            bool nativeDebugging,
            bool isWorkspace,
            bool useLegacyDebugger,
            string testFramework,
            string unitTestPattern,
            string unitTestRootDir,
            string discoveryWaitTimeInSeconds) {
            ProjectName = projectName;
            ProjectHome = projectHome;
            WorkingDirectory = workingDir;
            InterpreterPath = interpreter;
            PathEnv = pathEnv;
            EnableNativeCodeDebugging = nativeDebugging;
            SearchPath = new List<string>();
            Environment = new Dictionary<string, string>();
            Sources = new List<string>();
            IsWorkspace = isWorkspace;
            UseLegacyDebugger = useLegacyDebugger;
            TestFramwork = TestFrameworkType.None;
            Enum.TryParse<TestFrameworkType>(testFramework, ignoreCase: true, out TestFramwork);
            UnitTestPattern = string.IsNullOrEmpty(unitTestPattern) ? PythonConstants.DefaultUnitTestPattern : unitTestPattern;
            UnitTestRootDir = string.IsNullOrEmpty(unitTestRootDir) ? PythonConstants.DefaultUnitTestRootDirectory : unitTestRootDir;
            DiscoveryWaitTimeInSeconds = Int32.TryParse(discoveryWaitTimeInSeconds, out int parsedWaitTime) ? parsedWaitTime : PythonConstants.DiscoveryTimeoutInSeconds;
        }

        public override bool Equals(object obj) {
            return Equals(obj as PythonProjectSettings);
        }

        public override int GetHashCode() {
            return ProjectHome.GetHashCode() ^
                WorkingDirectory.GetHashCode() ^
                InterpreterPath.GetHashCode();
        }

        public bool Equals(PythonProjectSettings other) {
            if (other == null) {
                return false;
            }

            if (ProjectHome == other.ProjectHome &&
                WorkingDirectory == other.WorkingDirectory &&
                InterpreterPath == other.InterpreterPath &&
                PathEnv == other.PathEnv &&
                EnableNativeCodeDebugging == other.EnableNativeCodeDebugging &&
                IsWorkspace == other.IsWorkspace &&
                UseLegacyDebugger == other.UseLegacyDebugger &&
                TestFramwork == other.TestFramwork &&
                UnitTestPattern == other.UnitTestPattern &&
                UnitTestRootDir == other.UnitTestRootDir) {
                if (SearchPath.Count == other.SearchPath.Count &&
                    Environment.Count == other.Environment.Count) {
                    for (int i = 0; i < SearchPath.Count; i++) {
                        if (SearchPath[i] != other.SearchPath[i]) {
                            return false;
                        }
                    }

                    for (int i = 0; i < Sources.Count; i++) {
                        if (Sources[i] != other.Sources[i]) {
                            return false;
                        }
                    }

                    foreach (var keyValue in Environment) {
                        string value;
                        if (!other.Environment.TryGetValue(keyValue.Key, out value) ||
                            value != keyValue.Value) {
                            return false;
                        }
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
