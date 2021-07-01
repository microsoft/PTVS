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


// TODO: Localization - How to localize the different classification names

namespace Microsoft.PythonTools.Repl
{
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveBlackFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - Black";

        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveBlackFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_Black;
            ForegroundColor = Colors.Black;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkRedFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - DarkRed";

        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveDarkRedFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_DarkRed;
            ForegroundColor = Color.FromRgb(0x7f, 0, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkGreenFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - DarkGreen";

        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveDarkGreenFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_DarkGreen;
            ForegroundColor = Color.FromRgb(0x00, 0x7f, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkYellowFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - DarkYellow";

        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveDarkYellowFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_DarkYellow;
            ForegroundColor = Color.FromRgb(0x7f, 0x7f, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkBlueFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - DarkBlue";

        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveDarkBlueFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_DarkBlue;
            ForegroundColor = Color.FromRgb(0x00, 0x00, 0x7f);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkMagentaFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - DarkMagenta";
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveDarkMagentaFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_DarkMagenta;
            ForegroundColor = Color.FromRgb(0x7f, 0x00, 0x7f);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkCyanFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - DarkCyan";
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveDarkCyanFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_DarkCyan;
            ForegroundColor = Color.FromRgb(0x00, 0x7f, 0x7f);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveGrayFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - Gray";
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        public InteractiveGrayFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_Gray;
            ForegroundColor = Color.FromRgb(0xC0, 0xC0, 0xC0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkGrayFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - DarkGray";
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveDarkGrayFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_DarkGray;
            ForegroundColor = Color.FromRgb(0x7f, 0x7f, 0x7f);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveRedFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - Red";
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveRedFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_Red;
            ForegroundColor = Color.FromRgb(0xff, 0, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveGreenFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - Green";
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveGreenFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_Green;
            ForegroundColor = Color.FromRgb(0x00, 0xff, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveYellowFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - Yellow";
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveYellowFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_Yellow;
            ForegroundColor = Color.FromRgb(0xff, 0xff, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    [Order(After = Priority.Default, Before = Priority.High)]
    internal class InteractiveBlueFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - Blue";
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveBlueFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_Blue;
            ForegroundColor = Color.FromRgb(0x00, 0x00, 0xff);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveMagentaFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - Magenta";
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveMagentaFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_Magenta;
            ForegroundColor = Color.FromRgb(0xff, 0x00, 0xff);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveCyanFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - Cyan";
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveCyanFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_Cyan;
            ForegroundColor = Color.FromRgb(0x00, 0xff, 0xff);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [UserVisible(true)]
    internal class InteractiveWhiteFormatDefinition : ClassificationFormatDefinition
    {
        public const string Name = "Python Interactive - White";
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveWhiteFormatDefinition()
        {
            DisplayName = Strings.PythonInteractive_White;
            ForegroundColor = Color.FromRgb(0xff, 0xff, 0xff);
        }
    }
}
