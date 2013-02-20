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
using System.Windows.Forms;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Options {
    abstract class OptionInfo {
        public readonly string Text;
        public readonly string ToolTip;
        public readonly string Key;

        public OptionInfo(string text, string toolTip, string key) {
            Text = text;
            ToolTip = toolTip;
            Key = key;
        }

        public abstract OptionSettingNode CreateNode();
        public abstract string SerializeOptionValue(object value);
        public abstract object DeserializeOptionValue(string value);
        public abstract string GetPreviewText(object optionValue);
        public abstract object DefaultValue {
            get;
        }
    }

    class BooleanOptionInfo : OptionInfo {
        public readonly string PreviewOn;
        public readonly string PreviewOff;
        public readonly bool Default;

        public BooleanOptionInfo(string text, string key, string toolTip, string previewOn, string previewOff, bool defaultValue) :
            base(text, toolTip, key) {
            PreviewOn = previewOn;
            PreviewOff = previewOff;
            Default = defaultValue;
        }

        public override OptionSettingNode CreateNode() {
            return new BooleanCheckBoxNode(Text);
        }

        public override string SerializeOptionValue(object value) {
            return value.ToString();
        }

        public override object DeserializeOptionValue(string value) {
            bool b;
            if (Boolean.TryParse(value, out b)) {
                return b;
            }
            return Default;
        }

        public override string GetPreviewText(object optionValue) {
            return ((bool)optionValue) ? PreviewOn : PreviewOff;
        }

        public override object DefaultValue {
            get {
                return Default;
            }
        }
    }

    class TriStateOptionInfo : OptionInfo {
        public readonly string PreviewOn;
        public readonly string PreviewOff;
        public readonly bool? Default;

        public TriStateOptionInfo(string text, string key, string toolTip, string previewOn, string previewOff, bool? defaultValue) :
            base(text, toolTip, key) {
            PreviewOn = previewOn;
            PreviewOff = previewOff;
            Default = defaultValue;
        }

        public override OptionSettingNode CreateNode() {
            return new TriStateCheckBoxNode(Text);
        }

        public override string SerializeOptionValue(object value) {
            if (value == null) {
                return "-";
            }

            return ((bool?)value).ToString();
        }

        public override object DeserializeOptionValue(string value) {
            bool b;
            if (value == "-") {
                return null;
            } else if (Boolean.TryParse(value, out b)) {
                return b;
            }
            return Default;
        }

        public override string GetPreviewText(object optionValue) {
            if (optionValue == null) {
                return "# The existing formatting will not be altered:" +
                    Environment.NewLine +
                    Environment.NewLine +
                    PreviewOn +
                    Environment.NewLine +
                    "    # or" +
                    Environment.NewLine +
                    PreviewOff;
            } else {
                return ((bool)optionValue) ? PreviewOn : PreviewOff;
            }
        }

        public override object DefaultValue {
            get {
                return Default;
            }
        }
    }

    class IntegerOptionInfo : OptionInfo {
        public readonly string PreviewText;
        public readonly int Default;

        public IntegerOptionInfo(string text, string key, string toolTip, string preview, int defaultValue) :
            base(text, toolTip, key) {
            PreviewText = preview;
            Default = defaultValue;
        }

        public override OptionSettingNode CreateNode() {
            return new IntegerNode(Text);
        }

        public override string SerializeOptionValue(object value) {
            return value.ToString();
        }

        public override object DeserializeOptionValue(string value) {
            int i;
            if (Int32.TryParse(value, out i)) {
                return i;
            }
            return Default;
        }

        public override string GetPreviewText(object optionValue) {
            return PreviewText;
        }

        public override object DefaultValue {
            get { return Default; }
        }
    }
}
