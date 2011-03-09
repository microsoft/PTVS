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
using System.IO;
using System.Xml.Serialization;

namespace Microsoft.PythonTools.Profiling {
    [Serializable]
    public sealed class ProfilingTarget {
        internal static XmlSerializer Serializer = new XmlSerializer(typeof(ProfilingTarget));

        [XmlElement("ProjectTarget")]
        public ProjectTarget ProjectTarget {
            get;
            set;
        }

        [XmlElement("StandaloneTarget")]
        public StandaloneTarget StandaloneTarget {
            get;
            set;
        }

        [XmlElement("Reports")]
        public Reports Reports {
            get;
            set;
        }

        internal string GetProfilingName(out bool save) {
            string baseName = null;
            if (ProjectTarget != null) {
                if (!String.IsNullOrEmpty(ProjectTarget.FriendlyName)) {
                    baseName = ProjectTarget.FriendlyName;
                }
            } else if (StandaloneTarget != null) {
                if (!String.IsNullOrEmpty(StandaloneTarget.Script)) {
                    baseName = Path.GetFileNameWithoutExtension(StandaloneTarget.Script);
                }
            }

            if (baseName == null) {
                baseName = "Performance";
            }

            baseName = baseName + ".pyperf";

            var dte = (EnvDTE.DTE)PythonToolsPackage.GetGlobalService(typeof(EnvDTE.DTE));
            if (dte.Solution.IsOpen && !String.IsNullOrEmpty(dte.Solution.FullName)) {
                save = true;
                return Path.Combine(Path.GetDirectoryName(dte.Solution.FullName), baseName);
            }

            save = false;
            return baseName;
        }

        internal ProfilingTarget Clone() {
            var res = new ProfilingTarget();
            if (ProjectTarget != null) {
                res.ProjectTarget = ProjectTarget.Clone();
            }

            if (StandaloneTarget != null) {
                res.StandaloneTarget = StandaloneTarget.Clone();
            }

            if (Reports != null) {
                res.Reports = Reports.Clone();
            }

            return res;
        }

        internal static bool IsSame(ProfilingTarget self, ProfilingTarget other) {
            if (self == null) {
                return other == null;
            } else if (other != null) {
                return ProjectTarget.IsSame(self.ProjectTarget, other.ProjectTarget) &&
                    StandaloneTarget.IsSame(self.StandaloneTarget, other.StandaloneTarget);
            }
            return false;
        }
        
    }

    [Serializable]
    public sealed class ProjectTarget {
        [XmlElement("TargetProject")]
        public Guid TargetProject {
            get;
            set;
        }

        [XmlElement("FriendlyName")]
        public string FriendlyName {
            get;
            set;
        }

        internal ProjectTarget Clone() {
            var res = new ProjectTarget();
            res.TargetProject = TargetProject;
            res.FriendlyName = FriendlyName;
            return res;
        }

        internal static bool IsSame(ProjectTarget self, ProjectTarget other) {
            if (self == null) {
                return other == null;
            } else if (other != null) {
                return self.TargetProject == other.TargetProject;
            }
            return false;
        }
    }

    [Serializable]
    public sealed class StandaloneTarget {
        [XmlElement(ElementName = "PythonInterpreter")]
        public PythonInterpreter PythonInterpreter {
            get;
            set;
        }

        [XmlElement(ElementName = "InterpreterPath")]
        public string InterpreterPath {
            get;
            set;
        }

        [XmlElement("WorkingDirectory")]
        public string WorkingDirectory {
            get;
            set;
        }

        [XmlElement("Script")]
        public string Script {
            get;
            set;
        }

        [XmlElement("Arguments")]
        public string Arguments {
            get;
            set;
        }

        internal StandaloneTarget Clone() {
            var res = new StandaloneTarget();
            if (PythonInterpreter != null) {
                res.PythonInterpreter = PythonInterpreter.Clone();
            }

            res.InterpreterPath = InterpreterPath;
            res.WorkingDirectory = WorkingDirectory;
            res.Script = Script;
            res.Arguments = Arguments;
            return res;

        }

        internal static bool IsSame(StandaloneTarget self, StandaloneTarget other) {
            if (self == null) {
                return other == null;
            } else if (other != null) {
                return PythonInterpreter.IsSame(self.PythonInterpreter, other.PythonInterpreter) &&
                    self.InterpreterPath == other.InterpreterPath &&
                    self.WorkingDirectory == other.WorkingDirectory &&
                    self.Script == other.Script &&
                    self.Arguments == other.Arguments;
            }
            return false;
        }
    }

    public sealed class PythonInterpreter {
        [XmlElement("Id")]
        public Guid Id {
            get;
            set;
        }

        [XmlElement("Version")]
        public string Version {
            get;
            set;
        }

        internal PythonInterpreter Clone() {
            var res = new PythonInterpreter();

            res.Id = Id;
            res.Version = Version;
            return res;
        }

        internal static bool IsSame(PythonInterpreter self, PythonInterpreter other) {
            if (self == null) {
                return other == null;
            } else if (other != null) {
                return self.Id == other.Id &&
                    self.Version == other.Version;
            }
            return false;
        }
    }

    public sealed class Reports {
        public Reports() { }

        public Reports(Profiling.Report[] reports) {
            Report = reports;
        }

        [XmlElement("Report")]
        public Report[] Report {
            get;
            set;
        }


        internal Reports Clone() {
            var res = new Reports();
            if (Report != null) {
                res.Report = new Report[Report.Length];
                for (int i = 0; i < res.Report.Length; i++) {
                    res.Report[i] = Report[i].Clone();
                }
            }
            return res;
        }
    }

    public sealed class Report {
        public Report() { }
        public Report(string filename) {
            Filename = filename;
        }

        [XmlElement("Filename")]
        public string Filename {
            get;
            set;
        }

        internal Report Clone() {
            var res = new Report();
            res.Filename = Filename;
            return res;
        }
    }
}
