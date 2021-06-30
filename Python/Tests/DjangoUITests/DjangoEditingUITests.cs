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

extern alias util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using TestUtilities;
using TestUtilities.UI.Python;
using util::TestUtilities.UI;
#if DEV16_OR_LATER
using Microsoft.WebTools.Languages.Html.Editor.Document;
using Microsoft.WebTools.Languages.Html.Editor.Tree;
using Microsoft.WebTools.Languages.Html.Editor.Settings;
#else
#endif

namespace DjangoUITests {
    public class DjangoEditingUITests {
        private static void WaitForTextChange(IWpfTextView textView, Action textChange) {
            var are = new AutoResetEvent(false);

            EventHandler<TextContentChangedEventArgs> textChangedHandler = null;
            textChangedHandler = delegate {
                textView.TextBuffer.Changed -= textChangedHandler;
                are.Set();
            };
            textView.TextBuffer.Changed += textChangedHandler;

            textChange();

            Assert.IsTrue(are.WaitOne(5000), "Failed to see text change");
        }

        private static void WaitForHtmlTreeUpdate(IWpfTextView textView) {
            var htmlDoc = HtmlEditorDocument.TryFromTextView(textView);
            Assert.IsNotNull(htmlDoc);

            if (htmlDoc.HtmlEditorTree.IsReady) {
                return;
            }

            var are = new AutoResetEvent(false);

            EventHandler<HtmlTreeUpdatedEventArgs> updateCompletedHandler = null;
            updateCompletedHandler = delegate {
                if (htmlDoc.HtmlEditorTree.IsReady) {
                    if (htmlDoc != null) {
                        htmlDoc.HtmlEditorTree.UpdateCompleted -= updateCompletedHandler;
                        htmlDoc = null;
                        are.Set();
                    }
                }
            };
            htmlDoc.HtmlEditorTree.UpdateCompleted += updateCompletedHandler;

            Assert.IsTrue(are.WaitOne(5000), "Failed to see HTML tree update");
        }

        public void Classifications(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Classification.html.djt", 8, 10, "",
                new Classification("HTML Tag Delimiter", 0, 1, "<"),
                new Classification("HTML Element Name", 1, 5, "html"),
                new Classification("HTML Tag Delimiter", 5, 6, ">"),
                new Classification("HTML Tag Delimiter", 8, 9, "<"),
                new Classification("HTML Element Name", 9, 13, "head"),
                new Classification("HTML Tag Delimiter", 13, 15, "><"),
                new Classification("HTML Element Name", 15, 20, "title"),
                new Classification("HTML Tag Delimiter", 20, 21, ">"),
                new Classification("HTML Tag Delimiter", 24, 26, "</"),
                new Classification("HTML Element Name", 26, 31, "title"),
                new Classification("HTML Tag Delimiter", 31, 34, "></"),
                new Classification("HTML Element Name", 34, 38, "head"),
                new Classification("HTML Tag Delimiter", 38, 39, ">"),
                new Classification("HTML Tag Delimiter", 41, 42, "<"),
                new Classification("HTML Element Name", 42, 46, "body"),
                new Classification("HTML Tag Delimiter", 46, 47, ">"),
                new Classification("Django template tag", 50, 52, "{%"),
                new Classification("keyword", 53, 63, "autoescape"),
                new Classification("keyword", 64, 66, "on"),
                new Classification("Django template tag", 67, 69, "%}"),
                new Classification("Django template tag", 72, 74, "{%"),
                new Classification("keyword", 75, 85, "autoescape"),
                new Classification("keyword", 86, 89, "off"),
                new Classification("Django template tag", 90, 92, "%}"),
                new Classification("Django template tag", 95, 97, "{%"),
                new Classification("keyword", 98, 108, "autoescape"),
                new Classification("keyword", 109, 113, "blah"),
                new Classification("Django template tag", 114, 116, "%}"),
                new Classification("Django template tag", 122, 124, "{%"),
                new Classification("keyword", 125, 132, "comment"),
                new Classification("Django template tag", 133, 135, "%}"),
                new Classification("Django template tag", 144, 146, "{%"),
                new Classification("keyword", 147, 157, "endcomment"),
                new Classification("Django template tag", 158, 160, "%}"),
                new Classification("Django template tag", 166, 168, "{%"),
                new Classification("keyword", 169, 173, "csrf"),
                new Classification("Django template tag", 174, 176, "%}"),
                new Classification("Django template tag", 181, 183, "{%"),
                new Classification("keyword", 184, 189, "cycle"),
                new Classification("excluded code", 189, 203, " 'row1' 'row2'"),
                new Classification("Django template tag", 204, 206, "%}"),
                new Classification("Django template tag", 209, 211, "{%"),
                new Classification("keyword", 212, 217, "cycle"),
                new Classification("excluded code", 217, 238, " 'row1' 'row2' as baz"),
                new Classification("Django template tag", 239, 241, "%}"),
                new Classification("Django template tag", 244, 246, "{%"),
                new Classification("keyword", 247, 252, "cycle"),
                new Classification("excluded code", 252, 256, " baz"),
                new Classification("Django template tag", 257, 259, "%}"),
                new Classification("Django template tag", 265, 267, "{%"),
                new Classification("keyword", 268, 273, "debug"),
                new Classification("Django template tag", 274, 276, "%}"),
                new Classification("Django template tag", 282, 284, "{%"),
                new Classification("keyword", 285, 291, "filter"),
                new Classification("identifier", 292, 304, "force_escape"),
                new Classification("identifier", 305, 310, "lower"),
                new Classification("Django template tag", 311, 313, "%}"),
                new Classification("Django template tag", 316, 318, "{%"),
                new Classification("keyword", 319, 328, "endfilter"),
                new Classification("Django template tag", 329, 331, "%}"),
                new Classification("Django template tag", 337, 339, "{%"),
                new Classification("keyword", 340, 347, "firstof"),
                new Classification("identifier", 348, 352, "var1"),
                new Classification("identifier", 353, 357, "var2"),
                new Classification("identifier", 358, 362, "var3"),
                new Classification("Django template tag", 363, 365, "%}"),
                new Classification("Django template tag", 370, 372, "{%"),
                new Classification("keyword", 373, 380, "ifequal"),
                new Classification("identifier", 381, 385, "user"),
                new Classification("Python dot", 385, 386, "."),
                new Classification("identifier", 386, 388, "id"),
                new Classification("identifier", 389, 396, "comment"),
                new Classification("Python dot", 396, 397, "."),
                new Classification("identifier", 397, 404, "user_id"),
                new Classification("Django template tag", 405, 407, "%}"),
                new Classification("Django template tag", 410, 412, "{%"),
                new Classification("keyword", 413, 423, "endifequal"),
                new Classification("Django template tag", 424, 426, "%}"),
                new Classification("Django template tag", 431, 433, "{%"),
                new Classification("keyword", 434, 441, "ifequal"),
                new Classification("identifier", 442, 446, "user"),
                new Classification("Python dot", 446, 447, "."),
                new Classification("identifier", 447, 449, "id"),
                new Classification("identifier", 450, 457, "comment"),
                new Classification("Python dot", 457, 458, "."),
                new Classification("identifier", 458, 465, "user_id"),
                new Classification("Django template tag", 466, 468, "%}"),
                new Classification("Django template tag", 471, 473, "{%"),
                new Classification("keyword", 474, 478, "else"),
                new Classification("Django template tag", 479, 481, "%}"),
                new Classification("Django template tag", 484, 486, "{%"),
                new Classification("keyword", 487, 497, "endifequal"),
                new Classification("Django template tag", 498, 500, "%}"),
                new Classification("Django template tag", 505, 507, "{%"),
                new Classification("keyword", 508, 518, "ifnotequal"),
                new Classification("identifier", 519, 523, "user"),
                new Classification("Python dot", 523, 524, "."),
                new Classification("identifier", 524, 526, "id"),
                new Classification("identifier", 527, 534, "comment"),
                new Classification("Python dot", 534, 535, "."),
                new Classification("identifier", 535, 542, "user_id"),
                new Classification("Django template tag", 543, 545, "%}"),
                new Classification("Django template tag", 548, 550, "{%"),
                new Classification("keyword", 551, 555, "else"),
                new Classification("Django template tag", 556, 558, "%}"),
                new Classification("Django template tag", 561, 563, "{%"),
                new Classification("keyword", 564, 577, "endifnotequal"),
                new Classification("Django template tag", 578, 580, "%}"),
                new Classification("Django template tag", 585, 587, "{%"),
                new Classification("keyword", 588, 598, "ifnotequal"),
                new Classification("identifier", 599, 603, "user"),
                new Classification("Python dot", 603, 604, "."),
                new Classification("identifier", 604, 606, "id"),
                new Classification("identifier", 607, 614, "comment"),
                new Classification("Python dot", 614, 615, "."),
                new Classification("identifier", 615, 622, "user_id"),
                new Classification("Django template tag", 623, 625, "%}"),
                new Classification("Django template tag", 628, 630, "{%"),
                new Classification("keyword", 631, 644, "endifnotequal"),
                new Classification("Django template tag", 645, 647, "%}"),
                new Classification("Django template tag", 652, 654, "{%"),
                new Classification("keyword", 655, 657, "if"),
                new Classification("identifier", 658, 661, "fob"),
                new Classification("Django template tag", 662, 664, "%}"),
                new Classification("Django template tag", 667, 669, "{%"),
                new Classification("keyword", 670, 675, "endif"),
                new Classification("Django template tag", 676, 678, "%}"),
                new Classification("Django template tag", 683, 685, "{%"),
                new Classification("keyword", 686, 688, "if"),
                new Classification("identifier", 689, 692, "fob"),
                new Classification("Django template tag", 693, 695, "%}"),
                new Classification("Django template tag", 698, 700, "{%"),
                new Classification("keyword", 701, 705, "elif"),
                new Classification("identifier", 706, 709, "oar"),
                new Classification("Django template tag", 710, 712, "%}"),
                new Classification("Django template tag", 715, 717, "{%"),
                new Classification("keyword", 718, 723, "endif"),
                new Classification("Django template tag", 724, 726, "%}"),
                new Classification("Django template tag", 731, 733, "{%"),
                new Classification("keyword", 734, 736, "if"),
                new Classification("identifier", 737, 740, "fob"),
                new Classification("Django template tag", 741, 743, "%}"),
                new Classification("Django template tag", 746, 748, "{%"),
                new Classification("keyword", 749, 753, "else"),
                new Classification("Django template tag", 754, 756, "%}"),
                new Classification("Django template tag", 759, 761, "{%"),
                new Classification("keyword", 762, 767, "endif"),
                new Classification("Django template tag", 768, 770, "%}"),
                new Classification("Django template tag", 775, 777, "{%"),
                new Classification("keyword", 778, 781, "for"),
                new Classification("identifier", 782, 783, "x"),
                new Classification("keyword", 784, 786, "in"),
                new Classification("identifier", 787, 790, "abc"),
                new Classification("Django template tag", 791, 793, "%}"),
                new Classification("Django template tag", 796, 798, "{%"),
                new Classification("keyword", 799, 805, "endfor"),
                new Classification("Django template tag", 806, 808, "%}"),
                new Classification("Django template tag", 813, 815, "{%"),
                new Classification("keyword", 816, 819, "for"),
                new Classification("identifier", 820, 821, "x"),
                new Classification("keyword", 822, 824, "in"),
                new Classification("identifier", 825, 828, "abc"),
                new Classification("keyword", 829, 837, "reversed"),
                new Classification("Django template tag", 838, 840, "%}"),
                new Classification("Django template tag", 843, 845, "{%"),
                new Classification("keyword", 846, 852, "endfor"),
                new Classification("Django template tag", 853, 855, "%}"),
                new Classification("Django template tag", 860, 862, "{%"),
                new Classification("keyword", 863, 866, "for"),
                new Classification("identifier", 868, 869, "x"),
                new Classification("keyword", 871, 873, "in"),
                new Classification("identifier", 875, 878, "abc"),
                new Classification("keyword", 880, 888, "reversed"),
                new Classification("Django template tag", 889, 891, "%}"),
                new Classification("Django template tag", 894, 896, "{%"),
                new Classification("keyword", 897, 903, "endfor"),
                new Classification("Django template tag", 904, 906, "%}"),
                new Classification("Django template tag", 911, 913, "{%"),
                new Classification("keyword", 914, 918, "load"),
                new Classification("Django template tag", 928, 930, "%}"),
                new Classification("Django template tag", 933, 935, "{%"),
                new Classification("keyword", 936, 940, "load"),
                new Classification("Django template tag", 954, 956, "%}"),
                new Classification("Django template tag", 961, 963, "{%"),
                new Classification("keyword", 964, 967, "now"),
                new Classification("excluded code", 967, 973, " 'Y H'"),
                new Classification("Django template tag", 974, 976, "%}"),
                new Classification("Django template tag", 981, 983, "{%"),
                new Classification("keyword", 984, 991, "regroup"),
                new Classification("excluded code", 991, 1019, " people by gender as grouped"),
                new Classification("Django template tag", 1020, 1022, "%}"),
                new Classification("Django template tag", 1027, 1029, "{%"),
                new Classification("keyword", 1030, 1039, "spaceless"),
                new Classification("Django template tag", 1040, 1042, "%}"),
                new Classification("HTML Tag Delimiter", 1046, 1047, "<"),
                new Classification("HTML Element Name", 1047, 1048, "p"),
                new Classification("HTML Tag Delimiter", 1048, 1049, ">"),
                new Classification("HTML Tag Delimiter", 1053, 1055, "</"),
                new Classification("HTML Element Name", 1055, 1056, "p"),
                new Classification("HTML Tag Delimiter", 1056, 1057, ">"),
                new Classification("Django template tag", 1060, 1062, "{%"),
                new Classification("keyword", 1063, 1075, "endspaceless"),
                new Classification("Django template tag", 1076, 1078, "%}"),
                new Classification("Django template tag", 1083, 1085, "{%"),
                new Classification("keyword", 1086, 1089, "ssi"),
                new Classification("excluded code", 1089, 1103, " /home/fob.txt"),
                new Classification("Django template tag", 1104, 1106, "%}"),
                new Classification("Django template tag", 1113, 1115, "{%"),
                new Classification("keyword", 1116, 1128, "unknownblock"),
                new Classification("Django template tag", 1129, 1131, "%}"),
                new Classification("Django template tag", 1137, 1139, "{%"),
                new Classification("keyword", 1140, 1151, "templatetag"),
                new Classification("keyword", 1152, 1161, "openblock"),
                new Classification("Django template tag", 1162, 1164, "%}"),
                new Classification("Django template tag", 1170, 1172, "{%"),
                new Classification("keyword", 1173, 1184, "templatetag"),
                new Classification("keyword", 1185, 1195, "closeblock"),
                new Classification("Django template tag", 1196, 1198, "%}"),
                new Classification("Django template tag", 1204, 1206, "{%"),
                new Classification("keyword", 1207, 1217, "widthratio"),
                new Classification("identifier", 1218, 1221, "fob"),
                new Classification("Django template tag", 1222, 1224, "%}"),
                new Classification("Django template tag", 1230, 1232, "{{"),
                new Classification("identifier", 1233, 1236, "fob"),
                new Classification("Django template tag", 1247, 1249, "}}"),
                new Classification("HTML Tag Delimiter", 1251, 1253, "</"),
                new Classification("HTML Element Name", 1253, 1257, "body"),
                new Classification("HTML Tag Delimiter", 1257, 1258, ">"),
                new Classification("HTML Tag Delimiter", 1260, 1262, "</"),
                new Classification("HTML Element Name", 1262, 1266, "html"),
                new Classification("HTML Tag Delimiter", 1266, 1267, ">")
            );
        }

