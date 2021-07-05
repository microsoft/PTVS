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

namespace Microsoft.PythonTools.Options {
    // TODO: We should switch to a scheme which takes strings / returns object for options so they're extensible w/o reving the interface
    [Guid("BACA2500-5EA7-4075-8D02-647EAC0BC6E3")]
    public interface IPythonOptions {
        IPythonIntellisenseOptions Intellisense { get; }

        IPythonInteractiveOptions Interactive { get; }

        bool PromptBeforeRunningWithBuildErrorSetting {
            get;
            set;
        }

        bool AutoAnalyzeStandardLibrary {
            get;
            set;
        }

        Severity IndentationInconsistencySeverity {
            get;
            set;
        }

        bool TeeStandardOutput {
            get;
            set;
        }

        bool WaitOnAbnormalExit {
            get;
            set;
        }

        bool WaitOnNormalExit {
            get;
            set;
        }
    }

    [Guid("0AC0FBE6-C711-46DB-9856-0DD169E1EB9E")]
    public interface IPythonOptions2 : IPythonOptions {
        bool UseLegacyDebugger {
            get;
            set;
        }
    }

    [Guid("E02D8200-D02B-4437-B9D3-A3AE883B9C37")]
    public interface IPythonOptions3 : IPythonOptions2 {
        bool PromptForEnvCreate {
            get;
            set;
        }

        bool PromptForPackageInstallation {
            get;
            set;
        }
    }

    [Guid("77179244-BBD7-4AA2-B27B-F2CCC679953A")]
    public interface IPythonIntellisenseOptions {
        bool AddNewLineAtEndOfFullyTypedWord { get; set; }
        bool EnterCommitsCompletion { get; set; }
        bool UseMemberIntersection { get; set; }
        string CompletionCommittedBy { get; set; }
        bool AutoListIdentifiers { get; set; }
    }

    [Guid("28214322-2EEC-4750-8D87-EF76714757CE")]
    public interface IPythonInteractiveOptions {
        bool UseSmartHistory {
            get;
            set;
        }

        string CompletionMode {
            get;
            set;
        }

        string StartupScripts {
            get;
            set;
        }
    }
}
