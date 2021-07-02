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

namespace Microsoft.CookiecutterTools.ViewModel
{
    class ErrorViewModel : TreeItemViewModel
    {
        private string _errorDescription;
        private string _errorDetails;

        public ErrorViewModel()
        {
        }

        public bool Selectable => false;

        public string ErrorDescription
        {
            get
            {
                return _errorDescription;
            }

            set
            {
                if (value != _errorDescription)
                {
                    _errorDescription = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(ErrorDescription)));
                }
            }
        }

        public string ErrorDetails
        {
            get
            {
                return _errorDetails;
            }

            set
            {
                if (value != _errorDetails)
                {
                    _errorDetails = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(ErrorDetails)));
                }
            }
        }
    }
}
