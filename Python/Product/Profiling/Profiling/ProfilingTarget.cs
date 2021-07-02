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

namespace Microsoft.PythonTools.Profiling
{
    // XmlSerializer requires these types to be public
    [Serializable]
    public sealed class ProfilingTarget
    {
        internal static XmlSerializer Serializer = new XmlSerializer(typeof(ProfilingTarget));

        [XmlElement("ProjectTarget")]
        public ProjectTarget ProjectTarget
        {
            get;
            set;
        }

        [XmlElement("StandaloneTarget")]
        public StandaloneTarget StandaloneTarget
        {
            get;
            set;
        }

        [XmlElement("Reports")]
        public Reports Reports
        {
            get;
            set;
        }

        [XmlElement()]
        public bool UseVTune
        {
            get;
            set;
        }

        internal string GetProfilingName(IServiceProvider serviceProvider, out bool save)
        {
            string baseName = null;
            if (ProjectTarget != null)
            {
                if (!String.IsNullOrEmpty(ProjectTarget.FriendlyName))
                {
                    baseName = ProjectTarget.FriendlyName;
                }
            }
            else if (StandaloneTarget != null)
            {
                if (!String.IsNullOrEmpty(StandaloneTarget.Script))
                {
                    baseName = Path.GetFileNameWithoutExtension(StandaloneTarget.Script);
                }
            }

            if (baseName == null)
            {
                baseName = Strings.PerformanceBaseFileName;
            }

            baseName = baseName + ".pyperf";

            var dte = (EnvDTE.DTE)serviceProvider.GetService(typeof(EnvDTE.DTE));
            if (dte.Solution.IsOpen && !String.IsNullOrEmpty(dte.Solution.FullName))
            {
                save = true;
                return Path.Combine(Path.GetDirectoryName(dte.Solution.FullName), baseName);
            }

            save = false;
            return baseName;
        }

        internal ProfilingTarget Clone()
        {
            var res = new ProfilingTarget();
            if (ProjectTarget != null)
            {
                res.ProjectTarget = ProjectTarget.Clone();
            }

            if (StandaloneTarget != null)
            {
                res.StandaloneTarget = StandaloneTarget.Clone();
            }

            if (Reports != null)
            {
                res.Reports = Reports.Clone();
            }

            res.UseVTune = UseVTune;

            return res;
        }

        internal static bool IsSame(ProfilingTarget self, ProfilingTarget other)
        {
            if (self == null)
            {
                return other == null;
            }
            else if (other != null)
            {
                return ProjectTarget.IsSame(self.ProjectTarget, other.ProjectTarget) &&
                    StandaloneTarget.IsSame(self.StandaloneTarget, other.StandaloneTarget);
            }
            return false;
        }

    }

    [Serializable]
    public sealed class ProjectTarget
    {
        [XmlElement("TargetProject")]
        public Guid TargetProject
        {
            get;
            set;
        }

        [XmlElement("FriendlyName")]
        public string FriendlyName
        {
            get;
            set;
        }

        internal ProjectTarget Clone()
        {
            var res = new ProjectTarget();
            res.TargetProject = TargetProject;
            res.FriendlyName = FriendlyName;
            return res;
        }

        internal static bool IsSame(ProjectTarget self, ProjectTarget other)
        {
            if (self == null)
            {
                return other == null;
            }
            else if (other != null)
            {
                return self.TargetProject == other.TargetProject;
            }
            return false;
        }
    }

    [Serializable]
    public sealed class StandaloneTarget
    {
        [XmlElement(ElementName = "PythonInterpreter")]
        public PythonInterpreter PythonInterpreter
        {
            get;
            set;
        }

        [XmlElement(ElementName = "InterpreterPath")]
        public string InterpreterPath
        {
            get;
            set;
        }

        [XmlElement("WorkingDirectory")]
        public string WorkingDirectory
        {
            get;
            set;
        }

        [XmlElement("Script")]
        public string Script
        {
            get;
            set;
        }

        [XmlElement("Arguments")]
        public string Arguments
        {
            get;
            set;
        }

        internal StandaloneTarget Clone()
        {
            var res = new StandaloneTarget();
            if (PythonInterpreter != null)
            {
                res.PythonInterpreter = PythonInterpreter.Clone();
            }

            res.InterpreterPath = InterpreterPath;
            res.WorkingDirectory = WorkingDirectory;
            res.Script = Script;
            res.Arguments = Arguments;
            return res;

        }

        internal static bool IsSame(StandaloneTarget self, StandaloneTarget other)
        {
            if (self == null)
            {
                return other == null;
            }
            else if (other != null)
            {
                return PythonInterpreter.IsSame(self.PythonInterpreter, other.PythonInterpreter) &&
                    self.InterpreterPath == other.InterpreterPath &&
                    self.WorkingDirectory == other.WorkingDirectory &&
                    self.Script == other.Script &&
                    self.Arguments == other.Arguments;
            }
            return false;
        }
    }

    public sealed class PythonInterpreter
    {
        [XmlElement("Id")]
        public string Id
        {
            get;
            set;
        }

        internal PythonInterpreter Clone()
        {
            var res = new PythonInterpreter();

            res.Id = Id;
            return res;
        }

        internal static bool IsSame(PythonInterpreter self, PythonInterpreter other)
        {
            if (self == null)
            {
                return other == null;
            }
            else if (other != null)
            {
                return self.Id == other.Id;
            }
            return false;
        }
    }

    public sealed class Reports
    {
        public Reports() { }

        public Reports(Profiling.Report[] reports)
        {
            Report = reports;
        }

        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")]
        [XmlElement("Report")]
        public Report[] Report
        {
            get
            {
                return AllReports.Values.ToArray();
            }
            set
            {
                AllReports = new SortedDictionary<uint, Report>();
                for (uint i = 0; i < value.Length; i++)
                {
                    AllReports[i + SessionNode.StartingReportId] = value[i];
                }
            }
        }

        internal SortedDictionary<uint, Report> AllReports
        {
            get;
            set;
        }

        internal Reports Clone()
        {
            var res = new Reports();
            if (Report != null)
            {
                res.Report = new Report[Report.Length];
                for (int i = 0; i < res.Report.Length; i++)
                {
                    res.Report[i] = Report[i].Clone();
                }
            }
            return res;
        }
    }

    public sealed class Report
    {
        public Report() { }
        public Report(string filename)
        {
            Filename = filename;
        }

        [XmlElement("Filename")]
        public string Filename
        {
            get;
            set;
        }

        internal Report Clone()
        {
            var res = new Report();
            res.Filename = Filename;
            return res;
        }
    }
}
