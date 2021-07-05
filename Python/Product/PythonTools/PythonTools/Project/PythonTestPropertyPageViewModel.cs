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

namespace Microsoft.PythonTools.Project {
    class PythonTestPropertyPageViewModel : INotifyPropertyChanged {
        private string _selectedFramework;
        private string _unitTestRootDirectory;
        private string _unittestPattern;

        public event PropertyChangedEventHandler PropertyChanged;

        public PythonTestPropertyPageViewModel() {
            Frameworks = new[] {
                TestFrameworkType.None.ToString().ToLowerInvariant(),
                TestFrameworkType.UnitTest.ToString().ToLowerInvariant(),
                TestFrameworkType.Pytest.ToString().ToLowerInvariant(),
            };

            SelectedFramework = Frameworks[0];
            UnitTestRootDirectory = PythonConstants.DefaultUnitTestRootDirectory;
            UnitTestPattern = PythonConstants.DefaultUnitTestPattern;
        }

        public string[] Frameworks { get; }

        public string SelectedFramework {
            get => _selectedFramework;

            set {
                if (value != _selectedFramework) {
                    _selectedFramework = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedFramework)));
                }
            }
        }

        public string UnitTestRootDirectory {
            get => _unitTestRootDirectory;

            set {
                if (value != _unitTestRootDirectory) {
                    _unitTestRootDirectory = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnitTestRootDirectory)));
                }
            }
        }

        public string UnitTestPattern {
            get => _unittestPattern;

            set {
                if (value != _unittestPattern) {
                    _unittestPattern = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnitTestPattern)));
                }
            }
        }
    }
}
