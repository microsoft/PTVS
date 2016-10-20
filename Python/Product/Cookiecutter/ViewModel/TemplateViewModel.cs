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

using System.ComponentModel;
using Microsoft.CookiecutterTools.Model;
using Microsoft.VisualStudio.Imaging.Interop;

namespace Microsoft.CookiecutterTools.ViewModel {
    class TemplateViewModel : INotifyPropertyChanged {
        private string _displayName;
        private string _remoteUrl;
        private string _clonedPath;
        private string _description;
        private bool _isSearchTerm;
        private bool _isUpdateAvailable;

        public event PropertyChangedEventHandler PropertyChanged;

        public TemplateViewModel() {
        }

        public bool Selectable => true;

        public string GitHubHomeUrl => RemoteUrl;

        public string GitHubIssuesUrl => RemoteUrl != null ? RemoteUrl + "/issues" : null;

        public string GitHubWikiUrl => RemoteUrl != null ? RemoteUrl + "/wiki" : null;

        /// <summary>
        /// Repository name.
        /// </summary>
        public string RepositoryName {
            get {
                if (string.IsNullOrEmpty(RemoteUrl)) {
                    return string.Empty;
                }

                string owner;
                string name;
                ParseUtils.ParseGitHubRepoOwnerAndName(RemoteUrl, out owner, out name);
                return name;
            }
        }

        /// <summary>
        /// Repository owner.
        /// </summary>
        public string RepositoryOwner {
            get {
                if (string.IsNullOrEmpty(RemoteUrl)) {
                    return string.Empty;
                }

                string owner;
                string name;
                ParseUtils.ParseGitHubRepoOwnerAndName(RemoteUrl, out owner, out name);
                return owner;
            }
        }

        /// <summary>
        /// Repository full name, ie. 'owner/name'.
        /// </summary>
        public string RepositoryFullName {
            get {
                if (string.IsNullOrEmpty(RemoteUrl)) {
                    return string.Empty;
                }

                string owner;
                string name;
                ParseUtils.ParseGitHubRepoOwnerAndName(RemoteUrl, out owner, out name);
                if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(name)) {
                    return owner + '/' + name;
                }
                return string.Empty;
            }
        }

        public string DisplayName {
            get {
                return _displayName;
            }

            set {
                if (value != _displayName) {
                    _displayName = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                }
            }
        }

        public string RemoteUrl {
            get {
                return _remoteUrl;
            }

            set {
                if (value != _remoteUrl) {
                    _remoteUrl = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemoteUrl)));
                }
            }
        }

        public string ClonedPath {
            get {
                return _clonedPath;
            }

            set {
                if (value != _clonedPath) {
                    _clonedPath = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClonedPath)));
                }
            }
        }

        public string Description {
            get {
                return _description;
            }

            set {
                if (value != _description) {
                    _description = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                }
            }
        }

        public bool IsSearchTerm {
            get {
                return _isSearchTerm;
            }

            set {
                if (value != _isSearchTerm) {
                    _isSearchTerm = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSearchTerm)));
                }
            }
        }

        public bool IsUpdateAvailable {
            get {
                return _isUpdateAvailable;
            }

            set {
                if (value != _isUpdateAvailable) {
                    _isUpdateAvailable = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAvailable)));
                }
            }
        }
    }
}