        public void Insertion1(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Insertion1.html.djt", 8, 10, "}",
                new Classification("HTML Tag Delimiter", 0, 1, "<"),
                new Classification("HTML Element Name", 1, 5, "html"),
                new Classification("HTML Tag Delimiter", 5, 6, ">"),
                new Classification("HTML Tag Delimiter", 8, 9, "<"),
                new Classification("HTML Element Name", 9, 13, "head"),
                new Classification("HTML Tag Delimiter", 13, 15, "><"),
                new Classification("HTML Element Name", 15, 20, "title"),
                new Classification("HTML Tag Delimiter", 20, 23, "></"),
                new Classification("HTML Element Name", 23, 28, "title"),
                new Classification("HTML Tag Delimiter", 28, 31, "></"),
                new Classification("HTML Element Name", 31, 35, "head"),
                new Classification("HTML Tag Delimiter", 35, 36, ">"),
                new Classification("HTML Tag Delimiter", 40, 41, "<"),
                new Classification("HTML Element Name", 41, 45, "body"),
                new Classification("HTML Tag Delimiter", 45, 46, ">"),
                new Classification("HTML Tag Delimiter", 48, 49, "<"),
                new Classification("HTML Element Name", 49, 55, "script"),
                new Classification("HTML Tag Delimiter", 55, 56, ">"),
                new Classification("HTML Tag Delimiter", 58, 60, "</"),
                new Classification("HTML Element Name", 60, 66, "script"),
                new Classification("HTML Tag Delimiter", 66, 67, ">"),
                new Classification("Django template tag", 71, 73, "{{"),
                new Classification("identifier", 74, 78, "faoo"),
                new Classification("Django template tag", 79, 81, "}}"),
                new Classification("Django template tag", 85, 87, "{{"),
                new Classification("identifier", 88, 91, "fob"),
                new Classification("Django template tag", 92, 94, "}}"),
                new Classification("HTML Tag Delimiter", 96, 98, "</"),
                new Classification("HTML Element Name", 98, 102, "body"),
                new Classification("HTML Tag Delimiter", 102, 103, ">"),
                new Classification("HTML Tag Delimiter", 105, 107, "</"),
                new Classification("HTML Element Name", 107, 111, "html"),
                new Classification("HTML Tag Delimiter", 111, 112, ">")
            );

            InsertionTest(app, "Insertion1.html.djt", 8, 10, "}aaa",
                new Classification("HTML Tag Delimiter", 0, 1, "<"),
                new Classification("HTML Element Name", 1, 5, "html"),
                new Classification("HTML Tag Delimiter", 5, 6, ">"),
                new Classification("HTML Tag Delimiter", 8, 9, "<"),
                new Classification("HTML Element Name", 9, 13, "head"),
                new Classification("HTML Tag Delimiter", 13, 15, "><"),
                new Classification("HTML Element Name", 15, 20, "title"),
                new Classification("HTML Tag Delimiter", 20, 23, "></"),
                new Classification("HTML Element Name", 23, 28, "title"),
                new Classification("HTML Tag Delimiter", 28, 31, "></"),
                new Classification("HTML Element Name", 31, 35, "head"),
                new Classification("HTML Tag Delimiter", 35, 36, ">"),
                new Classification("HTML Tag Delimiter", 40, 41, "<"),
                new Classification("HTML Element Name", 41, 45, "body"),
                new Classification("HTML Tag Delimiter", 45, 46, ">"),
                new Classification("HTML Tag Delimiter", 48, 49, "<"),
                new Classification("HTML Element Name", 49, 55, "script"),
                new Classification("HTML Tag Delimiter", 55, 56, ">"),
                new Classification("HTML Tag Delimiter", 58, 60, "</"),
                new Classification("HTML Element Name", 60, 66, "script"),
                new Classification("HTML Tag Delimiter", 66, 67, ">"),
                new Classification("Django template tag", 71, 73, "{{"),
                new Classification("identifier", 74, 78, "faoo"),
                new Classification("Django template tag", 79, 81, "}}"),
                new Classification("Django template tag", 88, 90, "{{"),
                new Classification("identifier", 91, 94, "fob"),
                new Classification("Django template tag", 95, 97, "}}"),
                new Classification("HTML Tag Delimiter", 99, 101, "</"),
                new Classification("HTML Element Name", 101, 105, "body"),
                new Classification("HTML Tag Delimiter", 105, 106, ">"),
                new Classification("HTML Tag Delimiter", 108, 110, "</"),
                new Classification("HTML Element Name", 110, 114, "html"),
                new Classification("HTML Tag Delimiter", 114, 115, ">")
            );
        }

        public void Insertion2(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionDeletionTest(app, "Insertion2.html.djt", 9, 34, "{",
                new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("Django template tag", 8, 10, "{{"),
                    new Classification("Django template tag", 13, 15, "}}"),
                    new Classification("HTML Tag Delimiter", 17, 18, "<"),
                    new Classification("HTML Element Name", 18, 22, "head"),
                    new Classification("HTML Tag Delimiter", 22, 24, "><"),
                    new Classification("HTML Element Name", 24, 29, "title"),
                    new Classification("HTML Tag Delimiter", 29, 32, "></"),
                    new Classification("HTML Element Name", 32, 37, "title"),
                    new Classification("HTML Tag Delimiter", 37, 40, "></"),
                    new Classification("HTML Element Name", 40, 44, "head"),
                    new Classification("HTML Tag Delimiter", 44, 45, ">"),
                    new Classification("HTML Tag Delimiter", 49, 50, "<"),
                    new Classification("HTML Element Name", 50, 54, "body"),
                    new Classification("HTML Tag Delimiter", 54, 55, ">"),
                    new Classification("HTML Tag Delimiter", 57, 58, "<"),
                    new Classification("HTML Element Name", 58, 64, "script"),
                    new Classification("HTML Tag Delimiter", 64, 65, ">"),
                    new Classification("HTML Tag Delimiter", 67, 69, "</"),
                    new Classification("HTML Element Name", 69, 75, "script"),
                    new Classification("HTML Tag Delimiter", 75, 76, ">"),
                    new Classification("Django template tag", 96, 98, "{{"),
                    new Classification("identifier", 99, 103, "faoo"),
                    new Classification("Django template tag", 106, 108, "}}"),
                    new Classification("Django template tag", 113, 115, "{{"),
                    new Classification("identifier", 116, 119, "fob"),
                    new Classification("Django template tag", 120, 122, "}}"),
                    new Classification("HTML Tag Delimiter", 124, 126, "</"),
                    new Classification("HTML Element Name", 126, 130, "body"),
                    new Classification("HTML Tag Delimiter", 130, 131, ">"),
                    new Classification("HTML Tag Delimiter", 133, 135, "</"),
                    new Classification("HTML Element Name", 135, 139, "html"),
                    new Classification("HTML Tag Delimiter", 139, 140, ">")
                },
                new Classification[]     {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("Django template tag", 8, 10, "{{"),
                    new Classification("Django template tag", 13, 15, "}}"),
                    new Classification("HTML Tag Delimiter", 17, 18, "<"),
                    new Classification("HTML Element Name", 18, 22, "head"),
                    new Classification("HTML Tag Delimiter", 22, 24, "><"),
                    new Classification("HTML Element Name", 24, 29, "title"),
                    new Classification("HTML Tag Delimiter", 29, 32, "></"),
                    new Classification("HTML Element Name", 32, 37, "title"),
                    new Classification("HTML Tag Delimiter", 37, 40, "></"),
                    new Classification("HTML Element Name", 40, 44, "head"),
                    new Classification("HTML Tag Delimiter", 44, 45, ">"),
                    new Classification("HTML Tag Delimiter", 49, 50, "<"),
                    new Classification("HTML Element Name", 50, 54, "body"),
                    new Classification("HTML Tag Delimiter", 54, 55, ">"),
                    new Classification("HTML Tag Delimiter", 57, 58, "<"),
                    new Classification("HTML Element Name", 58, 64, "script"),
                    new Classification("HTML Tag Delimiter", 64, 65, ">"),
                    new Classification("HTML Tag Delimiter", 67, 69, "</"),
                    new Classification("HTML Element Name", 69, 75, "script"),
                    new Classification("HTML Tag Delimiter", 75, 76, ">"),
                    new Classification("Django template tag", 96, 98, "{{"),
                    new Classification("identifier", 99, 103, "faoo"),
                    new Classification("Django template tag", 106, 108, "}}"),
                    new Classification("HTML Tag Delimiter", 123, 125, "</"),
                    new Classification("HTML Element Name", 125, 129, "body"),
                    new Classification("HTML Tag Delimiter", 129, 130, ">"),
                    new Classification("HTML Tag Delimiter", 132, 134, "</"),
                    new Classification("HTML Element Name", 134, 138, "html"),
                    new Classification("HTML Tag Delimiter", 138, 139, ">")
                }
            );
        }

        public void Insertion3(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Insertion3.html.djt", 2, 5, "}",
                new Classification("HTML Tag Delimiter", 0, 1, "<"),
                new Classification("HTML Element Name", 1, 5, "html"),
                new Classification("HTML Tag Delimiter", 5, 6, ">"),
                new Classification("Django template tag", 8, 10, "{{"),
                new Classification("Django template tag", 11, 13, "}}")
            );
        }

        public void Insertion4(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Insertion4.html.djt", 1, 1, "{", new[] {
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 10, 12, "}}")
            });

            InsertionTest(app, "Insertion4.html.djt", 1, 2, "{", new[] {
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 10, 12, "}}")
            });
        }

        public void Insertion5(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Insertion5.html.djt", 1, 2, "#",
                new Classification("Django template tag", 0, 2, "{#"),
                new Classification("HTML Comment", 2, 11, "{<html>\r\n"),
                new Classification("Django template tag", 11, 13, "#}")
            );
        }

        public void Insertion6(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Insertion6.html.djt", 1, 4, "a",
                new Classification("Django template tag", 4, 6, "{{"),
                new Classification("Django template tag", 16, 18, "}}")
            );
        }

        public void Insertion7(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Insertion7.html.djt", 1, 16, "{",
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 10, 12, "}}"),
                new Classification("Django template tag", 15, 17, "{{"),
                new Classification("Django template tag", 28, 30, "}}"),
                new Classification("HTML Tag Delimiter", 38, 39, "<"),
                new Classification("HTML Element Name", 39, 42, "fob"),
                new Classification("HTML Tag Delimiter", 42, 43, ">"),
                new Classification("Django template tag", 49, 51, "{{"),
                new Classification("Django template tag", 61, 63, "}}")
            );
        }

        public void Insertion8(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Insertion8.html.djt", 2, 9, "}",
                new Classification("HTML Tag Delimiter", 0, 1, "<"),
                new Classification("HTML Element Name", 1, 5, "html"),
                new Classification("HTML Tag Delimiter", 5, 6, ">"),
                new Classification("Django template tag", 8, 10, "{{"),
                new Classification("identifier", 11, 14, "fob"),
                new Classification("Django template tag", 15, 17, "}}")
            );
        }

        public void Insertion9(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Insertion9.html.djt", 1, 7, "a",
                new Classification("Django template tag", 4, 6, "{{"),
                new Classification("identifier", 6, 7, "a"),
                new Classification("Django template tag", 17, 19, "}}")
            );
        }

        public void Insertion10(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Insertion10.html.djt", 7, 10, "a",
                new Classification("HTML Tag Delimiter", 0, 1, "<"),
                new Classification("HTML Element Name", 1, 5, "html"),
                new Classification("HTML Tag Delimiter", 5, 6, ">"),
                new Classification("HTML Tag Delimiter", 8, 9, "<"),
                new Classification("HTML Element Name", 9, 13, "head"),
                new Classification("HTML Tag Delimiter", 13, 15, "><"),
                new Classification("HTML Element Name", 15, 20, "title"),
                new Classification("HTML Tag Delimiter", 20, 23, "></"),
                new Classification("HTML Element Name", 23, 28, "title"),
                new Classification("HTML Tag Delimiter", 28, 31, "></"),
                new Classification("HTML Element Name", 31, 35, "head"),
                new Classification("HTML Tag Delimiter", 35, 36, ">"),
                new Classification("HTML Tag Delimiter", 40, 41, "<"),
                new Classification("HTML Element Name", 41, 45, "body"),
                new Classification("HTML Tag Delimiter", 45, 46, ">"),
                new Classification("HTML Tag Delimiter", 48, 49, "<"),
                new Classification("HTML Element Name", 49, 55, "script"),
                new Classification("HTML Tag Delimiter", 55, 56, ">"),
                new Classification("HTML Tag Delimiter", 58, 60, "</"),
                new Classification("HTML Element Name", 60, 66, "script"),
                new Classification("HTML Tag Delimiter", 66, 67, ">"),
                new Classification("Django template tag", 72, 74, "{{"),
                new Classification("identifier", 75, 78, "fob"),
                new Classification("Django template tag", 79, 81, "}}"),
                new Classification("Django template tag", 84, 86, "{{"),
                new Classification("identifier", 87, 91, "faoo"),
                new Classification("Django template tag", 104, 106, "}}"),
                new Classification("HTML Tag Delimiter", 108, 110, "</"),
                new Classification("HTML Element Name", 110, 114, "body"),
                new Classification("HTML Tag Delimiter", 114, 115, ">"),
                new Classification("HTML Tag Delimiter", 117, 119, "</"),
                new Classification("HTML Element Name", 119, 123, "html"),
                new Classification("HTML Tag Delimiter", 123, 124, ">")
            );
        }

        public void Insertion11(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Insertion11.html.djt", 9, 5, 1, "{", true,
                new Classification("Django template tag", 2, 4, "{%"),
                new Classification("keyword", 5, 9, "load"),
                new Classification("Django template tag", 22, 24, "%}"),
                new Classification("Django template tag", 28, 30, "{%"),
                new Classification("keyword", 31, 33, "if"),
                new Classification("identifier", 34, 50, "latest_poll_list"),
                new Classification("Django template tag", 51, 53, "%}"),
                new Classification("HTML Tag Delimiter", 59, 60, "<"),
                new Classification("HTML Element Name", 60, 62, "ul"),
                new Classification("HTML Tag Delimiter", 62, 63, ">"),
                new Classification("Django template tag", 81, 83, "{%"),
                new Classification("keyword", 84, 87, "for"),
                new Classification("identifier", 88, 98, "poll,value"),
                new Classification("keyword", 99, 101, "in"),
                new Classification("identifier", 102, 122, "latest_poll_list_fob"),
                new Classification("Django template tag", 123, 125, "%}"),
                new Classification("Django template tag", 135, 137, "{%"),
                new Classification("keyword", 138, 141, "for"),
                new Classification("identifier", 142, 152, "inner_poll"),
                new Classification("keyword", 153, 155, "in"),
                new Classification("identifier", 156, 172, "latest_poll_list"),
                new Classification("Django template tag", 173, 175, "%}"),
                new Classification("Django template tag", 185, 187, "{%"),
                new Classification("keyword", 188, 194, "endfor"),
                new Classification("Django template tag", 195, 197, "%}"),
                new Classification("Django template tag", 203, 205, "{%"),
                new Classification("keyword", 206, 212, "endfor"),
                new Classification("Django template tag", 213, 215, "%}"),
                new Classification("HTML Tag Delimiter", 223, 225, "</"),
                new Classification("HTML Element Name", 225, 227, "ul"),
                new Classification("HTML Tag Delimiter", 227, 228, ">"),
                new Classification("Django template tag", 230, 232, "{%"),
                new Classification("keyword", 233, 237, "else"),
                new Classification("Django template tag", 238, 240, "%}"),
                new Classification("HTML Tag Delimiter", 246, 247, "<"),
                new Classification("HTML Element Name", 247, 248, "p"),
                new Classification("HTML Tag Delimiter", 248, 249, ">"),
                new Classification("HTML Tag Delimiter", 272, 274, "</"),
                new Classification("HTML Element Name", 274, 275, "p"),
                new Classification("HTML Tag Delimiter", 275, 276, ">"),
                new Classification("Django template tag", 282, 284, "{%"),
                new Classification("keyword", 285, 297, "current_time"),
                new Classification("excluded code", 297, 317, " \"%Y-%m-%d %I:%M %p\""),
                new Classification("Django template tag", 318, 320, "%}"),
                new Classification("Django template tag", 322, 324, "{%"),
                new Classification("keyword", 325, 330, "endif"),
                new Classification("Django template tag", 331, 333, "%}")
            );

            InsertionTest(app, "Insertion11.html.djt", 9, 5, 2, "{%", true,
                new Classification("Django template tag", 2, 4, "{%"),
                new Classification("keyword", 5, 9, "load"),
                new Classification("Django template tag", 22, 24, "%}"),
                new Classification("Django template tag", 28, 30, "{%"),
                new Classification("keyword", 31, 33, "if"),
                new Classification("identifier", 34, 50, "latest_poll_list"),
                new Classification("Django template tag", 51, 53, "%}"),
                new Classification("HTML Tag Delimiter", 59, 60, "<"),
                new Classification("HTML Element Name", 60, 62, "ul"),
                new Classification("HTML Tag Delimiter", 62, 63, ">"),
                new Classification("Django template tag", 81, 83, "{%"),
                new Classification("keyword", 84, 87, "for"),
                new Classification("identifier", 88, 98, "poll,value"),
                new Classification("keyword", 99, 101, "in"),
                new Classification("identifier", 102, 122, "latest_poll_list_fob"),
                new Classification("Django template tag", 123, 125, "%}"),
                new Classification("Django template tag", 135, 137, "{%"),
                new Classification("keyword", 138, 141, "for"),
                new Classification("identifier", 142, 152, "inner_poll"),
                new Classification("keyword", 153, 155, "in"),
                new Classification("identifier", 156, 172, "latest_poll_list"),
                new Classification("Django template tag", 173, 175, "%}"),
                new Classification("Django template tag", 185, 187, "{%"),
                new Classification("keyword", 188, 194, "endfor"),
                new Classification("Django template tag", 195, 197, "%}"),
                new Classification("Django template tag", 203, 205, "{%"),
                new Classification("keyword", 206, 212, "endfor"),
                new Classification("Django template tag", 213, 215, "%}"),
                new Classification("HTML Tag Delimiter", 223, 225, "</"),
                new Classification("HTML Element Name", 225, 227, "ul"),
                new Classification("HTML Tag Delimiter", 227, 228, ">"),
                new Classification("Django template tag", 230, 232, "{%"),
                new Classification("keyword", 233, 237, "else"),
                new Classification("Django template tag", 238, 240, "%}"),
                new Classification("HTML Tag Delimiter", 246, 247, "<"),
                new Classification("HTML Element Name", 247, 248, "p"),
                new Classification("HTML Tag Delimiter", 248, 249, ">"),
                new Classification("HTML Tag Delimiter", 272, 274, "</"),
                new Classification("HTML Element Name", 274, 275, "p"),
                new Classification("HTML Tag Delimiter", 275, 276, ">"),
                new Classification("Django template tag", 282, 284, "{%"),
                new Classification("keyword", 285, 297, "current_time"),
                new Classification("excluded code", 297, 317, " \"%Y-%m-%d %I:%M %p\""),
                new Classification("Django template tag", 318, 320, "%}"),
                new Classification("Django template tag", 322, 324, "{%"),
                new Classification("keyword", 325, 330, "endif"),
                new Classification("Django template tag", 331, 333, "%}")
            );
        }

        public void Insertion12(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Insertion12.html.djt", 9, 61, "}",
                new Classification("HTML Tag Delimiter", 0, 1, "<"),
                new Classification("HTML Element Name", 1, 5, "html"),
                new Classification("HTML Tag Delimiter", 5, 6, ">"),
                new Classification("HTML Tag Delimiter", 8, 9, "<"),
                new Classification("HTML Element Name", 9, 13, "head"),
                new Classification("HTML Tag Delimiter", 13, 15, "><"),
                new Classification("HTML Element Name", 15, 20, "title"),
                new Classification("HTML Tag Delimiter", 20, 23, "></"),
                new Classification("HTML Element Name", 23, 28, "title"),
                new Classification("HTML Tag Delimiter", 28, 31, "></"),
                new Classification("HTML Element Name", 31, 35, "head"),
                new Classification("HTML Tag Delimiter", 35, 36, ">"),
                new Classification("HTML Tag Delimiter", 40, 41, "<"),
                new Classification("HTML Element Name", 41, 45, "body"),
                new Classification("HTML Tag Delimiter", 45, 46, ">"),
                new Classification("HTML Tag Delimiter", 48, 49, "<"),
                new Classification("HTML Element Name", 49, 53, "form"),
                new Classification("HTML Attribute Name", 54, 60, "method"),
                new Classification("HTML Operator", 60, 61, "="),
                new Classification("HTML Attribute Value", 61, 66, "\"get\""),
                new Classification("HTML Tag Delimiter", 66, 67, ">"),
                new Classification("HTML Tag Delimiter", 73, 74, "<"),
                new Classification("HTML Element Name", 74, 79, "table"),
                new Classification("HTML Attribute Name", 80, 86, "border"),
                new Classification("HTML Operator", 86, 87, "="),
                new Classification("HTML Attribute Value", 87, 90, "\"1\""),
                new Classification("HTML Attribute Name", 91, 96, "style"),
                new Classification("HTML Operator", 96, 97, "="),
                new Classification("HTML Attribute Value", 97, 98, "\""),
                new Classification("CSS Property Name", 98, 103, "width"),
                new Classification("CSS Property Value", 105, 109, "100%"),
                new Classification("HTML Attribute Value", 109, 110, "\""),
                new Classification("HTML Tag Delimiter", 110, 111, ">"),
                new Classification("HTML Tag Delimiter", 121, 122, "<"),
                new Classification("HTML Element Name", 122, 124, "tr"),
                new Classification("HTML Tag Delimiter", 124, 125, ">"),
                new Classification("HTML Tag Delimiter", 139, 140, "<"),
                new Classification("HTML Element Name", 140, 142, "td"),
                new Classification("HTML Tag Delimiter", 142, 143, ">"),
                new Classification("HTML Tag Delimiter", 153, 155, "</"),
                new Classification("HTML Element Name", 155, 157, "td"),
                new Classification("HTML Tag Delimiter", 157, 158, ">"),
                new Classification("HTML Tag Delimiter", 172, 173, "<"),
                new Classification("HTML Element Name", 173, 175, "td"),
                new Classification("HTML Tag Delimiter", 175, 177, "><"),
                new Classification("HTML Element Name", 177, 182, "input"),
                new Classification("HTML Attribute Name", 183, 187, "name"),
                new Classification("HTML Operator", 187, 188, "="),
                new Classification("HTML Attribute Value", 188, 199, "\"task_name\""),
                new Classification("HTML Attribute Name", 200, 204, "type"),
                new Classification("HTML Operator", 204, 205, "="),
                new Classification("HTML Attribute Value", 205, 206, "\""),
                new Classification("Django template tag", 206, 208, "{{"),
                new Classification("identifier", 209, 213, "task"),
                new Classification("Python dot", 213, 214, "."),
                new Classification("identifier", 214, 218, "name"),
                new Classification("Django template tag", 219, 221, "}}"),
                new Classification("HTML Attribute Value", 221, 222, "\""),
                new Classification("HTML Tag Delimiter", 223, 227, "/></"),
                new Classification("HTML Element Name", 227, 229, "td"),
                new Classification("HTML Tag Delimiter", 229, 230, ">"),
                new Classification("HTML Tag Delimiter", 240, 242, "</"),
                new Classification("HTML Element Name", 242, 244, "tr"),
                new Classification("HTML Tag Delimiter", 244, 245, ">"),
                new Classification("HTML Tag Delimiter", 255, 256, "<"),
                new Classification("HTML Element Name", 256, 258, "tr"),
                new Classification("HTML Tag Delimiter", 258, 259, ">"),
                new Classification("HTML Tag Delimiter", 273, 274, "<"),
                new Classification("HTML Element Name", 274, 276, "td"),
                new Classification("HTML Tag Delimiter", 276, 277, ">"),
                new Classification("HTML Tag Delimiter", 286, 288, "</"),
                new Classification("HTML Element Name", 288, 290, "td"),
                new Classification("HTML Tag Delimiter", 290, 291, ">"),
                new Classification("Django template tag", 305, 307, "{%"),
                new Classification("keyword", 308, 310, "if"),
                new Classification("identifier", 311, 323, "task.rolling"),
                new Classification("Django template tag", 324, 326, "%}"),
                new Classification("HTML Tag Delimiter", 340, 341, "<"),
                new Classification("HTML Element Name", 341, 343, "td"),
                new Classification("HTML Tag Delimiter", 343, 345, "><"),
                new Classification("HTML Element Name", 345, 350, "input"),
                new Classification("HTML Attribute Name", 351, 358, "checked"),
                new Classification("HTML Operator", 358, 359, "="),
                new Classification("HTML Attribute Value", 359, 368, "\"checked\""),
                new Classification("HTML Attribute Name", 369, 373, "name"),
                new Classification("HTML Operator", 373, 374, "="),
                new Classification("HTML Attribute Value", 374, 383, "\"rolling\""),
                new Classification("HTML Attribute Name", 384, 388, "type"),
                new Classification("HTML Operator", 388, 389, "="),
                new Classification("HTML Attribute Value", 389, 396, "\"radio\""),
                new Classification("HTML Tag Delimiter", 397, 399, "/>"),
                new Classification("HTML Tag Delimiter", 416, 417, "<"),
                new Classification("HTML Element Name", 417, 422, "input"),
                new Classification("HTML Attribute Name", 423, 427, "name"),
                new Classification("HTML Operator", 427, 428, "="),
                new Classification("HTML Attribute Value", 428, 437, "\"rolling\""),
                new Classification("HTML Attribute Name", 438, 442, "type"),
                new Classification("HTML Operator", 442, 443, "="),
                new Classification("HTML Attribute Value", 443, 450, "\"radio\""),
                new Classification("HTML Tag Delimiter", 451, 453, "/>"),
                new Classification("HTML Tag Delimiter", 455, 457, "</"),
                new Classification("HTML Element Name", 457, 459, "td"),
                new Classification("HTML Tag Delimiter", 459, 460, ">"),
                new Classification("Django template tag", 474, 476, "{%"),
                new Classification("keyword", 477, 481, "else"),
                new Classification("Django template tag", 482, 484, "%}"),
                new Classification("HTML Tag Delimiter", 498, 499, "<"),
                new Classification("HTML Element Name", 499, 501, "td"),
                new Classification("HTML Tag Delimiter", 501, 503, "><"),
                new Classification("HTML Element Name", 503, 508, "input"),
                new Classification("HTML Attribute Name", 509, 516, "checked"),
                new Classification("HTML Operator", 516, 517, "="),
                new Classification("HTML Attribute Value", 517, 526, "\"checked\""),
                new Classification("HTML Attribute Name", 527, 531, "name"),
                new Classification("HTML Operator", 531, 532, "="),
                new Classification("HTML Attribute Value", 532, 541, "\"rolling\""),
                new Classification("HTML Attribute Name", 542, 546, "type"),
                new Classification("HTML Operator", 546, 547, "="),
                new Classification("HTML Attribute Value", 547, 554, "\"radio\""),
                new Classification("HTML Tag Delimiter", 555, 557, "/>"),
                new Classification("HTML Tag Delimiter", 574, 575, "<"),
                new Classification("HTML Element Name", 575, 580, "input"),
                new Classification("HTML Attribute Name", 581, 585, "name"),
                new Classification("HTML Operator", 585, 586, "="),
                new Classification("HTML Attribute Value", 586, 595, "\"rolling\""),
                new Classification("HTML Attribute Name", 596, 600, "type"),
                new Classification("HTML Operator", 600, 601, "="),
                new Classification("HTML Attribute Value", 601, 608, "\"radio\""),
                new Classification("HTML Tag Delimiter", 609, 611, "/>"),
                new Classification("HTML Tag Delimiter", 613, 615, "</"),
                new Classification("HTML Element Name", 615, 617, "td"),
                new Classification("HTML Tag Delimiter", 617, 618, ">"),
                new Classification("Django template tag", 632, 634, "{%"),
                new Classification("keyword", 635, 640, "endif"),
                new Classification("Django template tag", 641, 643, "%}"),
                new Classification("HTML Tag Delimiter", 653, 655, "</"),
                new Classification("HTML Element Name", 655, 657, "tr"),
                new Classification("HTML Tag Delimiter", 657, 658, ">"),
                new Classification("HTML Tag Delimiter", 668, 669, "<"),
                new Classification("HTML Element Name", 669, 671, "tr"),
                new Classification("HTML Tag Delimiter", 671, 672, ">"),
                new Classification("HTML Tag Delimiter", 686, 687, "<"),
                new Classification("HTML Element Name", 687, 689, "td"),
                new Classification("HTML Tag Delimiter", 689, 690, ">"),
                new Classification("HTML Tag Delimiter", 699, 701, "</"),
                new Classification("HTML Element Name", 701, 703, "td"),
                new Classification("HTML Tag Delimiter", 703, 704, ">"),
                new Classification("HTML Tag Delimiter", 718, 719, "<"),
                new Classification("HTML Element Name", 719, 721, "td"),
                new Classification("HTML Tag Delimiter", 721, 723, "><"),
                new Classification("HTML Element Name", 723, 728, "input"),
                new Classification("HTML Attribute Name", 729, 736, "checked"),
                new Classification("HTML Operator", 736, 737, "="),
                new Classification("HTML Attribute Value", 737, 746, "\"checked\""),
                new Classification("HTML Attribute Name", 747, 751, "name"),
                new Classification("HTML Operator", 751, 752, "="),
                new Classification("HTML Attribute Value", 752, 762, "\"interval\""),
                new Classification("HTML Attribute Name", 763, 767, "type"),
                new Classification("HTML Operator", 767, 768, "="),
                new Classification("HTML Attribute Value", 768, 775, "\"radio\""),
                new Classification("HTML Tag Delimiter", 776, 778, "/>"),
                new Classification("HTML Tag Delimiter", 800, 801, "<"),
                new Classification("HTML Element Name", 801, 806, "input"),
                new Classification("HTML Attribute Name", 807, 811, "name"),
                new Classification("HTML Operator", 811, 812, "="),
                new Classification("HTML Attribute Value", 812, 822, "\"interval\""),
                new Classification("HTML Attribute Name", 823, 827, "type"),
                new Classification("HTML Operator", 827, 828, "="),
                new Classification("HTML Attribute Value", 828, 835, "\"radio\""),
                new Classification("HTML Tag Delimiter", 836, 838, "/>"),
                new Classification("HTML Tag Delimiter", 846, 848, "</"),
                new Classification("HTML Element Name", 848, 850, "td"),
                new Classification("HTML Tag Delimiter", 850, 851, ">"),
                new Classification("HTML Tag Delimiter", 861, 863, "</"),
                new Classification("HTML Element Name", 863, 865, "tr"),
                new Classification("HTML Tag Delimiter", 865, 866, ">"),
                new Classification("HTML Tag Delimiter", 872, 874, "</"),
                new Classification("HTML Element Name", 874, 879, "table"),
                new Classification("HTML Tag Delimiter", 879, 880, ">"),
                new Classification("HTML Tag Delimiter", 882, 884, "</"),
                new Classification("HTML Element Name", 884, 888, "form"),
                new Classification("HTML Tag Delimiter", 888, 889, ">"),
                new Classification("HTML Tag Delimiter", 893, 894, "<"),
                new Classification("HTML Element Name", 894, 899, "table"),
                new Classification("HTML Attribute Name", 900, 905, "style"),
                new Classification("HTML Operator", 905, 906, "="),
                new Classification("HTML Attribute Value", 906, 907, "\""),
                new Classification("CSS Property Name", 907, 912, "width"),
                new Classification("CSS Property Value", 914, 918, "100%"),
                new Classification("HTML Attribute Value", 918, 919, "\""),
                new Classification("HTML Tag Delimiter", 919, 920, ">"),
                new Classification("HTML Tag Delimiter", 926, 927, "<"),
                new Classification("HTML Element Name", 927, 929, "tr"),
                new Classification("HTML Tag Delimiter", 929, 930, ">"),
                new Classification("HTML Tag Delimiter", 940, 941, "<"),
                new Classification("HTML Element Name", 941, 943, "td"),
                new Classification("HTML Tag Delimiter", 943, 944, ">"),
                new Classification("HTML Tag Delimiter", 953, 955, "</"),
                new Classification("HTML Element Name", 955, 957, "td"),
                new Classification("HTML Tag Delimiter", 957, 958, ">"),
                new Classification("HTML Tag Delimiter", 968, 969, "<"),
                new Classification("HTML Element Name", 969, 971, "td"),
                new Classification("HTML Tag Delimiter", 971, 972, ">"),
                new Classification("HTML Tag Delimiter", 981, 983, "</"),
                new Classification("HTML Element Name", 983, 985, "td"),
                new Classification("HTML Tag Delimiter", 985, 986, ">"),
                new Classification("HTML Tag Delimiter", 996, 997, "<"),
                new Classification("HTML Element Name", 997, 999, "td"),
                new Classification("HTML Tag Delimiter", 999, 1000, ">"),
                new Classification("HTML Tag Delimiter", 1009, 1011, "</"),
                new Classification("HTML Element Name", 1011, 1013, "td"),
                new Classification("HTML Tag Delimiter", 1013, 1014, ">"),
                new Classification("HTML Tag Delimiter", 1020, 1022, "</"),
                new Classification("HTML Element Name", 1022, 1024, "tr"),
                new Classification("HTML Tag Delimiter", 1024, 1025, ">"),
                new Classification("Django template tag", 1027, 1029, "{%"),
                new Classification("keyword", 1030, 1033, "for"),
                new Classification("identifier", 1034, 1042, "testcase"),
                new Classification("keyword", 1043, 1045, "in"),
                new Classification("identifier", 1046, 1055, "testcases"),
                new Classification("Django template tag", 1056, 1058, "%}"),
                new Classification("HTML Tag Delimiter", 1064, 1065, "<"),
                new Classification("HTML Element Name", 1065, 1067, "tr"),
                new Classification("HTML Tag Delimiter", 1067, 1068, ">"),
                new Classification("HTML Tag Delimiter", 1078, 1079, "<"),
                new Classification("HTML Element Name", 1079, 1081, "td"),
                new Classification("HTML Tag Delimiter", 1081, 1082, ">"),
                new Classification("Django template tag", 1082, 1084, "{{"),
                new Classification("identifier", 1085, 1093, "testcase"),
                new Classification("Python dot", 1093, 1094, "."),
                new Classification("identifier", 1094, 1098, "name"),
                new Classification("Django template tag", 1099, 1101, "}}"),
                new Classification("HTML Tag Delimiter", 1101, 1103, "</"),
                new Classification("HTML Element Name", 1103, 1105, "td"),
                new Classification("HTML Tag Delimiter", 1105, 1106, ">"),
                new Classification("HTML Tag Delimiter", 1116, 1117, "<"),
                new Classification("HTML Element Name", 1117, 1119, "td"),
                new Classification("HTML Tag Delimiter", 1119, 1120, ">"),
                new Classification("Django template tag", 1120, 1122, "{{"),
                new Classification("identifier", 1123, 1131, "testcase"),
                new Classification("Python dot", 1131, 1132, "."),
                new Classification("identifier", 1132, 1136, "path"),
                new Classification("Django template tag", 1137, 1139, "}}"),
                new Classification("HTML Tag Delimiter", 1139, 1141, "</"),
                new Classification("HTML Element Name", 1141, 1143, "td"),
                new Classification("HTML Tag Delimiter", 1143, 1144, ">"),
                new Classification("HTML Tag Delimiter", 1154, 1155, "<"),
                new Classification("HTML Element Name", 1155, 1157, "td"),
                new Classification("HTML Tag Delimiter", 1157, 1158, ">"),
                new Classification("Django template tag", 1158, 1160, "{{"),
                new Classification("identifier", 1161, 1169, "testcase"),
                new Classification("Python dot", 1169, 1170, "."),
                new Classification("identifier", 1170, 1178, "selected"),
                new Classification("Django template tag", 1179, 1181, "}}"),
                new Classification("HTML Tag Delimiter", 1181, 1183, "</"),
                new Classification("HTML Element Name", 1183, 1185, "td"),
                new Classification("HTML Tag Delimiter", 1185, 1186, ">"),
                new Classification("HTML Tag Delimiter", 1192, 1194, "</"),
                new Classification("HTML Element Name", 1194, 1196, "tr"),
                new Classification("HTML Tag Delimiter", 1196, 1197, ">"),
                new Classification("Django template tag", 1199, 1201, "{%"),
                new Classification("keyword", 1202, 1208, "endfor"),
                new Classification("Django template tag", 1209, 1211, "%}"),
                new Classification("HTML Tag Delimiter", 1215, 1217, "</"),
                new Classification("HTML Element Name", 1217, 1222, "table"),
                new Classification("HTML Tag Delimiter", 1222, 1223, ">"),
                new Classification("HTML Tag Delimiter", 1229, 1231, "</"),
                new Classification("HTML Element Name", 1231, 1235, "body"),
                new Classification("HTML Tag Delimiter", 1235, 1236, ">"),
                new Classification("HTML Tag Delimiter", 1238, 1240, "</"),
                new Classification("HTML Element Name", 1240, 1244, "html"),
                new Classification("HTML Tag Delimiter", 1244, 1245, ">")
            );
        }

        public void Deletion1(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            DeletionTest(app, "Deletion1.html.djt", 1, 2, 1,
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 12, 14, "}}")
            );

            DeletionTest(app, "Deletion1.html.djt", 1, 3, 1,
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 12, 14, "}}")
            );

            DeletionTest(app, "Deletion1.html.djt", 1, 4, 1,
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 12, 14, "}}")
            );
        }

        public void Paste1(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            PasteTest(app, "Paste1.html.djt", 1, 2, "{{fob}}", "{{bazz}}",
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 12, 14, "}}"),
                new Classification("HTML Tag Delimiter", 18, 19, "<"),
                new Classification("HTML Element Name", 19, 22, "fob"),
                new Classification("HTML Tag Delimiter", 22, 23, ">"),
                new Classification("Django template tag", 25, 27, "{{"),
                new Classification("identifier", 27, 31, "bazz"),
                new Classification("Django template tag", 31, 33, "}}")
            );
        }

        public void SelectAllMixed1(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            SelectAllAndDeleteTest(app, "SelectAllMixed1.html.djt");
        }

        public void SelectAllMixed2(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            SelectAllAndDeleteTest(app, "SelectAllMixed2.html.djt");
        }

        public void SelectAllMixed3(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            SelectAllAndDeleteTest(app, "SelectAllMixed3.html.djt");
        }

        public void SelectAllMixed4(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            SelectAllAndDeleteTest(app, "SelectAllMixed4.html.djt");
        }

        public void SelectAllTag(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            SelectAllAndDeleteTest(app, "SelectAllTag.html.djt");
        }

        public void SelectAllText(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            SelectAllAndDeleteTest(app, "SelectAllText.html.djt");
        }

        public void CutUndo(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            CutUndoTest(app, "CutUndo.html.djt",
                6, 1, "{% for x in oar %}",
                new Classification("HTML Tag Delimiter", 0, 1, "<"),
                new Classification("HTML Element Name", 1, 5, "html"),
                new Classification("HTML Tag Delimiter", 5, 6, ">"),
                new Classification("HTML Tag Delimiter", 8, 9, "<"),
                new Classification("HTML Element Name", 9, 13, "head"),
                new Classification("HTML Tag Delimiter", 13, 15, "><"),
                new Classification("HTML Element Name", 15, 20, "title"),
                new Classification("HTML Tag Delimiter", 20, 23, "></"),
                new Classification("HTML Element Name", 23, 28, "title"),
                new Classification("HTML Tag Delimiter", 28, 31, "></"),
                new Classification("HTML Element Name", 31, 35, "head"),
                new Classification("HTML Tag Delimiter", 35, 36, ">"),
                new Classification("HTML Tag Delimiter", 40, 41, "<"),
                new Classification("HTML Element Name", 41, 45, "body"),
                new Classification("HTML Tag Delimiter", 45, 46, ">"),
                new Classification("Django template tag", 50, 52, "{%"),
                new Classification("keyword", 53, 56, "for"),
                new Classification("identifier", 57, 58, "x"),
                new Classification("keyword", 59, 61, "in"),
                new Classification("identifier", 62, 65, "oar"),
                new Classification("Django template tag", 66, 68, "%}"),
                new Classification("Django template tag", 70, 72, "{{"),
                new Classification("identifier", 73, 80, "content"),
                new Classification("Django template tag", 81, 83, "}}"),
                new Classification("HTML Tag Delimiter", 87, 89, "</"),
                new Classification("HTML Element Name", 89, 93, "body"),
                new Classification("HTML Tag Delimiter", 93, 94, ">"),
                new Classification("HTML Tag Delimiter", 96, 98, "</"),
                new Classification("HTML Element Name", 98, 102, "html"),
                new Classification("HTML Tag Delimiter", 102, 103, ">")
            );
        }

        private static void SelectAllAndDeleteTest(VisualStudioApp app, string filename) {
            Window window;
            var item = OpenDjangoProjectItem(app, filename, out window);

            item.Invoke(() => {
                using (var edit = item.TextView.TextBuffer.CreateEdit()) {
                    edit.Delete(new Span(0, item.TextView.TextBuffer.CurrentSnapshot.Length));
                    edit.Apply();
                }
            });

            WaitForHtmlTreeUpdate(item.TextView);

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
            var classifier = item.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            Classification.Verify(spans);
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void DeletionTest(VisualStudioApp app, string filename, int line, int column, int deletionCount, params Classification[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(app, filename, out window);
            item.MoveCaret(line, column);
            for (int i = 0; i < deletionCount; i++) {
                Keyboard.Backspace();
            }

            WaitForHtmlTreeUpdate(item.TextView);

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
            var classifier = item.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            Classification.Verify(
                spans,
                expected
            );
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void PasteTest(VisualStudioApp app, string filename, int line, int column, string selectionText, string pasteText, params Classification[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(app, filename, out window);
            item.MoveCaret(line, column);

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;

            var selectionStart = snapshot.GetText().IndexOf(selectionText);
            item.Invoke(() => {
                item.TextView.Selection.Select(new SnapshotSpan(item.TextView.TextBuffer.CurrentSnapshot, new Span(selectionStart, selectionText.Length)), false);
                System.Windows.Clipboard.SetText(pasteText);
            });

            WaitForTextChange(item.TextView, () => {
                Keyboard.ControlV();
            });
            WaitForHtmlTreeUpdate(item.TextView);

            IList<ClassificationSpan> spans = null;
            item.Invoke(() => {
                snapshot = item.TextView.TextBuffer.CurrentSnapshot;
                var classifier = item.Classifier;
                spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            });
            Assert.IsNotNull(spans);
            Classification.Verify(
                spans,
                expected
            );
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void CutUndoTest(VisualStudioApp app, string filename, int line, int column, string selectionText, params Classification[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(app, filename, out window);
            item.MoveCaret(line, column);

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;

            var selectionStart = snapshot.GetText().IndexOf(selectionText);
            item.Invoke(() => {
                item.TextView.Selection.Select(new SnapshotSpan(item.TextView.TextBuffer.CurrentSnapshot, new Span(selectionStart, selectionText.Length)), false);
                Keyboard.ControlX();
            });

            WaitForTextChange(item.TextView, () => {
                Keyboard.ControlZ();
            });
            WaitForHtmlTreeUpdate(item.TextView);

            IList<ClassificationSpan> spans = null;
            item.Invoke(() => {
                snapshot = item.TextView.TextBuffer.CurrentSnapshot;
                var classifier = item.Classifier;
                spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            });
            Assert.IsNotNull(spans);
            Classification.Verify(
                spans,
                expected
            );
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void InsertionTest(VisualStudioApp app, string filename, int line, int column, string insertionText, params Classification[] expected) {
            InsertionTest(app, filename, line, column, -1, insertionText, false, true, true, expected);
        }

        private static void InsertionTest(VisualStudioApp app, string filename, int line, int column, int selectionLength, string insertionText, bool paste, params Classification[] expected) {
            InsertionTest(app, filename, line, column, selectionLength, insertionText, paste, true, true, expected);
        }

        private static void InsertionTest(VisualStudioApp app, string filename, int line, int column, int selectionLength, string insertionText, bool paste, bool checkInsertionMoved, bool checkInsertionLen, params Classification[] expected) {
            InsertionTest(app, filename, line, column, selectionLength, insertionText, paste, checkInsertionMoved, checkInsertionLen, @"TestData\DjangoEditProject.sln", false, expected);
        }

        private static bool SetBraceCompletion(VisualStudioApp app, bool value) {
            bool oldValue = false;
            ThreadHelper.JoinableTaskFactory.Run(async () => {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                HtmlSettings.InsertMatchingBraces = false;
                HtmlSettings.InsertEndTags = false;
            });
            return oldValue;
        }

        private class SetRestoreBraceCompletion : IDisposable {
            private readonly VisualStudioApp _app;
            private readonly bool _oldValue;

            public SetRestoreBraceCompletion(VisualStudioApp app, bool newValue) {
                _app = app;
                _oldValue = SetBraceCompletion(app, newValue);
            }

            public void Dispose() {
                SetBraceCompletion(_app, _oldValue);
            }
        }

        private static IDisposable WithoutBraceCompletion(VisualStudioApp app) {
            return new SetRestoreBraceCompletion(app, false);
        }

        private static void InsertionTest(
            VisualStudioApp app,
            string filename,
            int line,
            int column,
            int selectionLength,
            string insertionText,
            bool paste,
            bool checkInsertionMoved,
            bool checkInsertionLen,
            string projectName,
            bool wait,
            params Classification[] expected
        ) {
            using (WithoutBraceCompletion(app)) {
                Window window;
                var item = OpenDjangoProjectItem(app, filename, out window, projectName, wait);

                item.MoveCaret(line, column);
                var pos = item.TextView.Caret.Position.BufferPosition.Position;
                if (selectionLength != -1) {
                    item.Select(line, column, selectionLength);
                }

                if (!String.IsNullOrEmpty(insertionText)) {
                    if (paste) {
                        item.Invoke(() => {
                            System.Windows.Clipboard.SetText(insertionText);
                            var editorOps = app.ComponentModel.GetService<IEditorOperationsFactoryService>();
                            Assert.IsTrue(editorOps.GetEditorOperations(item.TextView).Paste());
                        });
                    } else {
                        Keyboard.Type(insertionText);
                    }

                    var newPos = item.TextView.Caret.Position.BufferPosition;

                    if (checkInsertionMoved) {
                        Assert.AreNotEqual(pos, newPos);
                    }
                    if (checkInsertionLen) {
                        Assert.AreEqual(pos + insertionText.Length, newPos.Position);
                    }
                }

                WaitForHtmlTreeUpdate(item.TextView);

                IList<ClassificationSpan> spans = null;
                item.Invoke(() => {
                    var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
                    var classifier = item.Classifier;
                    spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
                    // HtmlTemplatesClassifier treats {{ and }} as client-side Mustache templates, and classifies them and their
                    // content  accordingly with somewhat inconsistent results, which throws our tests off. Filter out those
                    // classifications entirely to avoid the problem.
                    spans = spans.Where(span => !span.ClassificationType.IsOfType("HtmlClientTemplateValue")).ToList();
                });

                Assert.IsNotNull(spans);
                Classification.Verify(
                    spans,
                    expected
                );

                window.Close(vsSaveChanges.vsSaveChangesNo);
            }
        }

        private static void InsertionDeletionTest(VisualStudioApp app, string filename, int line, int column, string insertionText, Classification[] expectedFirst, Classification[] expectedAfter) {
            using (WithoutBraceCompletion(app)) {
                Window window;
                var item = OpenDjangoProjectItem(app, filename, out window);
                item.MoveCaret(line, column);

                WaitForTextChange(item.TextView, () => {
                    Keyboard.Type(insertionText);
                });
                WaitForHtmlTreeUpdate(item.TextView);

                var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
                var classifier = item.Classifier;
                IList<ClassificationSpan> spans = null;
                item.Invoke(() => {
                    spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
                });
                Assert.IsNotNull(spans);
                Classification.Verify(
                    spans,
                    expectedFirst
                );

                WaitForTextChange(item.TextView, () => {
                    for (int i = 0; i < insertionText.Length; i++) {
                        Keyboard.Backspace();
                    }
                });
                WaitForHtmlTreeUpdate(item.TextView);

                item.Invoke(() => {
                    snapshot = item.TextView.TextBuffer.CurrentSnapshot;
                    spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
                });

                Classification.Verify(
                    spans,
                    expectedAfter
                );

                window.Close(vsSaveChanges.vsSaveChangesNo);
            }
        }

        public void IntellisenseCompletions(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Intellisense.html.djt", 6, 3, -1, " end\r",
                paste: false,
                checkInsertionMoved: true,
                checkInsertionLen: false,
                expected: new[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 40, 41, "<"),
                    new Classification("HTML Element Name", 41, 45, "body"),
                    new Classification("HTML Tag Delimiter", 45, 46, ">"),
                    new Classification("Django template tag", 48, 50, "{%"),
                    new Classification("keyword", 51, 54, "for"),
                    new Classification("identifier", 55, 56, "x"),
                    new Classification("keyword", 57, 59, "in"),
                    new Classification("identifier", 60, 63, "fob"),
                    new Classification("Django template tag", 64, 66, "%}"),
                    new Classification("Django template tag", 68, 70, "{%"),
                    new Classification("keyword", 71, 77, "endfor"),
                    new Classification("Django template tag", 78, 80, "%}"),
                    new Classification("HTML Tag Delimiter", 84, 86, "</"),
                    new Classification("HTML Element Name", 86, 90, "body"),
                    new Classification("HTML Tag Delimiter", 90, 91, ">"),
                    new Classification("HTML Tag Delimiter", 93, 95, "</"),
                    new Classification("HTML Element Name", 95, 99, "html"),
                    new Classification("HTML Tag Delimiter", 99, 100, ">")
                }
            );
        }

        public void IntellisenseCompletions2(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Intellisense2.html.djt", 6, 1, -1, "{{" + Keyboard.OneSecondDelay + " o\t }}",
                paste: false,
                checkInsertionMoved: true,
                checkInsertionLen: false,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 40, 41, "<"),
                    new Classification("HTML Element Name", 41, 45, "body"),
                    new Classification("HTML Tag Delimiter", 45, 46, ">"),
                    new Classification("Django template tag", 48, 50, "{%"),
                    new Classification("keyword", 51, 54, "for"),
                    new Classification("identifier", 55, 58, "oar"),
                    new Classification("keyword", 59, 61, "in"),
                    new Classification("identifier", 62, 65, "fob"),
                    new Classification("Django template tag", 66, 68, "%}"),
                    new Classification("Django template tag", 70, 72, "{{"),
                    new Classification("identifier", 73, 76, "oar"),
                    new Classification("Django template tag", 77, 79, "}}"),
                    new Classification("Django template tag", 81, 83, "{%"),
                    new Classification("keyword", 84, 90, "endfor"),
                    new Classification("Django template tag", 91, 93, "%}"),
                    new Classification("HTML Tag Delimiter", 97, 99, "</"),
                    new Classification("HTML Element Name", 99, 103, "body"),
                    new Classification("HTML Tag Delimiter", 103, 104, ">"),
                    new Classification("HTML Tag Delimiter", 106, 108, "</"),
                    new Classification("HTML Element Name", 108, 112, "html"),
                    new Classification("HTML Tag Delimiter", 112, 113, ">")
                }
            );
        }

        public void IntellisenseCompletions4(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "TestApp\\Templates\\page.html.djt", 6, 11, -1, "|cu\t",
                paste: false,
                checkInsertionMoved: true,
                checkInsertionLen: false,
                projectName: @"TestData\DjangoTemplateCodeIntelligence.sln",
                wait: true,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 40, 41, "<"),
                    new Classification("HTML Element Name", 41, 45, "body"),
                    new Classification("HTML Tag Delimiter", 45, 46, ">"),
                    new Classification("Django template tag", 50, 52, "{{"),
                    new Classification("identifier", 53, 60, "content"),
                    new Classification("identifier", 61, 64, "cut"),
                    new Classification("Django template tag", 65, 67, "}}"),
                    new Classification("HTML Tag Delimiter", 71, 73, "</"),
                    new Classification("HTML Element Name", 73, 77, "body"),
                    new Classification("HTML Tag Delimiter", 77, 78, ">"),
                    new Classification("HTML Tag Delimiter", 80, 82, "</"),
                    new Classification("HTML Element Name", 82, 86, "html"),
                    new Classification("HTML Tag Delimiter", 86, 87, ">")
                }
            );
        }

        public void IntellisenseCompletions5(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "TestApp\\Templates\\page.html.djt", 6, 11, -1, ".c\t",
                paste: false,
                checkInsertionMoved: true,
                checkInsertionLen: false,
                projectName: @"TestData\DjangoTemplateCodeIntelligence.sln",
                wait: true,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 40, 41, "<"),
                    new Classification("HTML Element Name", 41, 45, "body"),
                    new Classification("HTML Tag Delimiter", 45, 46, ">"),
                    new Classification("Django template tag", 50, 52, "{{"),
                    new Classification("identifier", 53, 60, "content"),
                    new Classification("Python dot", 60, 61, "."),
                    new Classification("identifier", 61, 70, "conjugate"),
                    new Classification("Django template tag", 71, 73, "}}"),
                    new Classification("HTML Tag Delimiter", 77, 79, "</"),
                    new Classification("HTML Element Name", 79, 83, "body"),
                    new Classification("HTML Tag Delimiter", 83, 84, ">"),
                    new Classification("HTML Tag Delimiter", 86, 88, "</"),
                    new Classification("HTML Element Name", 88, 92, "html"),
                    new Classification("HTML Tag Delimiter", 92, 93, ">")
                }
            );
        }

        public void IntellisenseCompletions6(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "TestApp\\Templates\\page.html.djt", 7, 1, -1, "{%" + Keyboard.OneSecondDelay + " auto\t o\t %}",
                paste: false,
                checkInsertionMoved: true,
                checkInsertionLen: false,
                projectName: @"TestData\DjangoTemplateCodeIntelligence.sln",
                wait: true,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 40, 41, "<"),
                    new Classification("HTML Element Name", 41, 45, "body"),
                    new Classification("HTML Tag Delimiter", 45, 46, ">"),
                    new Classification("Django template tag", 50, 52, "{{"),
                    new Classification("identifier", 53, 60, "content"),
                    new Classification("Django template tag", 61, 63, "}}"),
                    new Classification("Django template tag", 65, 67, "{%"),
                    new Classification("keyword", 68, 78, "autoescape"),
                    new Classification("keyword", 79, 82, "off"),
                    new Classification("Django template tag", 83, 85, "%}"),
                    new Classification("HTML Tag Delimiter", 87, 89, "</"),
                    new Classification("HTML Element Name", 89, 93, "body"),
                    new Classification("HTML Tag Delimiter", 93, 94, ">"),
                    new Classification("HTML Tag Delimiter", 96, 98, "</"),
                    new Classification("HTML Element Name", 98, 102, "html"),
                    new Classification("HTML Tag Delimiter", 102, 103, ">")
                }
            );
        }

        public void IntellisenseCompletions7(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "TestApp\\Templates\\page4.html.djt", 6, 8, -1, Keyboard.CtrlSpace.ToString(),
                paste: false,
                checkInsertionMoved: false,
                checkInsertionLen: false,
                projectName: @"TestData\DjangoTemplateCodeIntelligence.sln",
                wait: true,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 40, 41, "<"),
                    new Classification("HTML Element Name", 41, 45, "body"),
                    new Classification("HTML Tag Delimiter", 45, 46, ">"),
                    new Classification("Django template tag", 50, 52, "{{"),
                    new Classification("identifier", 53, 60, "content"),
                    new Classification("Django template tag", 61, 63, "}}"),
                    new Classification("HTML Tag Delimiter", 67, 69, "</"),
                    new Classification("HTML Element Name", 69, 73, "body"),
                    new Classification("HTML Tag Delimiter", 73, 74, ">"),
                    new Classification("HTML Tag Delimiter", 76, 78, "</"),
                    new Classification("HTML Element Name", 78, 82, "html"),
                    new Classification("HTML Tag Delimiter", 82, 83, ">")
                }
            );
        }

        public void IntellisenseCompletions8(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "TestApp\\Templates\\page2.html.djt", 7, 8, -1, Keyboard.CtrlSpace.ToString(),
                paste: false,
                checkInsertionMoved: false,
                checkInsertionLen: false,
                projectName: @"TestData\DjangoTemplateCodeIntelligence.sln",
                wait: true,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 40, 41, "<"),
                    new Classification("HTML Element Name", 41, 45, "body"),
                    new Classification("HTML Tag Delimiter", 45, 46, ">"),
                    new Classification("Django template tag", 50, 52, "{%"),
                    new Classification("keyword", 53, 63, "autoescape"),
                    new Classification("keyword", 64, 66, "on"),
                    new Classification("Django template tag", 67, 69, "%}"),
                    new Classification("Django template tag", 71, 73, "{%"),
                    new Classification("keyword", 74, 84, "autoescape"),
                    new Classification("keyword", 85, 87, "on"),
                    new Classification("Django template tag", 88, 90, "%}"),
                    new Classification("Django template tag", 92, 94, "{{"),
                    new Classification("Django template tag", 95, 97, "}}"),
                    new Classification("HTML Tag Delimiter", 101, 103, "</"),
                    new Classification("HTML Element Name", 103, 107, "body"),
                    new Classification("HTML Tag Delimiter", 107, 108, ">"),
                    new Classification("HTML Tag Delimiter", 110, 112, "</"),
                    new Classification("HTML Element Name", 112, 116, "html"),
                    new Classification("HTML Tag Delimiter", 116, 117, ">")
                }
            );
        }

        public void IntellisenseCompletions9(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            string keySequence = "c" + Keyboard.CtrlSpace.ToString();
            InsertionTest(app, "TestApp\\Templates\\page2.html.djt", 8, 4, -1, keySequence,
                paste: false,
                checkInsertionMoved: true,
                checkInsertionLen: false,
                projectName: @"TestData\DjangoTemplateCodeIntelligence.sln",
                wait: true,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 40, 41, "<"),
                    new Classification("HTML Element Name", 41, 45, "body"),
                    new Classification("HTML Tag Delimiter", 45, 46, ">"),
                    new Classification("Django template tag", 50, 52, "{%"),
                    new Classification("keyword", 53, 63, "autoescape"),
                    new Classification("keyword", 64, 66, "on"),
                    new Classification("Django template tag", 67, 69, "%}"),
                    new Classification("Django template tag", 71, 73, "{%"),
                    new Classification("keyword", 74, 78, "auto"),
                    new Classification("excluded code", 78, 81, " on"),
                    new Classification("Django template tag", 82, 84, "%}"),
                    new Classification("Django template tag", 86, 88, "{{"),
                    new Classification("identifier", 89, 96, "content"),
                    new Classification("Django template tag", 96, 98, "}}"),
                    new Classification("HTML Tag Delimiter", 102, 104, "</"),
                    new Classification("HTML Element Name", 104, 108, "body"),
                    new Classification("HTML Tag Delimiter", 108, 109, ">"),
                    new Classification("HTML Tag Delimiter", 111, 113, "</"),
                    new Classification("HTML Element Name", 113, 117, "html"),
                    new Classification("HTML Tag Delimiter", 117, 118, ">")
                }
            );
        }

        public void IntellisenseCompletions10(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "TestApp\\Templates\\page3.html.djt", 6, 1, -1, Keyboard.CtrlSpace + "{%" + Keyboard.OneSecondDelay + " fo\t fob in con\t \t %}",
                paste: false,
                checkInsertionMoved: true,
                checkInsertionLen: false,
                projectName: @"TestData\DjangoTemplateCodeIntelligence.sln",
                wait: true,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 40, 41, "<"),
                    new Classification("HTML Element Name", 41, 45, "body"),
                    new Classification("HTML Tag Delimiter", 45, 46, ">"),
                    new Classification("Django template tag", 50, 52, "{%"),
                    new Classification("keyword", 53, 56, "for"),
                    new Classification("identifier", 57, 60, "fob"),
                    new Classification("keyword", 61, 63, "in"),
                    new Classification("identifier", 64, 71, "content"),
                    new Classification("keyword", 72, 80, "reversed"),
                    new Classification("Django template tag", 81, 83, "%}"),
                    new Classification("Django template tag", 85, 87, "{{"),
                    new Classification("Django template tag", 88, 90, "}}"),
                    new Classification("HTML Tag Delimiter", 94, 96, "</"),
                    new Classification("HTML Element Name", 96, 100, "body"),
                    new Classification("HTML Tag Delimiter", 100, 101, ">"),
                    new Classification("HTML Tag Delimiter", 103, 105, "</"),
                    new Classification("HTML Element Name", 105, 109, "html"),
                    new Classification("HTML Tag Delimiter", 109, 110, ">")
                }
            );
        }

        public void IntellisenseCompletions11(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "TestApp\\Templates\\page.html.djt", 3, 1, -1, "<\b\t",
                paste: false,
                checkInsertionMoved: true,
                checkInsertionLen: false,
                projectName: @"TestData\DjangoTemplateCodeIntelligence.sln",
                wait: false,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 44, 45, "<"),
                    new Classification("HTML Element Name", 45, 49, "body"),
                    new Classification("HTML Tag Delimiter", 49, 50, ">"),
                    new Classification("Django template tag", 54, 56, "{{"),
                    new Classification("identifier", 57, 64, "content"),
                    new Classification("Django template tag", 65, 67, "}}"),
                    new Classification("HTML Tag Delimiter", 71, 73, "</"),
                    new Classification("HTML Element Name", 73, 77, "body"),
                    new Classification("HTML Tag Delimiter", 77, 78, ">"),
                    new Classification("HTML Tag Delimiter", 80, 82, "</"),
                    new Classification("HTML Element Name", 82, 86, "html"),
                    new Classification("HTML Tag Delimiter", 86, 87, ">")
                }
            );
        }

        public void IntellisenseCompletions12(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "TestApp\\Templates\\page5.html.djt", 6, 8, -1, Keyboard.CtrlSpace.ToString(),
                paste: false,
                checkInsertionMoved: false,
                checkInsertionLen: false,
                projectName: @"TestData\DjangoTemplateCodeIntelligence.sln",
                wait: true,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 40, 41, "<"),
                    new Classification("HTML Element Name", 41, 45, "body"),
                    new Classification("HTML Tag Delimiter", 45, 46, ">"),
                    new Classification("Django template tag", 50, 52, "{{"),
                    new Classification("identifier", 53, 60, "content"),
                    new Classification("Django template tag", 61, 63, "}}"),
                    new Classification("HTML Tag Delimiter", 67, 69, "</"),
                    new Classification("HTML Element Name", 69, 73, "body"),
                    new Classification("HTML Tag Delimiter", 73, 74, ">"),
                    new Classification("HTML Tag Delimiter", 76, 78, "</"),
                    new Classification("HTML Element Name", 78, 82, "html"),
                    new Classification("HTML Tag Delimiter", 82, 83, ">")
                }
            );
        }

        public void IntellisenseCompletionsHtml(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "Intellisense3.html.djt", 4, 1, -1, "<bo>",
                paste: false,
                checkInsertionMoved: true,
                checkInsertionLen: false,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 15, "><"),
                    new Classification("HTML Element Name", 15, 20, "title"),
                    new Classification("HTML Tag Delimiter", 20, 23, "></"),
                    new Classification("HTML Element Name", 23, 28, "title"),
                    new Classification("HTML Tag Delimiter", 28, 31, "></"),
                    new Classification("HTML Element Name", 31, 35, "head"),
                    new Classification("HTML Tag Delimiter", 35, 36, ">"),
                    new Classification("HTML Tag Delimiter", 40, 41, "<"),
                    new Classification("HTML Element Name", 41, 45, "body"),
                    new Classification("HTML Tag Delimiter", 45, 46, ">"),
                    new Classification("Django template tag", 48, 50, "{%"),
                    new Classification("keyword", 51, 54, "for"),
                    new Classification("identifier", 55, 58, "oar"),
                    new Classification("keyword", 59, 61, "in"),
                    new Classification("identifier", 62, 65, "fob"),
                    new Classification("Django template tag", 66, 68, "%}"),
                    new Classification("Django template tag", 72, 74, "{%"),
                    new Classification("keyword", 75, 81, "endfor"),
                    new Classification("Django template tag", 82, 84, "%}"),
                    new Classification("HTML Tag Delimiter", 88, 90, "</"),
                    new Classification("HTML Element Name", 90, 94, "body"),
                    new Classification("HTML Tag Delimiter", 94, 95, ">"),
                    new Classification("HTML Tag Delimiter", 97, 99, "</"),
                    new Classification("HTML Element Name", 99, 103, "html"),
                    new Classification("HTML Tag Delimiter", 103, 104, ">")
                }
            );
        }

        public void IntellisenseCompletionsCss(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "IntellisenseCssJs.html.djt", 3, 36, -1, Keyboard.CtrlSpace.ToString(),
                paste: false,
                checkInsertionMoved: false,
                checkInsertionLen: false,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 14, ">"),
                    new Classification("HTML Tag Delimiter", 16, 17, "<"),
                    new Classification("HTML Element Name", 17, 22, "style"),
                    new Classification("HTML Attribute Name", 23, 27, "type"),
                    new Classification("HTML Operator", 27, 28, "="),
                    new Classification("HTML Attribute Value", 28, 38, "\"text/css\""),
                    new Classification("HTML Tag Delimiter", 38, 39, ">"),
                    new Classification("CSS Selector", 39, 40, "*"),
                    new Classification("CSS Property Name", 43, 54, "font-family"),
                    new Classification("HTML Tag Delimiter", 56, 58, "</"),
                    new Classification("HTML Element Name", 58, 63, "style"),
                    new Classification("HTML Tag Delimiter", 63, 64, ">"),
                    new Classification("HTML Tag Delimiter", 66, 67, "<"),
                    new Classification("HTML Element Name", 67, 73, "script"),
                    new Classification("HTML Attribute Name", 74, 78, "type"),
                    new Classification("HTML Operator", 78, 79, "="),
                    new Classification("HTML Attribute Value", 79, 96, "\"text/javascript\""),
                    new Classification("HTML Tag Delimiter", 96, 97, ">"),
                    new Classification("identifier", 97, 100, "thr"),
                    new Classification("HTML Tag Delimiter", 100, 102, "</"),
                    new Classification("HTML Element Name", 102, 108, "script"),
                    new Classification("HTML Tag Delimiter", 108, 109, ">"),
                    new Classification("HTML Tag Delimiter", 111, 113, "</"),
                    new Classification("HTML Element Name", 113, 117, "head"),
                    new Classification("HTML Tag Delimiter", 117, 118, ">"),
                    new Classification("HTML Tag Delimiter", 120, 121, "<"),
                    new Classification("HTML Element Name", 121, 125, "body"),
                    new Classification("HTML Tag Delimiter", 125, 126, ">"),
                    new Classification("Django template tag", 126, 128, "{{"),
                    new Classification("identifier", 129, 136, "content"),
                    new Classification("Django template tag", 137, 139, "}}"),
                    new Classification("HTML Tag Delimiter", 139, 141, "</"),
                    new Classification("HTML Element Name", 141, 145, "body"),
                    new Classification("HTML Tag Delimiter", 145, 146, ">"),
                    new Classification("HTML Tag Delimiter", 148, 150, "</"),
                    new Classification("HTML Element Name", 150, 154, "html"),
                    new Classification("HTML Tag Delimiter", 154, 155, ">")
                }
            );
        }

        public void IntellisenseCompletionsJS(VisualStudioApp app, DjangoInterpreterSetter interpreterSetter) {
            InsertionTest(app, "IntellisenseCssJs.html.djt", 4, 35, -1, Keyboard.CtrlSpace.ToString(),
                paste: false,
                checkInsertionMoved: false,
                checkInsertionLen: false,
                expected: new Classification[] {
                    new Classification("HTML Tag Delimiter", 0, 1, "<"),
                    new Classification("HTML Element Name", 1, 5, "html"),
                    new Classification("HTML Tag Delimiter", 5, 6, ">"),
                    new Classification("HTML Tag Delimiter", 8, 9, "<"),
                    new Classification("HTML Element Name", 9, 13, "head"),
                    new Classification("HTML Tag Delimiter", 13, 14, ">"),
                    new Classification("HTML Tag Delimiter", 16, 17, "<"),
                    new Classification("HTML Element Name", 17, 22, "style"),
                    new Classification("HTML Attribute Name", 23, 27, "type"),
                    new Classification("HTML Operator", 27, 28, "="),
                    new Classification("HTML Attribute Value", 28, 38, "\"text/css\""),
                    new Classification("HTML Tag Delimiter", 38, 39, ">"),
                    new Classification("CSS Selector", 39, 40, "*"),
                    new Classification("CSS Property Name", 43, 51, "font-fam"),
                    new Classification("HTML Tag Delimiter", 53, 55, "</"),
                    new Classification("HTML Element Name", 55, 60, "style"),
                    new Classification("HTML Tag Delimiter", 60, 61, ">"),
                    new Classification("HTML Tag Delimiter", 63, 64, "<"),
                    new Classification("HTML Element Name", 64, 70, "script"),
                    new Classification("HTML Attribute Name", 71, 75, "type"),
                    new Classification("HTML Operator", 75, 76, "="),
                    new Classification("HTML Attribute Value", 76, 93, "\"text/javascript\""),
                    new Classification("HTML Tag Delimiter", 93, 94, ">"),
                    new Classification("keyword", 94, 99, "throw"),
                    new Classification("HTML Tag Delimiter", 99, 101, "</"),
                    new Classification("HTML Element Name", 101, 107, "script"),
                    new Classification("HTML Tag Delimiter", 107, 108, ">"),
                    new Classification("HTML Tag Delimiter", 110, 112, "</"),
                    new Classification("HTML Element Name", 112, 116, "head"),
                    new Classification("HTML Tag Delimiter", 116, 117, ">"),
                    new Classification("HTML Tag Delimiter", 119, 120, "<"),
                    new Classification("HTML Element Name", 120, 124, "body"),
                    new Classification("HTML Tag Delimiter", 124, 125, ">"),
                    new Classification("Django template tag", 125, 127, "{{"),
                    new Classification("identifier", 128, 135, "content"),
                    new Classification("Django template tag", 136, 138, "}}"),
                    new Classification("HTML Tag Delimiter", 138, 140, "</"),
                    new Classification("HTML Element Name", 140, 144, "body"),
                    new Classification("HTML Tag Delimiter", 144, 145, ">"),
                    new Classification("HTML Tag Delimiter", 147, 149, "</"),
                    new Classification("HTML Element Name", 149, 153, "html"),
                    new Classification("HTML Tag Delimiter", 153, 154, ">")
                }
            );
        }

        private static EditorWindow OpenDjangoProjectItem(VisualStudioApp app, string startItem, out Window window, string projectName = @"TestData\DjangoEditProject.sln", bool wait = false) {
            var sln = app.CopyProjectForTest(projectName);
            var project = app.OpenProject(sln, startItem);
            var pyProj = project.GetPythonProject();

            EnvDTE.ProjectItem item = null;
            if (startItem.IndexOf('\\') != -1) {
                var items = project.ProjectItems;
                foreach (var itemName in startItem.Split('\\')) {
                    Console.WriteLine(itemName);
                    item = items.Item(itemName);
                    items = item.ProjectItems;
                }
            } else {
                item = project.ProjectItems.Item(startItem);
            }

            Assert.IsNotNull(item);

            window = item.Open();
            window.Activate();
            var doc = app.GetDocument(item.Document.FullName);

            if (wait) {
                pyProj.GetAnalyzer().WaitForCompleteAnalysis(_ => true);
            }

            return doc;
        }
    }
}
