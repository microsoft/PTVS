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
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Represents a category of options.  Currently only used for formatting options.
    /// </summary>
    class OptionCategory {
        public readonly string Description;
        public readonly OptionInfo[] Options;
        private static Dictionary<CodeFormattingCategory, List<OptionInfo>> _cachedOptions = new Dictionary<CodeFormattingCategory, List<OptionInfo>>();

        public OptionCategory(string description, params OptionInfo[] options) {
            Description = description;
            Options = options;
        }

        public static OptionInfo[] GetOptions(CodeFormattingCategory category) {
            List<OptionInfo> res;
            if (!_cachedOptions.TryGetValue(category, out res)) {
                res = new List<OptionInfo>();
                foreach (var prop in typeof(CodeFormattingOptions).GetProperties()) {
                    var attrs = prop.GetCustomAttributes(typeof(CodeFormattingCategoryAttribute), false);
                    if (attrs.Length > 0) {
                        if (((CodeFormattingCategoryAttribute)attrs[0]).Category == category) {
                            var desc = prop.GetCustomAttributes(typeof(CodeFormattingDescriptionAttribute), false);
                            string descStr = prop.Name, toolTip = "";
                            if (desc.Length > 0) {
                                descStr = ((CodeFormattingDescriptionAttribute)desc[0]).ShortDescription;
                                toolTip = ((CodeFormattingDescriptionAttribute)desc[0]).LongDescription;
                            }

                            string previewOn = "", previewOff = "";
                            desc = prop.GetCustomAttributes(typeof(CodeFormattingExampleAttribute), false);
                            if (desc.Length > 0) {
                                previewOn = ((CodeFormattingExampleAttribute)desc[0]).On;
                                previewOff = ((CodeFormattingExampleAttribute)desc[0]).Off;
                            };

                            object defaultValue = null;
                            desc = prop.GetCustomAttributes(typeof(CodeFormattingDefaultValueAttribute), false);
                            if (desc.Length > 0) {
                                defaultValue = ((CodeFormattingDefaultValueAttribute)desc[0]).DefaultValue;
                            }

                            if (prop.PropertyType == typeof(bool)) {
                                res.Add(new BooleanOptionInfo(descStr, prop.Name, toolTip, previewOn, previewOff, (bool)defaultValue));
                            } else if (prop.PropertyType == typeof(bool?)) {
                                res.Add(new TriStateOptionInfo(descStr, prop.Name, toolTip, previewOn, previewOff, (bool?)defaultValue));
                            }else if(prop.PropertyType == typeof(int)) {
                                res.Add(new IntegerOptionInfo(descStr, prop.Name, toolTip, previewOn, (int)defaultValue));
                            } else {
                                throw new InvalidOperationException();
                            }
                        }
                    }
                }
            }
            return res.ToArray();
        }

    }
}
