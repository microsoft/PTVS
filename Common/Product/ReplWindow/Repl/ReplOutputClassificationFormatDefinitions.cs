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

using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

#if NTVS_FEATURE_INTERACTIVEWINDOW
namespace Microsoft.NodejsTools.Repl {
#else
namespace Microsoft.VisualStudio.Repl {
#endif
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]    
    [UserVisible(true)]
    internal class InteractiveBlackFormatDefinition : ClassificationFormatDefinition {
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - Black";
#else
        public const string Name = "Interactive - Black";
#endif
        


        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveBlackFormatDefinition() {
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkRedFormatDefinition : ClassificationFormatDefinition {
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - DarkRed";
#else
        public const string Name = "Interactive - DarkRed";
#endif
        

        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveDarkRedFormatDefinition() {
            ForegroundColor = Color.FromRgb(0x7f, 0, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkGreenFormatDefinition : ClassificationFormatDefinition {
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - DarkGreen";
#else
        public const string Name = "Interactive - DarkGreen";
#endif
        

        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveDarkGreenFormatDefinition() {
            ForegroundColor = Color.FromRgb(0x00, 0x7f, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkYellowFormatDefinition : ClassificationFormatDefinition {
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - DarkYellow";
#else
        public const string Name = "Interactive - DarkYellow";
#endif
        

        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveDarkYellowFormatDefinition() {
            ForegroundColor = Color.FromRgb(0x7f, 0x7f, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkBlueFormatDefinition : ClassificationFormatDefinition {
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - DarkBlue";
#else
        public const string Name = "Interactive - DarkBlue";
#endif

        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveDarkBlueFormatDefinition() {
            ForegroundColor = Color.FromRgb(0x00, 0x00, 0x7f);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkMagentaFormatDefinition : ClassificationFormatDefinition {        
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - DarkMagenta";
#else
        public const string Name = "Interactive - DarkMagenta";
#endif
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveDarkMagentaFormatDefinition() {
            ForegroundColor = Color.FromRgb(0x7f, 0x00, 0x7f);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkCyanFormatDefinition : ClassificationFormatDefinition {        
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - DarkCyan";
#else
        public const string Name = "Interactive - DarkCyan";
#endif
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveDarkCyanFormatDefinition() {
            ForegroundColor = Color.FromRgb(0x00, 0x7f, 0x7f);
        }
    }
#if NTVS_FEATURE_INTERACTIVEWINDOW
#else
#endif
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveGrayFormatDefinition : ClassificationFormatDefinition {        
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - Gray";
#else
        public const string Name = "Interactive - Gray";
#endif
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        public InteractiveGrayFormatDefinition() {
            ForegroundColor = Color.FromRgb(0xC0, 0xC0, 0xC0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveDarkGrayFormatDefinition : ClassificationFormatDefinition {        
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - DarkGray";
#else
        public const string Name = "Interactive - DarkGray";
#endif
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveDarkGrayFormatDefinition() {
            ForegroundColor = Color.FromRgb(0x7f, 0x7f, 0x7f);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveRedFormatDefinition : ClassificationFormatDefinition {        
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - Red";
#else
        public const string Name = "Interactive - Red";
#endif
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveRedFormatDefinition() {
            ForegroundColor = Color.FromRgb(0xff, 0, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveGreenFormatDefinition : ClassificationFormatDefinition {        
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - Green";
#else
        public const string Name = "Interactive - Green";
#endif
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveGreenFormatDefinition() {
            ForegroundColor = Color.FromRgb(0x00, 0xff, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveYellowFormatDefinition : ClassificationFormatDefinition {        
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - Yellow";
#else
        public const string Name = "Interactive - Yellow";
#endif
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveYellowFormatDefinition() {
            ForegroundColor = Color.FromRgb(0xff, 0xff, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    [Order(After = Priority.Default, Before = Priority.High)]
    internal class InteractiveBlueFormatDefinition : ClassificationFormatDefinition {        
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - Blue";
#else
        public const string Name = "Interactive - Blue";
#endif
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveBlueFormatDefinition() {
            ForegroundColor = Color.FromRgb(0x00, 0x00, 0xff);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveMagentaFormatDefinition : ClassificationFormatDefinition {        
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - Magenta";
#else
        public const string Name = "Interactive - Magenta";
#endif
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveMagentaFormatDefinition() {
            ForegroundColor = Color.FromRgb(0xff, 0x00, 0xff);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveCyanFormatDefinition : ClassificationFormatDefinition {        
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - Cyan";
#else
        public const string Name = "Interactive - Cyan";
#endif
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF

        public InteractiveCyanFormatDefinition() {
            ForegroundColor = Color.FromRgb(0x00, 0xff, 0xff);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    [DisplayName(Name)]
    [UserVisible(true)]
    internal class InteractiveWhiteFormatDefinition : ClassificationFormatDefinition {        
#if NTVS_FEATURE_INTERACTIVEWINDOW
        public const string Name = "Node.js Interactive - White";
#else
        public const string Name = "Interactive - White";
#endif
        [Export]
        [Name(Name)]
        [BaseDefinition(PredefinedClassificationTypeNames.NaturalLanguage)]
        internal static ClassificationTypeDefinition Definition = null; // Set via MEF
        
        public InteractiveWhiteFormatDefinition() {
            // not really white by default so the user can actually see "white" text.
            ForegroundColor = Color.FromRgb(0x7f, 0x7f, 0x7f);
        }
    }
}
