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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.PythonTools.Django.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Django.Intellisense {
    static class AnalyzerExtensions {
        public static string[] GetTags(this VsProjectAnalyzer analyzer) {
            var tags = analyzer.SendExtensionCommandAsync(
                DjangoAnalyzer.Name,
                DjangoAnalyzer.Commands.GetTags,
                string.Empty
            ).WaitOrDefault(1000);

            if (tags != null) {
                return new JavaScriptSerializer().Deserialize<string[]>(tags);
            }

            return Array.Empty<string>();
        }

        public static Dictionary<string, TagInfo> GetFilters(this VsProjectAnalyzer analyzer) {
            var filtersRes = analyzer.SendExtensionCommandAsync(
                DjangoAnalyzer.Name,
                DjangoAnalyzer.Commands.GetFilters,
                string.Empty
            ).WaitOrDefault(1000);


            var res = new Dictionary<string, TagInfo>();
            if (filtersRes != null) {
                var filters = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(filtersRes);
                foreach (var filter in filters) {
                    res[filter.Key] = new TagInfo(filter.Value, null);
                }
            }
            return res;
        }

        public static string[] GetUrls(this VsProjectAnalyzer analyzer)
        {
            var urls = analyzer.SendExtensionCommandAsync(
                DjangoAnalyzer.Name,
                DjangoAnalyzer.Commands.GetUrls,
                string.Empty
            ).WaitOrDefault(1000);

            return urls != null ? new JavaScriptSerializer().Deserialize<string[]>(urls) : Array.Empty<string>();
        }

        public static string[] GetVariableNames(this VsProjectAnalyzer analyzer, string file) {
            var variables = analyzer.SendExtensionCommandAsync(
                DjangoAnalyzer.Name,
                DjangoAnalyzer.Commands.GetVariables,
                file
            ).WaitOrDefault(1000);

            if (variables != null) {
                return new JavaScriptSerializer().Deserialize<string[]>(variables);
            }

            return Array.Empty<string>();
        }

        public static Dictionary<string, PythonMemberType> GetMembers(this VsProjectAnalyzer analyzer, string file, string variable) {
            var serializer = new JavaScriptSerializer();

            var members = analyzer.SendExtensionCommandAsync(
                DjangoAnalyzer.Name,
                DjangoAnalyzer.Commands.GetMembers,
                serializer.Serialize(new[] { file, variable })
            ).WaitOrDefault(1000);

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
