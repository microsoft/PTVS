using System;
using System.Collections.Generic;

namespace Microsoft.PythonTools.TestAdapter.Config {
    public class PythonProjectSettings : IEquatable<PythonProjectSettings> {
        public readonly string ProjectName, ProjectHome, WorkingDirectory, InterpreterPath, PathEnv, PytestPath, PytestArgs;
        public readonly bool EnableNativeCodeDebugging;
        public readonly bool PytestEnabled = false;
        public readonly List<string> SearchPath;
        public readonly Dictionary<string, string> Environment;
        public readonly List<string> Sources;
        public readonly bool IsWorkspace;
        public readonly bool UseLegacyDebugger;

        public PythonProjectSettings(string projectName,
            string projectHome,
            string workingDir,
            string interpreter,
            string pathEnv,
            bool nativeDebugging,
            string pytestPath,
            string pytestArgs,
            bool pytestEnabled,
            bool isWorkspace,
            bool useLegacyDebugger) {
            ProjectName = projectName;
            ProjectHome = projectHome;
            WorkingDirectory = workingDir;
            InterpreterPath = interpreter;
            PathEnv = pathEnv;
            EnableNativeCodeDebugging = nativeDebugging;
            PytestEnabled = pytestEnabled;
            PytestPath = pytestPath;
            PytestArgs = pytestArgs;
            SearchPath = new List<string>();
            Environment = new Dictionary<string, string>();
            Sources = new List<string>();
            IsWorkspace = isWorkspace;
            UseLegacyDebugger = useLegacyDebugger;
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
                PytestEnabled == other.PytestEnabled &&
                UseLegacyDebugger == other.UseLegacyDebugger) {
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
