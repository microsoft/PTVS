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
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.PythonTools.Django.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Django.Intellisense {
    static class AnalyzerExtensions {
        public static string[] GetTags(this VsProjectAnalyzer analyzer) {
            var tags = analyzer.WaitForRequest(analyzer.SendExtensionCommandAsync(
                DjangoAnalyzer.Name,
                DjangoAnalyzer.Commands.GetTags,
                string.Empty
            ), "Django.GetTags");

            if (tags != null) {
                return new JavaScriptSerializer().Deserialize<string[]>(tags);
            }

            return Array.Empty<string>();
        }

        public static Dictionary<string, TagInfo> GetFilters(this VsProjectAnalyzer analyzer) {
            var filtersRes = analyzer.WaitForRequest(analyzer.SendExtensionCommandAsync(
                DjangoAnalyzer.Name,
                DjangoAnalyzer.Commands.GetFilters,
                string.Empty
            ), "Django.GetFilters");


            var res = new Dictionary<string, TagInfo>();
            if (filtersRes != null) {
                var filters = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(filtersRes);
                foreach (var filter in filters) {
                    res[filter.Key] = new TagInfo(filter.Value, null);
                }
            }
            return res;
        }

        public static DjangoUrl[] GetUrls(this VsProjectAnalyzer analyzer) {
            var urls = analyzer.WaitForRequest(analyzer.SendExtensionCommandAsync(
                DjangoAnalyzer.Name,
                DjangoAnalyzer.Commands.GetUrls,
                string.Empty
            ), "Django.GetUrls");

            return urls != null ? new JavaScriptSerializer().Deserialize<DjangoUrl[]>(urls) : Array.Empty<DjangoUrl>();
        }

        public static string[] GetVariableNames(this VsProjectAnalyzer analyzer, string file) {
            var variables = analyzer.WaitForRequest(analyzer.SendExtensionCommandAsync(
                DjangoAnalyzer.Name,
                DjangoAnalyzer.Commands.GetVariables,
                file
            ), "Django.GetVariableNames");

            if (variables != null) {
                return new JavaScriptSerializer().Deserialize<string[]>(variables);
            }

            return Array.Empty<string>();
        }

        public static Dictionary<string, PythonMemberType> GetMembers(this VsProjectAnalyzer analyzer, string file, string variable) {
            var serializer = new JavaScriptSerializer();

            var members = analyzer.WaitForRequest(analyzer.SendExtensionCommandAsync(
                DjangoAnalyzer.Name,
                DjangoAnalyzer.Commands.GetMembers,
                serializer.Serialize(new[] { file, variable })
            ), "Django.GetMembers");

            if (members != null) {
                var res = serializer.Deserialize<Dictionary<string, string>>(members);

                return res.ToDictionary(
                    x => x.Key,
                    x => (PythonMemberType)Enum.Parse(typeof(PythonMemberType), x.Value, true)
                );
            }


            return new Dictionary<string, PythonMemberType>();
        }
    }
}
