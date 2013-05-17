/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
