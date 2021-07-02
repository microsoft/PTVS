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

using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.CookiecutterTools.Model;

namespace Microsoft.CookiecutterTools.ViewModel
{
    class TemplateViewModel : TreeItemViewModel
    {
        private string _displayName;
        private string _remoteUrl;
        private string _ownerUrl;
        private string _ownerTooltip;
        private string _clonedPath;
        private string _description;
        private string _avatarUrl;
        private string _category;
        private bool _isSearchTerm;
        private bool _isUpdateAvailable;

        public TemplateViewModel()
        {
        }

        public override string ToString() => DisplayName;

        public bool Selectable => true;

        public string GitHubHomeUrl => RemoteUrl;

        public string GitHubIssuesUrl => RemoteUrl != null ? RemoteUrl + "/issues" : null;

        public string GitHubWikiUrl => RemoteUrl != null ? RemoteUrl + "/wiki" : null;

        public bool HasDetails
        {
            get
            {
                return !string.IsNullOrEmpty(Description) &&
                       !string.IsNullOrEmpty(AvatarUrl) &&
                       !string.IsNullOrEmpty(OwnerUrl);
            }
        }

        /// <summary>
        /// Repository name.
        /// </summary>
        public string RepositoryName
        {
            get
            {
                if (string.IsNullOrEmpty(RemoteUrl))
                {
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
        public string RepositoryOwner
        {
            get
            {
                if (string.IsNullOrEmpty(RemoteUrl))
                {
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
        public string RepositoryFullName
        {
            get
            {
                if (string.IsNullOrEmpty(RemoteUrl))
                {
                    return string.Empty;
                }

                string owner;
                string name;
                ParseUtils.ParseGitHubRepoOwnerAndName(RemoteUrl, out owner, out name);
                if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(name))
                {
                    return owner + '/' + name;
                }
                return string.Empty;
            }
        }

        public string DisplayName
        {
            get
            {
                return _displayName;
            }

            set
            {
                if (value != _displayName)
                {
                    _displayName = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(DisplayName)));
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(AutomationName)));
                }
            }
        }

        public string RemoteUrl
        {
            get
            {
                return _remoteUrl;
            }

            set
            {
                if (value != _remoteUrl)
                {
                    _remoteUrl = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(RemoteUrl)));
                }

                RefreshOwnerTooltip();
            }
        }

        public string ClonedPath
        {
            get
            {
                return _clonedPath;
            }

            set
            {
                if (value != _clonedPath)
                {
                    _clonedPath = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(ClonedPath)));
                }
            }
        }

        public string Description
        {
            get
            {
                return _description;
            }

            set
            {
                if (value != _description)
                {
                    _description = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(Description)));
                }
            }
        }

        public string AvatarUrl
        {
            get
            {
                return _avatarUrl;
            }

            set
            {
                if (value != _avatarUrl)
                {
                    _avatarUrl = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(AvatarUrl)));
                }
            }
        }

        public string OwnerUrl
        {
            get
            {
                return _ownerUrl;
            }

            set
            {
                if (value != _ownerUrl)
                {
                    _ownerUrl = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(OwnerUrl)));
                }
            }
        }

        public string OwnerTooltip
        {
            get
            {
                return _ownerTooltip;
            }

            set
            {
                if (value != _ownerTooltip)
                {
                    _ownerTooltip = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(OwnerTooltip)));
                }
            }
        }

        public bool IsSearchTerm
        {
            get
            {
                return _isSearchTerm;
            }

            set
            {
                if (value != _isSearchTerm)
                {
                    _isSearchTerm = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSearchTerm)));
                }
            }
        }

        public bool IsUpdateAvailable
        {
            get
            {
                return _isUpdateAvailable;
            }

            set
            {
                if (value != _isUpdateAvailable)
                {
                    _isUpdateAvailable = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsUpdateAvailable)));
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(AutomationHelpText)));
                }
            }
        }

        public string Category
        {
            get
            {
                return _category;
            }
            set
            {
                if (value != _category)
                {
                    _category = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(Category)));
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(AutomationHelpText)));
                }
            }
        }

        public override string AutomationHelpText =>
            (IsUpdateAvailable ? Strings.SearchPage_CategoryHelpTextUpdate : Strings.SearchPage_CategoryHelpTextNoUpdate)
                .FormatUI(Category);

        private void RefreshOwnerTooltip()
        {
            var owner = RepositoryOwner;
            if (string.IsNullOrEmpty(owner))
            {
                owner = Strings.SearchPage_Creator;
            }

            OwnerTooltip = Strings.SearchPage_VisitOwnerOnGitHub.FormatUI(owner);
        }
    }
}
