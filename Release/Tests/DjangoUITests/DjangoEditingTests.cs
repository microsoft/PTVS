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
using System.Threading;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using TestUtilities;
using TestUtilities.UI;

namespace DjangoUITests {
    [TestClass]
    public class DjangoEditingTests {
        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Classifications() {
            InsertionTest("Classification.html.djt", 8, 10, "",
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
                new Classification("Django template tag", 67, 69, "%}"),
                new Classification("Django template tag", 72, 74, "{%"),
                new Classification("keyword", 75, 85, "autoescape"),
                new Classification("Django template tag", 90, 92, "%}"),
                new Classification("Django template tag", 95, 97, "{%"),
                new Classification("keyword", 98, 108, "autoescape"),
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
                new Classification("identifier", 658, 661, "foo"),
                new Classification("Django template tag", 662, 664, "%}"),
                new Classification("Django template tag", 667, 669, "{%"),
                new Classification("keyword", 670, 675, "endif"),
                new Classification("Django template tag", 676, 678, "%}"),
                new Classification("Django template tag", 683, 685, "{%"),
                new Classification("keyword", 686, 688, "if"),
                new Classification("identifier", 689, 692, "foo"),
                new Classification("Django template tag", 693, 695, "%}"),
                new Classification("Django template tag", 698, 700, "{%"),
                new Classification("keyword", 701, 705, "else"),
                new Classification("Django template tag", 706, 708, "%}"),
                new Classification("Django template tag", 711, 713, "{%"),
                new Classification("keyword", 714, 719, "endif"),
                new Classification("Django template tag", 720, 722, "%}"),
                new Classification("Django template tag", 727, 729, "{%"),
                new Classification("keyword", 730, 733, "for"),
                new Classification("identifier", 734, 735, "x"),
                new Classification("keyword", 736, 738, "in"),
                new Classification("identifier", 739, 742, "abc"),
                new Classification("Django template tag", 743, 745, "%}"),
                new Classification("Django template tag", 748, 750, "{%"),
                new Classification("keyword", 751, 757, "endfor"),
                new Classification("Django template tag", 758, 760, "%}"),
                new Classification("Django template tag", 765, 767, "{%"),
                new Classification("keyword", 768, 771, "for"),
                new Classification("identifier", 772, 773, "x"),
                new Classification("keyword", 774, 776, "in"),
                new Classification("identifier", 777, 780, "abc"),
                new Classification("keyword", 781, 789, "reversed"),
                new Classification("Django template tag", 790, 792, "%}"),
                new Classification("Django template tag", 795, 797, "{%"),
                new Classification("keyword", 798, 804, "endfor"),
                new Classification("Django template tag", 805, 807, "%}"),
                new Classification("Django template tag", 812, 814, "{%"),
                new Classification("keyword", 815, 818, "for"),
                new Classification("identifier", 820, 821, "x"),
                new Classification("keyword", 823, 825, "in"),
                new Classification("identifier", 827, 830, "abc"),
                new Classification("keyword", 832, 840, "reversed"),
                new Classification("Django template tag", 841, 843, "%}"),
                new Classification("Django template tag", 846, 848, "{%"),
                new Classification("keyword", 849, 855, "endfor"),
                new Classification("Django template tag", 856, 858, "%}"),
                new Classification("Django template tag", 863, 865, "{%"),
                new Classification("keyword", 866, 870, "load"),
                new Classification("Django template tag", 880, 882, "%}"),
                new Classification("Django template tag", 885, 887, "{%"),
                new Classification("keyword", 888, 892, "load"),
                new Classification("Django template tag", 906, 908, "%}"),
                new Classification("Django template tag", 913, 915, "{%"),
                new Classification("keyword", 916, 919, "now"),
                new Classification("excluded code", 919, 925, " 'Y H'"),
                new Classification("Django template tag", 926, 928, "%}"),
                new Classification("Django template tag", 933, 935, "{%"),
                new Classification("keyword", 936, 943, "regroup"),
                new Classification("excluded code", 943, 971, " people by gender as grouped"),
                new Classification("Django template tag", 972, 974, "%}"),
                new Classification("Django template tag", 979, 981, "{%"),
                new Classification("keyword", 982, 991, "spaceless"),
                new Classification("Django template tag", 992, 994, "%}"),
                new Classification("HTML Tag Delimiter", 998, 999, "<"),
                new Classification("HTML Element Name", 999, 1000, "p"),
                new Classification("HTML Tag Delimiter", 1000, 1001, ">"),
                new Classification("HTML Tag Delimiter", 1005, 1007, "</"),
                new Classification("HTML Element Name", 1007, 1008, "p"),
                new Classification("HTML Tag Delimiter", 1008, 1009, ">"),
                new Classification("Django template tag", 1012, 1014, "{%"),
                new Classification("keyword", 1015, 1027, "endspaceless"),
                new Classification("Django template tag", 1028, 1030, "%}"),
                new Classification("Django template tag", 1035, 1037, "{%"),
                new Classification("keyword", 1038, 1041, "ssi"),
                new Classification("excluded code", 1041, 1055, " /home/foo.txt"),
                new Classification("Django template tag", 1056, 1058, "%}"),
                new Classification("Django template tag", 1065, 1067, "{%"),
                new Classification("keyword", 1068, 1080, "unknownblock"),
                new Classification("Django template tag", 1081, 1083, "%}"),
                new Classification("Django template tag", 1089, 1091, "{%"),
                new Classification("keyword", 1092, 1103, "templatetag"),
                new Classification("keyword", 1104, 1113, "openblock"),
                new Classification("Django template tag", 1114, 1116, "%}"),
                new Classification("Django template tag", 1122, 1124, "{%"),
                new Classification("keyword", 1125, 1136, "templatetag"),
                new Classification("keyword", 1137, 1147, "closeblock"),
                new Classification("Django template tag", 1148, 1150, "%}"),
                new Classification("Django template tag", 1156, 1158, "{%"),
                new Classification("keyword", 1159, 1169, "widthratio"),
                new Classification("identifier", 1170, 1173, "foo"),
                new Classification("Django template tag", 1174, 1176, "%}"),
                new Classification("Django template tag", 1182, 1184, "{{"),
                new Classification("identifier", 1185, 1188, "foo"),
                new Classification("Django template tag", 1199, 1201, "}}"),
                new Classification("HTML Tag Delimiter", 1203, 1205, "</"),
                new Classification("HTML Element Name", 1205, 1209, "body"),
                new Classification("HTML Tag Delimiter", 1209, 1210, ">"),
                new Classification("HTML Tag Delimiter", 1212, 1214, "</"),
                new Classification("HTML Element Name", 1214, 1218, "html"),
                new Classification("HTML Tag Delimiter", 1218, 1219, ">")
                );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion1() {
            InsertionTest("Insertion1.html.djt", 8, 10, "}",
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
                new Classification("identifier", 88, 91, "foo"),
                new Classification("Django template tag", 92, 94, "}}"),
                new Classification("HTML Tag Delimiter", 96, 98, "</"),
                new Classification("HTML Element Name", 98, 102, "body"),
                new Classification("HTML Tag Delimiter", 102, 103, ">"),
                new Classification("HTML Tag Delimiter", 105, 107, "</"),
                new Classification("HTML Element Name", 107, 111, "html"),
                new Classification("HTML Tag Delimiter", 111, 112, ">")
            );

            InsertionTest("Insertion1.html.djt", 8, 10, "}aaa",
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
                new Classification("identifier", 91, 94, "foo"),
                new Classification("Django template tag", 95, 97, "}}"),
                new Classification("HTML Tag Delimiter", 99, 101, "</"),
                new Classification("HTML Element Name", 101, 105, "body"),
                new Classification("HTML Tag Delimiter", 105, 106, ">"),
                new Classification("HTML Tag Delimiter", 108, 110, "</"),
                new Classification("HTML Element Name", 110, 114, "html"),
                new Classification("HTML Tag Delimiter", 114, 115, ">")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion2() {
            InsertionDeletionTest("Insertion2.html.djt", 9, 34, "{",
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
                    new Classification("identifier", 116, 119, "foo"),
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion3() {
            InsertionTest("Insertion3.html.djt", 2, 5, "}",
                new Classification("HTML Tag Delimiter", 0, 1, "<"),
                new Classification("HTML Element Name", 1, 5, "html"),
                new Classification("HTML Tag Delimiter", 5, 6, ">"),
                new Classification("Django template tag", 8, 10, "{{"),
                new Classification("Django template tag", 11, 13, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion4() {
            InsertionTest("Insertion4.html.djt", 1, 1, "{",
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 10, 12, "}}")
            );

            InsertionTest("Insertion4.html.djt", 1, 2, "{",
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 10, 12, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion5() {
            InsertionTest("Insertion5.html.djt", 1, 2, "#",
                new Classification("Django template tag", 0, 2, "{#"),
                new Classification("comment", 2, 11, "{<html>\r\n"),
                new Classification("Django template tag", 11, 13, "#}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion6() {
            InsertionTest("Insertion6.html.djt", 1, 4, "a",
                new Classification("Django template tag", 4, 6, "{{"),
                new Classification("Django template tag", 16, 18, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion7() {
            InsertionTest("Insertion7.html.djt", 1, 16, "{",
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 10, 12, "}}"),
                new Classification("Django template tag", 15, 17, "{{"),
                new Classification("Django template tag", 28, 30, "}}"),
                new Classification("HTML Tag Delimiter", 38, 39, "<"),
                new Classification("HTML Element Name", 39, 42, "foo"),
                new Classification("HTML Tag Delimiter", 42, 43, ">"),
                new Classification("Django template tag", 49, 51, "{{"),
                new Classification("Django template tag", 61, 63, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion8() {
            InsertionTest("Insertion8.html.djt", 2, 9, "}",
                new Classification("HTML Tag Delimiter", 0, 1, "<"),
                new Classification("HTML Element Name", 1, 5, "html"),
                new Classification("HTML Tag Delimiter", 5, 6, ">"),
                new Classification("Django template tag", 8, 10, "{{"),
                new Classification("identifier", 11, 14, "foo"),
                new Classification("Django template tag", 15, 17, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion9() {
            InsertionTest("Insertion9.html.djt", 1, 7, "a",
                new Classification("Django template tag", 4, 6, "{{"),
                new Classification("identifier", 6, 7, "a"),
                new Classification("Django template tag", 17, 19, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion10() {
            InsertionTest("Insertion10.html.djt", 7, 10, "a",
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
                new Classification("identifier", 75, 78, "foo"),
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion11() {
            InsertionTest("Insertion11.html.djt", 9, 5, 1, "{", true,
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
                new Classification("keyword", 99, 101, "in"),
                new Classification("Django template tag", 123, 125, "%}"),
                new Classification("Django template tag", 135, 137, "{%"),
                new Classification("keyword", 138, 141, "for"),
                new Classification("keyword", 153, 155, "in"),
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

            InsertionTest("Insertion11.html.djt", 9, 5, 2, "{%", true,
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
                new Classification("identifier", 102, 122, "latest_poll_list_foo"),
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


        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Deletion1() {
            DeletionTest("Deletion1.html.djt", 1, 2, 1,
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 12, 14, "}}")
            );

            DeletionTest("Deletion1.html.djt", 1, 3, 1,
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 12, 14, "}}")
            );

            DeletionTest("Deletion1.html.djt", 1, 4, 1,
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 12, 14, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Paste1() {
            PasteTest("Paste1.html.djt", 1, 2, "{{foo}}", "{{bazz}}",
                new Classification("Django template tag", 0, 2, "{{"),
                new Classification("Django template tag", 12, 14, "}}"),
                new Classification("HTML Tag Delimiter", 18, 19, "<"),
                new Classification("HTML Element Name", 19, 22, "foo"),
                new Classification("HTML Tag Delimiter", 22, 23, ">"),
                new Classification("Django template tag", 25, 27, "{{"),
                new Classification("identifier", 27, 31, "bazz"),
                new Classification("Django template tag", 31, 33, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed1() {
            SelectAllAndDeleteTest("SelectAllMixed1.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed2() {
            SelectAllAndDeleteTest("SelectAllMixed2.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed3() {
            SelectAllAndDeleteTest("SelectAllMixed3.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed4() {
            SelectAllAndDeleteTest("SelectAllMixed4.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllTag() {
            SelectAllAndDeleteTest("SelectAllTag.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllText() {
            SelectAllAndDeleteTest("SelectAllText.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutUndo() {
            CutUndoTest("CutUndo.html.djt",
                6, 1, "{% for x in bar %}",
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
                new Classification("identifier", 62, 65, "bar"),
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

        private static void SelectAllAndDeleteTest(string filename) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);

            item.Invoke(() => {
                using (var edit = item.TextView.TextBuffer.CreateEdit()) {
                    edit.Delete(new Span(0, item.TextView.TextBuffer.CurrentSnapshot.Length));
                    edit.Apply();
                }
            });

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
            var classifier = item.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            Classification.Verify(spans);
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void DeletionTest(string filename, int line, int column, int deletionCount, params Classification[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);
            for (int i = 0; i < deletionCount; i++) {
                Keyboard.Backspace();
            }

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
            var classifier = item.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            Classification.Verify(
                spans,
                expected
            );
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void PasteTest(string filename, int line, int column, string selectionText, string pasteText, params Classification[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;

            var selectionStart = snapshot.GetText().IndexOf(selectionText);
            item.Invoke(() => {
                item.TextView.Selection.Select(new SnapshotSpan(item.TextView.TextBuffer.CurrentSnapshot, new Span(selectionStart, selectionText.Length)), false);
                System.Windows.Clipboard.SetText(pasteText);
            });

            AutoResetEvent are = new AutoResetEvent(false);
            EventHandler<TextContentChangedEventArgs> textChangedHandler = (sender, args) => {
                are.Set();
            };
            item.TextView.TextBuffer.Changed += textChangedHandler;
            Keyboard.ControlV();
            Assert.IsTrue(are.WaitOne(5000));
            item.TextView.TextBuffer.Changed -= textChangedHandler;

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

        private static void CutUndoTest(string filename, int line, int column, string selectionText, params Classification[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;

            var selectionStart = snapshot.GetText().IndexOf(selectionText);
            item.Invoke(() => {
                item.TextView.Selection.Select(new SnapshotSpan(item.TextView.TextBuffer.CurrentSnapshot, new Span(selectionStart, selectionText.Length)), false);
                Keyboard.ControlX();
            });

            AutoResetEvent are = new AutoResetEvent(false);
            EventHandler<TextContentChangedEventArgs> textChangedHandler = (sender, args) => {
                are.Set();
            };
            item.TextView.TextBuffer.Changed += textChangedHandler;
            
            Keyboard.ControlZ();
            Assert.IsTrue(are.WaitOne(5000));
            item.TextView.TextBuffer.Changed -= textChangedHandler;

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

        private static void InsertionTest(string filename, int line, int column, string insertionText, params Classification[] expected) {
            InsertionTest(filename, line, column, -1, insertionText, false, true, expected);
        }

        private static void InsertionTest(string filename, int line, int column, int selectionLength, string insertionText, bool paste, params Classification[] expected) {
            InsertionTest(filename, line, column, selectionLength, insertionText, paste, true, expected);
        }

        private static void InsertionTest(string filename, int line, int column, int selectionLength, string insertionText, bool paste, bool checkInsertionLen, params Classification[] expected) {
            InsertionTest(filename, line, column, selectionLength, insertionText, paste, checkInsertionLen, @"TestData\DjangoEditProject.sln", false, expected);
        }

        private static void InsertionTest(string filename, int line, int column, int selectionLength, string insertionText, bool paste, bool checkInsertionLen, string projectName, bool wait, params Classification[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window, projectName, wait);
            
            item.MoveCaret(line, column);
            var pos = item.TextView.Caret.Position.BufferPosition.Position;
            if (selectionLength != -1) {
                item.Select(line, column, selectionLength);
            }
            window.Activate();

            if (!String.IsNullOrEmpty(insertionText)) {
                AutoResetEvent are = new AutoResetEvent(false);
                int delta = 0;
                EventHandler<TextContentChangedEventArgs> textChangedHandler = (sender, args) => {
                    foreach (var change in args.Changes) {
                        delta += change.Delta;
                    }
                    if (selectionLength == -1) {
                        if (delta >= insertionText.Length) {
                            are.Set();
                        }
                    } else {
                        if (delta == insertionText.Length - selectionLength) {
                            are.Set();
                        }
                    }
                };

                item.TextView.TextBuffer.Changed += textChangedHandler;
                if (paste) {
                    item.Invoke(() => System.Windows.Clipboard.SetText(insertionText));
                    Keyboard.ControlV();
                } else {
                    Keyboard.Type(insertionText);
                }
                Assert.IsTrue(are.WaitOne(5000));

                var newPos = item.TextView.Caret.Position.BufferPosition;
                if (checkInsertionLen) {
                    Assert.AreEqual(pos + insertionText.Length, newPos.Position);
                }
                item.TextView.TextBuffer.Changed -= textChangedHandler;
            }

            IList<ClassificationSpan> spans = null;
            item.Invoke(() => {
                var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
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

        private static void InsertionDeletionTest(string filename, int line, int column, string insertionText, Classification[] expectedFirst, Classification[] expectedAfter) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);
            AutoResetEvent are = new AutoResetEvent(false);
            EventHandler<TextContentChangedEventArgs> textChangedHandler = (sender, args) => {
                are.Set();
            };

            item.TextView.TextBuffer.Changed += textChangedHandler;
            Keyboard.Type(insertionText);
            Assert.IsTrue(are.WaitOne(5000));
            are.Reset();

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

            for (int i = 0; i < insertionText.Length; i++) {
                Keyboard.Backspace();
            }
            Assert.IsTrue(are.WaitOne(5000));
            item.TextView.TextBuffer.Changed -= textChangedHandler;

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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IntellisenseCompletions() {
            InsertionTest("Intellisense.html.djt", 6, 3, -1, " end\r", 
                paste: false, 
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
                    new Classification("identifier", 60, 63, "foo"),
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IntellisenseCompletions2() {
            InsertionTest("Intellisense2.html.djt", 6, 1, -1, "{{ b\t }}",
                paste: false,
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
                    new Classification("identifier", 55, 58, "bar"),
                    new Classification("keyword", 59, 61, "in"),
                    new Classification("identifier", 62, 65, "foo"),
                    new Classification("Django template tag", 66, 68, "%}"),
                    new Classification("Django template tag", 70, 72, "{{"),
                    new Classification("identifier", 73, 76, "bar"),
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IntellisenseCompletions3() {
            InsertionTest("Intellisense3.html.djt", 4, 1, -1, "<bo>",
                paste: false,
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
                    new Classification("identifier", 55, 58, "bar"),
                    new Classification("keyword", 59, 61, "in"),
                    new Classification("identifier", 62, 65, "foo"),
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IntellisenseCompletions4() {
            InsertionTest("TestApp\\Templates\\page.html.djt", 6, 11, -1, "|c\t",
                paste: false,
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IntellisenseCompletions5() {
            InsertionTest("TestApp\\Templates\\page.html.djt", 6, 11, -1, ".c\t",
                paste: false,
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IntellisenseCompletions6() {
            InsertionTest("TestApp\\Templates\\page.html.djt", 7, 1, -1, "{% auto\t o\t %}",
                paste: false,
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
                    new Classification("keyword", 68, 72, "auto"),
                    new Classification("excluded code", 72, 75, "  o"),
                    new Classification("Django template tag", 78, 80, "%}"),
                    new Classification("HTML Tag Delimiter", 82, 84, "</"),
                    new Classification("HTML Element Name", 84, 88, "body"),
                    new Classification("HTML Tag Delimiter", 88, 89, ">"),
                    new Classification("HTML Tag Delimiter", 91, 93, "</"),
                    new Classification("HTML Element Name", 93, 97, "html"),
                    new Classification("HTML Tag Delimiter", 97, 98, ">")
                }
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IntellisenseCompletions7() {
            InsertionTest("TestApp\\Templates\\page2.html.djt", 6, 4, -1, "" + Keyboard.CtrlSpace + "auto\t",
                paste: false,
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
                    new Classification("keyword", 53, 70, "endautoescapeauto"),
                    new Classification("excluded code", 70, 87, "    autoescape on"),
                    new Classification("Django template tag", 88, 90, "%}"),
                    new Classification("Django template tag", 92, 94, "{%"),
                    new Classification("keyword", 95, 99, "auto"),
                    new Classification("excluded code", 99, 102, " on"),
                    new Classification("Django template tag", 103, 105, "%}"),
                    new Classification("Django template tag", 107, 109, "{{"),
                    new Classification("Django template tag", 110, 112, "}}"),
                    new Classification("HTML Tag Delimiter", 116, 118, "</"),
                    new Classification("HTML Element Name", 118, 122, "body"),
                    new Classification("HTML Tag Delimiter", 122, 123, ">"),
                    new Classification("HTML Tag Delimiter", 125, 127, "</"),
                    new Classification("HTML Element Name", 127, 131, "html"),
                    new Classification("HTML Tag Delimiter", 131, 132, ">")
                }
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IntellisenseCompletions8() {
            InsertionTest("TestApp\\Templates\\page2.html.djt", 7, 8, -1, Keyboard.CtrlSpace.ToString(),
                paste: false,
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
                    new Classification("Django template tag", 67, 69, "%}"),
                    new Classification("Django template tag", 71, 73, "{%"),
                    new Classification("keyword", 74, 84, "autoescape"),
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IntellisenseCompletions9() {
            InsertionTest("TestApp\\Templates\\page2.html.djt", 8, 4, -1, Keyboard.CtrlSpace + "con\t",
                paste: false,
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


        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IntellisenseCompletions10() {
            InsertionTest("TestApp\\Templates\\page3.html.djt", 6, 1, -1, Keyboard.CtrlSpace + "{% fo\t foo in con\t \t %}",
                paste: false,
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
                    new Classification("identifier", 57, 60, "foo"),
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

        private static EditorWindow OpenDjangoProjectItem(string startItem, out Window window, string projectName = @"TestData\DjangoEditProject.sln", bool wait = false) {
            var project = DebuggerUITests.DebugProject.OpenProject(projectName, startItem);
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

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            window = item.Open();
            window.Activate();
            var doc = app.GetDocument(item.Document.FullName);

            if (wait) {
                pyProj.GetAnalyzer().WaitForCompleteAnalysis(x => true);
                Console.WriteLine("Waited for a complete analysis");
            }

            return doc;
        }
    }
}
