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

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.Django.Intellisense {
    /// <summary>
    /// Historically, this provided information on variable, tags and filters
    /// defined in the project, in addition to those built in to django.
    /// Now, this only returns a fixed list built in tags and filters.
    /// </summary>
    internal class DjangoProjectAnalyzer : IDjangoProjectAnalyzer {
        private readonly IPythonProject _project;
        private Dictionary<string, string> _tags = BuiltinTags.MakeKnownTagsTable();
        private Dictionary<string, string> _filters = BuiltinFilters.MakeKnownFiltersTable();

        public DjangoProjectAnalyzer(IPythonProject project) {
            _project = project ?? throw new ArgumentNullException(nameof(project));
        }

        public Dictionary<string, string> GetTags() {
            return _tags;
        }

        public Dictionary<string, string> GetFilters() {
            return _filters;
        }

        public Dictionary<string, PythonMemberType> GetMembers(string file, string variable) {
            return new Dictionary<string, PythonMemberType>();
        }

        public DjangoUrl[] GetUrls() {
            return Array.Empty<DjangoUrl>();
        }

        public string[] GetVariableNames(string file) {
           return Array.Empty<string>();
        }
    }
}
