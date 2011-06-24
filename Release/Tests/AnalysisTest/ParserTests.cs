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
using System.IO;
using System.Numerics;
using System.Text;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTest {
    /// <summary>
    /// Test cases for parser written in a continuation passing style.
    /// </summary>
    [TestClass]
    [DeploymentItem(@"Python.VS.TestData\\Grammar", "Python.VS.TestData\\Grammar")]
    public class ParserTests {
        internal static readonly PythonLanguageVersion[] AllVersions = new[] { PythonLanguageVersion.V24, PythonLanguageVersion.V25, PythonLanguageVersion.V26, PythonLanguageVersion.V27, PythonLanguageVersion.V30, PythonLanguageVersion.V31, PythonLanguageVersion.V32, };
        internal static readonly PythonLanguageVersion[] V25AndUp = new[] { PythonLanguageVersion.V25, PythonLanguageVersion.V26, PythonLanguageVersion.V27, PythonLanguageVersion.V30, PythonLanguageVersion.V31, PythonLanguageVersion.V32, };
        internal static readonly PythonLanguageVersion[] V26AndUp = new[] { PythonLanguageVersion.V26, PythonLanguageVersion.V27, PythonLanguageVersion.V30, PythonLanguageVersion.V31, PythonLanguageVersion.V32, };
        internal static readonly PythonLanguageVersion[] V27AndUp = new[] { PythonLanguageVersion.V27, PythonLanguageVersion.V30, PythonLanguageVersion.V31, PythonLanguageVersion.V32, };
        internal static readonly PythonLanguageVersion[] V2Versions = new[] { PythonLanguageVersion.V24, PythonLanguageVersion.V25, PythonLanguageVersion.V26, PythonLanguageVersion.V27,  };
        internal static readonly PythonLanguageVersion[] V24_V26Versions = new[] { PythonLanguageVersion.V24, PythonLanguageVersion.V25, PythonLanguageVersion.V26 };
        internal static readonly PythonLanguageVersion[] V24_V25Versions = new[] { PythonLanguageVersion.V24, PythonLanguageVersion.V25 };
        internal static readonly PythonLanguageVersion[] V26_V27Versions = new[] { PythonLanguageVersion.V26, PythonLanguageVersion.V27 };
        internal static readonly PythonLanguageVersion[] V3Versions = new[] { PythonLanguageVersion.V30, PythonLanguageVersion.V31, PythonLanguageVersion.V32 };

        #region Test Cases

        [TestMethod]
        public void MixedWhiteSpace() {
            // mixed, but in different blocks, which is ok
            ParseErrors("MixedWhitespace1.py", PythonLanguageVersion.V27, Severity.Error);

            // mixed in the same block, tabs first
            ParseErrors("MixedWhitespace2.py", PythonLanguageVersion.V27, Severity.Error, new ErrorInfo("inconsistent whitespace", 293, 13, 32, 302, 14, 9));

            // mixed in same block, spaces first
            ParseErrors("MixedWhitespace3.py", PythonLanguageVersion.V27, Severity.Error, new ErrorInfo("inconsistent whitespace", 285, 13, 24, 287, 14, 2));

            // mixed on same line, spaces first
            ParseErrors("MixedWhitespace4.py", PythonLanguageVersion.V27, Severity.Error);

            // mixed on same line, tabs first
            ParseErrors("MixedWhitespace5.py", PythonLanguageVersion.V27, Severity.Error);
        }

        [TestMethod]
        public void Errors() {
            ParseErrors("AllErrors.py",
                PythonLanguageVersion.V24,
                new ErrorInfo("future statement does not support import *", 0, 1, 1, 24, 1, 25),
                new ErrorInfo("future feature is not defined: *", 0, 1, 1, 24, 1, 25),
                new ErrorInfo("not a chance", 26, 2, 1, 55, 2, 30),
                new ErrorInfo("future feature is not defined: unknown", 57, 3, 1, 87, 3, 31),
                new ErrorInfo("default value must be specified here", 106, 5, 16, 107, 5, 17),
                new ErrorInfo("non-keyword arg after keyword arg", 134, 8, 12, 135, 8, 13),
                new ErrorInfo("only one * allowed", 147, 9, 10, 149, 9, 12),
                new ErrorInfo("only one ** allowed", 162, 10, 11, 165, 10, 14),
                new ErrorInfo("keywords must come before ** args", 180, 11, 13, 186, 11, 19),
                new ErrorInfo("unexpected token 'pass'", 197, 14, 1, 201, 14, 5),
                new ErrorInfo("unexpected token '42'", 217, 17, 11, 219, 17, 13),
                new ErrorInfo("'break' outside loop", 278, 25, 1, 283, 25, 6),
                new ErrorInfo("'continue' not properly in loop", 285, 26, 1, 293, 26, 9),
                new ErrorInfo("print statement expected expression to be printed", 297, 28, 1, 311, 28, 15),
                new ErrorInfo("'continue' not supported inside 'finally' clause", 374, 34, 9, 382, 34, 17),
                new ErrorInfo("expected expression after del", 386, 36, 1, 389, 36, 4),
                new ErrorInfo("can't delete binary operator", 396, 37, 5, 399, 37, 8),
                new ErrorInfo("can't delete unary operator", 405, 38, 5, 407, 38, 7),
                new ErrorInfo("can't delete or expression", 413, 39, 5, 421, 39, 13),
                new ErrorInfo("can't delete and expression", 427, 40, 5, 436, 40, 14),
                new ErrorInfo("can't delete dictionary display", 442, 41, 5, 444, 41, 7),
                new ErrorInfo("can't delete literal", 450, 42, 5, 454, 42, 9),
                new ErrorInfo("can't delete literal", 460, 43, 5, 464, 43, 9),
                new ErrorInfo("can't assign to literal", 468, 45, 1, 472, 45, 5),
                new ErrorInfo("can't assign to literal", 482, 46, 1, 486, 46, 5),
                new ErrorInfo("'return' outside function", 498, 48, 1, 504, 48, 7),
                new ErrorInfo("'return' with argument inside generator", 539, 53, 5, 548, 53, 14),
                new ErrorInfo("misplaced yield", 552, 55, 1, 557, 55, 6),
                new ErrorInfo("'return' with argument inside generator", 581, 59, 5, 590, 59, 14),
                new ErrorInfo("'return' with argument inside generator", 596, 60, 5, 606, 60, 15),
                new ErrorInfo("invalid syntax", 657, 68, 5, 658, 68, 6),
                new ErrorInfo("invalid syntax", 661, 68, 9, 662, 68, 10),
                new ErrorInfo("illegal expression for augmented assignment", 674, 70, 1, 676, 70, 3),
                new ErrorInfo("missing module name", 692, 72, 6, 698, 72, 12),
                new ErrorInfo("from __future__ imports must occur at the beginning of the file", 749, 78, 1, 780, 78, 32),
                new ErrorInfo("unexpected token 'foobar'", 797, 82, 10, 803, 82, 16),
                new ErrorInfo("invalid syntax, from cause not allowed in 2.x.", 837, 87, 11, 845, 87, 19),
                new ErrorInfo("invalid syntax, class decorators require 2.6 or later.", 861, 93, 1, 866, 93, 6),
                new ErrorInfo("invalid syntax, parameter annotations require 3.x", 890, 96, 8, 894, 96, 12),
                new ErrorInfo("default value must be specified here", 924, 99, 15, 925, 99, 16),
                new ErrorInfo("positional parameter after * args not allowed", 953, 102, 13, 959, 102, 19),
                new ErrorInfo("duplicate * args arguments", 987, 105, 13, 988, 105, 14),
                new ErrorInfo("duplicate * args arguments", 1017, 108, 13, 1018, 108, 14),
                new ErrorInfo("unexpected token ','", 1045, 111, 11, 1046, 111, 12),
                new ErrorInfo("unexpected token '42'", 1107, 117, 11, 1109, 117, 13),
                new ErrorInfo("duplicate argument 'abc' in function definition", 1143, 120, 12, 1146, 120, 15),
                new ErrorInfo("duplicate argument 'abc' in function definition", 1177, 123, 16, 1180, 123, 19),
                new ErrorInfo("default 'except' must be last", 1242, 132, 1, 1248, 132, 7),
                new ErrorInfo("'as' requires Python 2.6 or later", 1328, 139, 18, 1330, 139, 20),
                new ErrorInfo("invalid syntax", 1398, 147, 2, 1403, 147, 7),
                new ErrorInfo("invalid syntax", 1404, 147, 8, 1409, 147, 13),
                new ErrorInfo("unexpected token 'b'", 1417, 148, 7, 1418, 148, 8),
                new ErrorInfo("invalid syntax", 1431, 149, 7, 1433, 149, 9),
                new ErrorInfo("invalid syntax", 1431, 149, 7, 1433, 149, 9),
                new ErrorInfo("invalid syntax", 1436, 150, 2, 1441, 150, 7),
                new ErrorInfo("invalid syntax", 1442, 150, 8, 1444, 150, 10),
                new ErrorInfo("invalid syntax", 1451, 152, 4, 1453, 152, 6),
                new ErrorInfo("expected name", 1459, 154, 3, 1461, 154, 5),
                new ErrorInfo("invalid syntax, set literals require Python 2.7 or later.", 1511, 160, 12, 1512, 160, 13),
                new ErrorInfo("invalid syntax, set literals require Python 2.7 or later.", 1521, 161, 7, 1522, 161, 8)
            );

            ParseErrors("AllErrors.py",
                PythonLanguageVersion.V25,
                new ErrorInfo("future statement does not support import *", 0, 1, 1, 24, 1, 25),
                new ErrorInfo("future feature is not defined: *", 0, 1, 1, 24, 1, 25),
                new ErrorInfo("not a chance", 26, 2, 1, 55, 2, 30),
                new ErrorInfo("future feature is not defined: unknown", 57, 3, 1, 87, 3, 31),
                new ErrorInfo("default value must be specified here", 106, 5, 16, 107, 5, 17),
                new ErrorInfo("non-keyword arg after keyword arg", 134, 8, 12, 135, 8, 13),
                new ErrorInfo("only one * allowed", 147, 9, 10, 149, 9, 12),
                new ErrorInfo("only one ** allowed", 162, 10, 11, 165, 10, 14),
                new ErrorInfo("keywords must come before ** args", 180, 11, 13, 186, 11, 19),
                new ErrorInfo("unexpected token 'pass'", 197, 14, 1, 201, 14, 5),
                new ErrorInfo("unexpected token '42'", 217, 17, 11, 219, 17, 13),
                new ErrorInfo("'break' outside loop", 278, 25, 1, 283, 25, 6),
                new ErrorInfo("'continue' not properly in loop", 285, 26, 1, 293, 26, 9),
                new ErrorInfo("print statement expected expression to be printed", 297, 28, 1, 311, 28, 15),
                new ErrorInfo("'continue' not supported inside 'finally' clause", 374, 34, 9, 382, 34, 17),
                new ErrorInfo("expected expression after del", 386, 36, 1, 389, 36, 4),
                new ErrorInfo("can't delete binary operator", 396, 37, 5, 399, 37, 8),
                new ErrorInfo("can't delete unary operator", 405, 38, 5, 407, 38, 7),
                new ErrorInfo("can't delete or expression", 413, 39, 5, 421, 39, 13),
                new ErrorInfo("can't delete and expression", 427, 40, 5, 436, 40, 14),
                new ErrorInfo("can't delete dictionary display", 442, 41, 5, 444, 41, 7),
                new ErrorInfo("can't delete literal", 450, 42, 5, 454, 42, 9),
                new ErrorInfo("can't delete literal", 460, 43, 5, 464, 43, 9),
                new ErrorInfo("can't assign to literal", 468, 45, 1, 472, 45, 5),
                new ErrorInfo("can't assign to literal", 482, 46, 1, 486, 46, 5),
                new ErrorInfo("'return' outside function", 498, 48, 1, 504, 48, 7),
                new ErrorInfo("'return' with argument inside generator", 539, 53, 5, 548, 53, 14),
                new ErrorInfo("misplaced yield", 552, 55, 1, 557, 55, 6),
                new ErrorInfo("'return' with argument inside generator", 581, 59, 5, 590, 59, 14),
                new ErrorInfo("'return' with argument inside generator", 596, 60, 5, 606, 60, 15),
                new ErrorInfo("invalid syntax", 657, 68, 5, 658, 68, 6),
                new ErrorInfo("invalid syntax", 661, 68, 9, 662, 68, 10),
                new ErrorInfo("illegal expression for augmented assignment", 674, 70, 1, 676, 70, 3),
                new ErrorInfo("missing module name", 692, 72, 6, 698, 72, 12),
                new ErrorInfo("from __future__ imports must occur at the beginning of the file", 749, 78, 1, 780, 78, 32),
                new ErrorInfo("unexpected token 'foobar'", 797, 82, 10, 803, 82, 16),
                new ErrorInfo("invalid syntax, from cause not allowed in 2.x.", 837, 87, 11, 845, 87, 19),
                new ErrorInfo("invalid syntax, class decorators require 2.6 or later.", 861, 93, 1, 866, 93, 6),
                new ErrorInfo("invalid syntax, parameter annotations require 3.x", 890, 96, 8, 894, 96, 12),
                new ErrorInfo("default value must be specified here", 924, 99, 15, 925, 99, 16),
                new ErrorInfo("positional parameter after * args not allowed", 953, 102, 13, 959, 102, 19),
                new ErrorInfo("duplicate * args arguments", 987, 105, 13, 988, 105, 14),
                new ErrorInfo("duplicate * args arguments", 1017, 108, 13, 1018, 108, 14),
                new ErrorInfo("unexpected token ','", 1045, 111, 11, 1046, 111, 12),
                new ErrorInfo("unexpected token '42'", 1107, 117, 11, 1109, 117, 13),
                new ErrorInfo("duplicate argument 'abc' in function definition", 1143, 120, 12, 1146, 120, 15),
                new ErrorInfo("duplicate argument 'abc' in function definition", 1177, 123, 16, 1180, 123, 19),
                new ErrorInfo("default 'except' must be last", 1242, 132, 1, 1248, 132, 7),
                new ErrorInfo("'as' requires Python 2.6 or later", 1328, 139, 18, 1330, 139, 20),
                new ErrorInfo("invalid syntax", 1398, 147, 2, 1403, 147, 7),
                new ErrorInfo("invalid syntax", 1404, 147, 8, 1409, 147, 13),
                new ErrorInfo("unexpected token 'b'", 1417, 148, 7, 1418, 148, 8),
                new ErrorInfo("invalid syntax", 1431, 149, 7, 1433, 149, 9),
                new ErrorInfo("invalid syntax", 1431, 149, 7, 1433, 149, 9),
                new ErrorInfo("invalid syntax", 1436, 150, 2, 1441, 150, 7),
                new ErrorInfo("invalid syntax", 1442, 150, 8, 1444, 150, 10),
                new ErrorInfo("invalid syntax", 1451, 152, 4, 1453, 152, 6),
                new ErrorInfo("expected name", 1459, 154, 3, 1461, 154, 5),
                new ErrorInfo("invalid syntax, set literals require Python 2.7 or later.", 1511, 160, 12, 1512, 160, 13),
                new ErrorInfo("invalid syntax, set literals require Python 2.7 or later.", 1521, 161, 7, 1522, 161, 8)
            );

            ParseErrors("AllErrors.py",
                PythonLanguageVersion.V26,
                new ErrorInfo("future statement does not support import *", 0, 1, 1, 24, 1, 25),
                new ErrorInfo("future feature is not defined: *", 0, 1, 1, 24, 1, 25),
                new ErrorInfo("not a chance", 26, 2, 1, 55, 2, 30),
                new ErrorInfo("future feature is not defined: unknown", 57, 3, 1, 87, 3, 31),
                new ErrorInfo("default value must be specified here", 106, 5, 16, 107, 5, 17),
                new ErrorInfo("non-keyword arg after keyword arg", 134, 8, 12, 135, 8, 13),
                new ErrorInfo("only one * allowed", 147, 9, 10, 149, 9, 12),
                new ErrorInfo("only one ** allowed", 162, 10, 11, 165, 10, 14),
                new ErrorInfo("keywords must come before ** args", 180, 11, 13, 186, 11, 19),
                new ErrorInfo("unexpected token 'pass'", 197, 14, 1, 201, 14, 5),
                new ErrorInfo("unexpected token '42'", 217, 17, 11, 219, 17, 13),
                new ErrorInfo("'break' outside loop", 278, 25, 1, 283, 25, 6),
                new ErrorInfo("'continue' not properly in loop", 285, 26, 1, 293, 26, 9),
                new ErrorInfo("print statement expected expression to be printed", 297, 28, 1, 311, 28, 15),
                new ErrorInfo("'continue' not supported inside 'finally' clause", 374, 34, 9, 382, 34, 17),
                new ErrorInfo("expected expression after del", 386, 36, 1, 389, 36, 4),
                new ErrorInfo("can't delete binary operator", 396, 37, 5, 399, 37, 8),
                new ErrorInfo("can't delete unary operator", 405, 38, 5, 407, 38, 7),
                new ErrorInfo("can't delete or expression", 413, 39, 5, 421, 39, 13),
                new ErrorInfo("can't delete and expression", 427, 40, 5, 436, 40, 14),
                new ErrorInfo("can't delete dictionary display", 442, 41, 5, 444, 41, 7),
                new ErrorInfo("can't delete literal", 450, 42, 5, 454, 42, 9),
                new ErrorInfo("can't delete literal", 460, 43, 5, 464, 43, 9),
                new ErrorInfo("can't assign to literal", 468, 45, 1, 472, 45, 5),
                new ErrorInfo("can't assign to literal", 482, 46, 1, 486, 46, 5),
                new ErrorInfo("'return' outside function", 498, 48, 1, 504, 48, 7),
                new ErrorInfo("'return' with argument inside generator", 539, 53, 5, 548, 53, 14),
                new ErrorInfo("misplaced yield", 552, 55, 1, 557, 55, 6),
                new ErrorInfo("'return' with argument inside generator", 581, 59, 5, 590, 59, 14),
                new ErrorInfo("'return' with argument inside generator", 596, 60, 5, 606, 60, 15),
                new ErrorInfo("invalid syntax", 657, 68, 5, 658, 68, 6),
                new ErrorInfo("invalid syntax", 661, 68, 9, 662, 68, 10),
                new ErrorInfo("illegal expression for augmented assignment", 674, 70, 1, 676, 70, 3),
                new ErrorInfo("missing module name", 692, 72, 6, 698, 72, 12),
                new ErrorInfo("from __future__ imports must occur at the beginning of the file", 749, 78, 1, 780, 78, 32),
                new ErrorInfo("unexpected token 'foobar'", 797, 82, 10, 803, 82, 16),
                new ErrorInfo("invalid syntax, from cause not allowed in 2.x.", 837, 87, 11, 845, 87, 19),
                new ErrorInfo("invalid syntax, parameter annotations require 3.x", 890, 96, 8, 894, 96, 12),
                new ErrorInfo("default value must be specified here", 924, 99, 15, 925, 99, 16),
                new ErrorInfo("positional parameter after * args not allowed", 953, 102, 13, 959, 102, 19),
                new ErrorInfo("duplicate * args arguments", 987, 105, 13, 988, 105, 14),
                new ErrorInfo("duplicate * args arguments", 1017, 108, 13, 1018, 108, 14),
                new ErrorInfo("unexpected token ','", 1045, 111, 11, 1046, 111, 12),
                new ErrorInfo("unexpected token '42'", 1107, 117, 11, 1109, 117, 13),
                new ErrorInfo("duplicate argument 'abc' in function definition", 1143, 120, 12, 1146, 120, 15),
                new ErrorInfo("duplicate argument 'abc' in function definition", 1177, 123, 16, 1180, 123, 19),
                new ErrorInfo("default 'except' must be last", 1242, 132, 1, 1248, 132, 7),
                new ErrorInfo("invalid syntax", 1431, 149, 7, 1433, 149, 9),
                new ErrorInfo("invalid syntax", 1431, 149, 7, 1433, 149, 9),
                new ErrorInfo("invalid syntax", 1442, 150, 8, 1444, 150, 10),
                new ErrorInfo("invalid syntax", 1442, 150, 8, 1444, 150, 10),
                new ErrorInfo("invalid syntax", 1451, 152, 4, 1453, 152, 6),
                new ErrorInfo("expected name", 1459, 154, 3, 1461, 154, 5),
                new ErrorInfo("invalid syntax, set literals require Python 2.7 or later.", 1511, 160, 12, 1512, 160, 13),
                new ErrorInfo("invalid syntax, set literals require Python 2.7 or later.", 1521, 161, 7, 1522, 161, 8)
            );

            ParseErrors("AllErrors.py",
                PythonLanguageVersion.V27,
                new ErrorInfo("future statement does not support import *", 0, 1, 1, 24, 1, 25),
                new ErrorInfo("future feature is not defined: *", 0, 1, 1, 24, 1, 25),
                new ErrorInfo("not a chance", 26, 2, 1, 55, 2, 30),
                new ErrorInfo("future feature is not defined: unknown", 57, 3, 1, 87, 3, 31),
                new ErrorInfo("default value must be specified here", 106, 5, 16, 107, 5, 17),
                new ErrorInfo("non-keyword arg after keyword arg", 134, 8, 12, 135, 8, 13),
                new ErrorInfo("only one * allowed", 147, 9, 10, 149, 9, 12),
                new ErrorInfo("only one ** allowed", 162, 10, 11, 165, 10, 14),
                new ErrorInfo("keywords must come before ** args", 180, 11, 13, 186, 11, 19),
                new ErrorInfo("unexpected token 'pass'", 197, 14, 1, 201, 14, 5),
                new ErrorInfo("unexpected token '42'", 217, 17, 11, 219, 17, 13),
                new ErrorInfo("'break' outside loop", 278, 25, 1, 283, 25, 6),
                new ErrorInfo("'continue' not properly in loop", 285, 26, 1, 293, 26, 9),
                new ErrorInfo("print statement expected expression to be printed", 297, 28, 1, 311, 28, 15),
                new ErrorInfo("'continue' not supported inside 'finally' clause", 374, 34, 9, 382, 34, 17),
                new ErrorInfo("expected expression after del", 386, 36, 1, 389, 36, 4),
                new ErrorInfo("can't delete binary operator", 396, 37, 5, 399, 37, 8),
                new ErrorInfo("can't delete unary operator", 405, 38, 5, 407, 38, 7),
                new ErrorInfo("can't delete or expression", 413, 39, 5, 421, 39, 13),
                new ErrorInfo("can't delete and expression", 427, 40, 5, 436, 40, 14),
                new ErrorInfo("can't delete dictionary display", 442, 41, 5, 444, 41, 7),
                new ErrorInfo("can't delete literal", 450, 42, 5, 454, 42, 9),
                new ErrorInfo("can't delete literal", 460, 43, 5, 464, 43, 9),
                new ErrorInfo("can't assign to literal", 468, 45, 1, 472, 45, 5),
                new ErrorInfo("can't assign to literal", 482, 46, 1, 486, 46, 5),
                new ErrorInfo("'return' outside function", 498, 48, 1, 504, 48, 7),
                new ErrorInfo("'return' with argument inside generator", 539, 53, 5, 548, 53, 14),
                new ErrorInfo("misplaced yield", 552, 55, 1, 557, 55, 6),
                new ErrorInfo("'return' with argument inside generator", 581, 59, 5, 590, 59, 14),
                new ErrorInfo("'return' with argument inside generator", 596, 60, 5, 606, 60, 15),
                new ErrorInfo("invalid syntax", 657, 68, 5, 658, 68, 6),
                new ErrorInfo("invalid syntax", 661, 68, 9, 662, 68, 10),
                new ErrorInfo("illegal expression for augmented assignment", 674, 70, 1, 676, 70, 3),
                new ErrorInfo("missing module name", 692, 72, 6, 698, 72, 12),
                new ErrorInfo("from __future__ imports must occur at the beginning of the file", 749, 78, 1, 780, 78, 32),
                new ErrorInfo("unexpected token 'foobar'", 797, 82, 10, 803, 82, 16),
                new ErrorInfo("invalid syntax, from cause not allowed in 2.x.", 837, 87, 11, 845, 87, 19),
                new ErrorInfo("invalid syntax, parameter annotations require 3.x", 890, 96, 8, 894, 96, 12),
                new ErrorInfo("default value must be specified here", 924, 99, 15, 925, 99, 16),
                new ErrorInfo("positional parameter after * args not allowed", 953, 102, 13, 959, 102, 19),
                new ErrorInfo("duplicate * args arguments", 987, 105, 13, 988, 105, 14),
                new ErrorInfo("duplicate * args arguments", 1017, 108, 13, 1018, 108, 14),
                new ErrorInfo("unexpected token ','", 1045, 111, 11, 1046, 111, 12),
                new ErrorInfo("unexpected token '42'", 1107, 117, 11, 1109, 117, 13),
                new ErrorInfo("duplicate argument 'abc' in function definition", 1143, 120, 12, 1146, 120, 15),
                new ErrorInfo("duplicate argument 'abc' in function definition", 1177, 123, 16, 1180, 123, 19),
                new ErrorInfo("default 'except' must be last", 1242, 132, 1, 1248, 132, 7),
                new ErrorInfo("invalid syntax", 1431, 149, 7, 1433, 149, 9),
                new ErrorInfo("invalid syntax", 1431, 149, 7, 1433, 149, 9),
                new ErrorInfo("invalid syntax", 1442, 150, 8, 1444, 150, 10),
                new ErrorInfo("invalid syntax", 1442, 150, 8, 1444, 150, 10),
                new ErrorInfo("invalid syntax", 1451, 152, 4, 1453, 152, 6),
                new ErrorInfo("expected name", 1459, 154, 3, 1461, 154, 5),
                new ErrorInfo("invalid syntax", 1511, 160, 12, 1512, 160, 13),
                new ErrorInfo("invalid syntax", 1524, 161, 10, 1527, 161, 13)
            );

            foreach (var version in V3Versions) {
                ParseErrors("AllErrors.py",
                    version,
                    new ErrorInfo("future statement does not support import *", 0, 1, 1, 24, 1, 25),
                    new ErrorInfo("future feature is not defined: *", 0, 1, 1, 24, 1, 25),
                    new ErrorInfo("not a chance", 26, 2, 1, 55, 2, 30),
                    new ErrorInfo("future feature is not defined: unknown", 57, 3, 1, 87, 3, 31),
                    new ErrorInfo("default value must be specified here", 106, 5, 16, 107, 5, 17),
                    new ErrorInfo("non-keyword arg after keyword arg", 134, 8, 12, 135, 8, 13),
                    new ErrorInfo("only one * allowed", 147, 9, 10, 149, 9, 12),
                    new ErrorInfo("only one ** allowed", 162, 10, 11, 165, 10, 14),
                    new ErrorInfo("keywords must come before ** args", 180, 11, 13, 186, 11, 19),
                    new ErrorInfo("unexpected token 'pass'", 197, 14, 1, 201, 14, 5),
                    new ErrorInfo("unexpected token '42'", 217, 17, 11, 219, 17, 13),
                    new ErrorInfo("sublist parameters are not supported in 3.x", 216, 17, 10, 222, 17, 16),
                    new ErrorInfo("'break' outside loop", 278, 25, 1, 283, 25, 6),
                    new ErrorInfo("'continue' not properly in loop", 285, 26, 1, 293, 26, 9),
                    new ErrorInfo("'continue' not supported inside 'finally' clause", 374, 34, 9, 382, 34, 17),
                    new ErrorInfo("expected expression after del", 386, 36, 1, 389, 36, 4),
                    new ErrorInfo("can't delete binary operator", 396, 37, 5, 399, 37, 8),
                    new ErrorInfo("can't delete unary operator", 405, 38, 5, 407, 38, 7),
                    new ErrorInfo("can't delete or expression", 413, 39, 5, 421, 39, 13),
                    new ErrorInfo("can't delete and expression", 427, 40, 5, 436, 40, 14),
                    new ErrorInfo("can't delete dictionary display", 442, 41, 5, 444, 41, 7),
                    new ErrorInfo("can't delete literal", 450, 42, 5, 454, 42, 9),
                    new ErrorInfo("can't delete literal", 460, 43, 5, 464, 43, 9),
                    new ErrorInfo("can't assign to literal", 468, 45, 1, 472, 45, 5),
                    new ErrorInfo("can't assign to literal", 482, 46, 1, 486, 46, 5),
                    new ErrorInfo("'return' outside function", 498, 48, 1, 504, 48, 7),
                    new ErrorInfo("'return' with argument inside generator", 539, 53, 5, 548, 53, 14),
                    new ErrorInfo("misplaced yield", 552, 55, 1, 557, 55, 6),
                    new ErrorInfo("'return' with argument inside generator", 581, 59, 5, 590, 59, 14),
                    new ErrorInfo("'return' with argument inside generator", 596, 60, 5, 606, 60, 15),
                    new ErrorInfo("two starred expressions in assignment", 660, 68, 8, 662, 68, 10),
                    new ErrorInfo("illegal expression for augmented assignment", 674, 70, 1, 676, 70, 3),
                    new ErrorInfo("missing module name", 692, 72, 6, 698, 72, 12),
                    new ErrorInfo("import * only allowed at module level", 720, 75, 5, 735, 75, 20),
                    new ErrorInfo("from __future__ imports must occur at the beginning of the file", 749, 78, 1, 780, 78, 32),
                    new ErrorInfo("nonlocal declaration not allowed at module level", 788, 82, 1, 796, 82, 9),
                    new ErrorInfo("invalid syntax, only exception value is allowed in 3.x.", 814, 83, 10, 819, 83, 15),
                    new ErrorInfo("default value must be specified here", 924, 99, 15, 925, 99, 16),
                    new ErrorInfo("duplicate * args arguments", 987, 105, 13, 988, 105, 14),
                    new ErrorInfo("duplicate * args arguments", 1017, 108, 13, 1018, 108, 14),
                    new ErrorInfo("named arguments must follow bare *", 1044, 111, 10, 1048, 111, 14),
                    new ErrorInfo("sublist parameters are not supported in 3.x", 1072, 114, 10, 1077, 114, 15),
                    new ErrorInfo("unexpected token '42'", 1107, 117, 11, 1109, 117, 13),
                    new ErrorInfo("sublist parameters are not supported in 3.x", 1106, 117, 10, 1112, 117, 16),
                    new ErrorInfo("duplicate argument 'abc' in function definition", 1143, 120, 12, 1146, 120, 15),
                    new ErrorInfo("duplicate argument 'abc' in function definition", 1177, 123, 16, 1180, 123, 19),
                    new ErrorInfo("sublist parameters are not supported in 3.x", 1171, 123, 10, 1180, 123, 19),
                    new ErrorInfo("\", variable\" not allowed in 3.x - use \"as variable\" instead.", 1277, 134, 17, 1280, 134, 20),
                    new ErrorInfo("default 'except' must be last", 1242, 132, 1, 1248, 132, 7),
                    new ErrorInfo("\", variable\" not allowed in 3.x - use \"as variable\" instead.", 1379, 144, 17, 1382, 144, 20),
                    new ErrorInfo("cannot mix bytes and nonbytes literals", 1404, 147, 8, 1409, 147, 13),
                    new ErrorInfo("cannot mix bytes and nonbytes literals", 1417, 148, 7, 1423, 148, 13),
                    new ErrorInfo("invalid syntax", 1431, 149, 7, 1433, 149, 9),
                    new ErrorInfo("invalid syntax", 1431, 149, 7, 1433, 149, 9),
                    new ErrorInfo("invalid syntax", 1442, 150, 8, 1444, 150, 10),
                    new ErrorInfo("invalid syntax", 1442, 150, 8, 1444, 150, 10),
                    new ErrorInfo("invalid syntax", 1451, 152, 4, 1453, 152, 6),
                    new ErrorInfo("expected name", 1459, 154, 3, 1461, 154, 5),
                    new ErrorInfo("invalid syntax", 1511, 160, 12, 1512, 160, 13),
                    new ErrorInfo("invalid syntax", 1524, 161, 10, 1527, 161, 13)
                );
            }
        }

        [TestMethod]
        public void Literals() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("Literals.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckStrOrBytesStmt(version, "abc"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "abc"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "abc"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "abc"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckConstantStmt(1000),
                        CheckConstantStmt(2147483647),
                        CheckConstantStmt(3.14),
                        CheckConstantStmt(10.0),
                        CheckConstantStmt(.001),
                        CheckConstantStmt(1e100),
                        CheckConstantStmt(3.14e-10),
                        CheckConstantStmt(0e0),
                        CheckConstantStmt(new Complex(0, 3.14)),
                        CheckConstantStmt(new Complex(0, 10)),
                        CheckConstantStmt(new Complex(0, 10)),
                        CheckConstantStmt(new Complex(0, .001)),
                        CheckConstantStmt(new Complex(0, 1e100)),
                        CheckConstantStmt(new Complex(0, 3.14e-10)),
                        CheckConstantStmt(-2147483648),
                        CheckUnaryStmt(PythonOperator.Negate, CheckConstant(100))                        
                    )
                );
            }

            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("LiteralsV2.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckConstantStmt((BigInteger)1000),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("\\\'\"\a\b\f\n\r\t\u2026\v\x2A\x2A"),
                        IgnoreStmt(), // u'\N{COLON}',
                        CheckUnaryStmt(PythonOperator.Negate, CheckConstant(new BigInteger(2147483648))),
                        CheckUnaryStmt(PythonOperator.Negate, CheckConstant(new BigInteger(2147483648))),
                        CheckConstantStmt(464),
                        CheckUnaryStmt(PythonOperator.Negate, CheckConstant(new BigInteger(100)))    
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("LiteralsV2.py", 
                    version,
                    new ErrorInfo("bad char for the integer value: 'L' (base 10)", 0, 1, 1, 5, 1, 6),
                    new ErrorInfo("invalid syntax", 8, 2, 2, 24, 2, 18),
                    new ErrorInfo("invalid syntax", 27, 3, 2, 43, 3, 18),
                    new ErrorInfo("invalid syntax", 47, 4, 3, 60, 4, 16),
                    new ErrorInfo("invalid syntax", 64, 5, 3, 77, 5, 16),
                    new ErrorInfo("invalid syntax", 81, 6, 3, 94, 6, 16),
                    new ErrorInfo("invalid syntax", 98, 7, 3, 111, 7, 16),
                    new ErrorInfo("invalid syntax", 114, 8, 2, 134, 8, 22),
                    new ErrorInfo("invalid syntax", 137, 9, 2, 157, 9, 22),
                    new ErrorInfo("invalid syntax", 161, 10, 3, 178, 10, 20),
                    new ErrorInfo("invalid syntax", 182, 11, 3, 199, 11, 20),
                    new ErrorInfo("invalid syntax", 203, 12, 3, 220, 12, 20),
                    new ErrorInfo("invalid syntax", 224, 13, 3, 241, 13, 20),
                    new ErrorInfo("invalid syntax", 244, 14, 2, 260, 14, 18),
                    new ErrorInfo("invalid syntax", 263, 15, 2, 279, 15, 18),
                    new ErrorInfo("invalid syntax", 283, 16, 3, 296, 16, 16),
                    new ErrorInfo("invalid syntax", 300, 17, 3, 313, 17, 16),
                    new ErrorInfo("invalid syntax", 317, 18, 3, 330, 18, 16),
                    new ErrorInfo("invalid syntax", 334, 19, 3, 347, 19, 16),
                    new ErrorInfo("invalid syntax", 350, 20, 2, 370, 20, 22),
                    new ErrorInfo("invalid syntax", 373, 21, 2, 393, 21, 22),
                    new ErrorInfo("invalid syntax", 397, 22, 3, 414, 22, 20),
                    new ErrorInfo("invalid syntax", 418, 23, 3, 435, 23, 20),
                    new ErrorInfo("invalid syntax", 439, 24, 3, 456, 24, 20),
                    new ErrorInfo("invalid syntax", 460, 25, 3, 477, 25, 20),
                    new ErrorInfo("invalid syntax", 480, 26, 2, 519, 26, 41),
                    new ErrorInfo("invalid syntax", 522, 27, 2, 533, 27, 13),
                    new ErrorInfo("bad char for the integer value: 'l' (base 10)", 536, 28, 2, 547, 28, 13),
                    new ErrorInfo("bad char for the integer value: 'L' (base 10)", 550, 29, 2, 561, 29, 13),
                    new ErrorInfo("invalid token", 563, 30, 1, 567, 30, 5),
                    new ErrorInfo("bad char for the integer value: 'L' (base 10)", 570, 31, 2, 574, 31, 6)
                );
            }
        }

        [TestMethod]
        public void Literals26() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("Literals26.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckConstantStmt(464),
                        CheckConstantStmt(4)
                    )
                );
            }

            foreach (var version in V24_V25Versions) {
                ParseErrors("Literals26.py",
                    version,
                    new ErrorInfo("unexpected token 'o720'", 1, 1, 2, 5, 1, 6),
                    new ErrorInfo("unexpected token 'b100'", 8, 2, 2, 12, 2, 6)
                );
            }
        }

        [TestMethod]
        public void Keywords25() {
            foreach (var version in V24_V25Versions) {
                CheckAst(
                    ParseFile("Keywords25.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(CheckNameExpr("with"), One),
                        CheckAssignment(CheckNameExpr("as"), Two)
                    )
                );
            }

            foreach (var version in V26AndUp) {
                ParseErrors("Keywords25.py",
                    version,
                    new ErrorInfo("unexpected token '='", 5, 1, 6, 6, 1, 7),
                    new ErrorInfo("invalid syntax", 7, 1, 8, 8, 1, 9),
                    new ErrorInfo("unexpected token '<newline>'", 8, 1, 9, 10, 2, 1),
                    new ErrorInfo("unexpected token 'as'", 10, 2, 1, 12, 2, 3),
                    new ErrorInfo("can't assign to ErrorExpression", 10, 2, 1, 12, 2, 3)
                );
            }
        }

        [TestMethod]
        public void Keywords2x() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("Keywords2x.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(CheckNameExpr("True"), One),
                        CheckAssignment(CheckNameExpr("False"), Zero)
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("Keywords2x.py",
                    version,
                    new ErrorInfo("can't assign to literal", 0, 1, 1, 4, 1, 5),
                    new ErrorInfo("can't assign to literal", 10, 2, 1, 15, 2, 6)
                );
            }
        }

        [TestMethod]
        public void Keywords30() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("Keywords30.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(Foo, CheckConstant(true)),
                        CheckAssignment(Bar, CheckConstant(false))
                    )
                );
            }

            foreach (var version in V2Versions) {
                CheckAst(
                     ParseFile("Keywords30.py", ErrorSink.Null, version),
                     CheckSuite(
                         CheckAssignment(Foo, CheckNameExpr("True")),
                         CheckAssignment(Bar, CheckNameExpr("False"))
                     )
                 );
            }
        }

        [TestMethod]
        public void BinaryOperators() {
            foreach (var version in AllVersions) {

                CheckAst(
                    ParseFile("BinaryOperators.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckBinaryStmt(One, PythonOperator.Add, Two),
                        CheckBinaryStmt(One, PythonOperator.Subtract, Two),
                        CheckBinaryStmt(One, PythonOperator.Multiply, Two),
                        CheckBinaryStmt(One, PythonOperator.Power, Two),
                        CheckBinaryStmt(One, PythonOperator.Divide, Two),
                        CheckBinaryStmt(One, PythonOperator.FloorDivide, Two),
                        CheckBinaryStmt(One, PythonOperator.Mod, Two),
                        CheckBinaryStmt(One, PythonOperator.LeftShift, Two),
                        CheckBinaryStmt(One, PythonOperator.RightShift, Two),
                        CheckBinaryStmt(One, PythonOperator.BitwiseAnd, Two),
                        CheckBinaryStmt(One, PythonOperator.BitwiseOr, Two),
                        CheckBinaryStmt(One, PythonOperator.Xor, Two),
                        CheckBinaryStmt(One, PythonOperator.LessThan, Two),
                        CheckBinaryStmt(One, PythonOperator.GreaterThan, Two),
                        CheckBinaryStmt(One, PythonOperator.LessThanOrEqual, Two),
                        CheckBinaryStmt(One, PythonOperator.GreaterThanOrEqual, Two),
                        CheckBinaryStmt(One, PythonOperator.Equal, Two),
                        CheckBinaryStmt(One, PythonOperator.NotEqual, Two),
                        CheckBinaryStmt(One, PythonOperator.Is, Two),
                        CheckBinaryStmt(One, PythonOperator.IsNot, Two),                        
                        CheckExprStmt(CheckOrExpression(One, Two)),
                        CheckExprStmt(CheckAndExpression(One, Two)),
                        CheckBinaryStmt(One, PythonOperator.In, Two),                    
                        CheckBinaryStmt(One, PythonOperator.NotIn, Two)
                    )
                );
            }
        }

        [TestMethod]
        public void BinaryOperatorsV2() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("BinaryOperatorsV2.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckBinaryStmt(One, PythonOperator.NotEqual, Two)
                    )
                );
            }
            
            foreach (var version in V3Versions) {
                ParseErrors("BinaryOperatorsV2.py", version, new[] { 
                    new ErrorInfo("unexpected token '>'", 3, 1, 4, 4, 1, 5),
                    new ErrorInfo("invalid syntax", 5, 1, 6, 6, 1, 7)
                });
            }
        }

        [TestMethod]
        public void UnaryOperators() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("UnaryOperators.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckUnaryStmt(PythonOperator.Negate, One),
                        CheckUnaryStmt(PythonOperator.Invert, One),
                        CheckUnaryStmt(PythonOperator.Pos, One),
                        CheckUnaryStmt(PythonOperator.Not, One)
                    )
                );
            }
        }

        [TestMethod]
        public void StringPlus() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("StringPlus.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckStrOrBytesStmt(version, "hello again")
                    )
                );
            }
        }

        [TestMethod]
        public void BytesPlus() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("BytesPlus.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckConstant(ToBytes("hello again")))
                    )
                );
            }

            foreach (var version in V24_V25Versions) {
                ParseErrors("BytesPlus.py", version,
                    new ErrorInfo("invalid syntax", 1, 1, 2, 8, 1, 9),
                    new ErrorInfo("unexpected token 'b'", 9, 1, 10, 10, 1, 11)
                );
            }
        }

        [TestMethod]
        public void UnicodePlus() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("UnicodePlus.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckConstant("hello again"))
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("UnicodePlus.py", version,
                    new ErrorInfo("invalid syntax", 1, 1, 2, 8, 1, 9),
                    new ErrorInfo("unexpected token 'u'", 9, 1, 10, 10, 1, 11)
                );

            }
        }

        [TestMethod]
        public void Delimiters() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("Delimiters.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckCallStmt(One, PositionalArg(Two)),
                        CheckIndexStmt(One, Two),
                        CheckDictionaryStmt(DictItem(One, Two)),
                        CheckTupleStmt(One, Two, Three),
                        CheckIndexStmt(One, CheckSlice(Two, Three)),
                        CheckIndexStmt(One, CheckSlice(Two, Three, Four)),
                        CheckIndexStmt(One, CheckSlice(Two, null, Four)),
                        CheckIndexStmt(One, CheckSlice(null, null, Four)),
                        CheckIndexStmt(One, Ellipsis),
                        CheckIndexStmt(One, CheckTupleExpr(CheckSlice(null, null))),
                        CheckMemberStmt(Foo, "bar"),
                        CheckAssignment(Foo, One),
                        CheckAssignment(Foo, PythonOperator.Add, One),
                        CheckAssignment(Foo, PythonOperator.Subtract, One),
                        CheckAssignment(Foo, PythonOperator.Multiply, One),
                        CheckAssignment(Foo, PythonOperator.Divide, One),
                        CheckAssignment(Foo, PythonOperator.FloorDivide, One),
                        CheckAssignment(Foo, PythonOperator.Mod, One),
                        CheckAssignment(Foo, PythonOperator.BitwiseAnd, One),
                        CheckAssignment(Foo, PythonOperator.BitwiseOr, One),
                        CheckAssignment(Foo, PythonOperator.Xor, One),
                        CheckAssignment(Foo, PythonOperator.RightShift, One),
                        CheckAssignment(Foo, PythonOperator.LeftShift, One),
                        CheckAssignment(Foo, PythonOperator.Power, One)
                    )
                );
            }
        }

        [TestMethod]
        public void DelimitersV2() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("DelimitersV2.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckBackquoteStmt(Foo)
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors(
                    "DelimitersV2.py",
                    version,
                    new [] { 
                        new ErrorInfo("unexpected token '`'", 0, 1, 1, 1, 1, 2),
                        new ErrorInfo("unexpected token 'foo'", 1, 1, 2, 4, 1, 5),
                        new ErrorInfo("unexpected token '`'", 4, 1, 5, 5, 1, 6)
                   }
                );
            }
        }

        [TestMethod]
        public void ForStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("ForStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckForStmt(Foo, Bar, CheckSuite(Pass)),
                        CheckForStmt(CheckTupleExpr(Foo, Bar), Baz, CheckSuite(Pass)),
                        CheckForStmt(Foo, Bar, CheckSuite(Pass), CheckSuite(Pass)),
                        CheckForStmt(Foo, Bar, CheckSuite(Break)),
                        CheckForStmt(Foo, Bar, CheckSuite(Continue)),
                        CheckForStmt(CheckListExpr(CheckListExpr(Foo), CheckListExpr(Bar)), Baz, CheckSuite(Pass)),
                        CheckForStmt(CheckTupleExpr(CheckParenExpr(Foo), CheckParenExpr(Bar)), Baz, CheckSuite(Pass))
                    )
                );
            }
        }

        [TestMethod]
        public void WithStmt() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("WithStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckWithStmt(Foo, CheckSuite(Pass)),
                        CheckWithStmt(Foo, Bar, CheckSuite(Pass)),
                        CheckWithStmt(new[] { Foo, Bar }, CheckSuite(Pass)),
                        CheckWithStmt(new[] { Foo, Baz }, new[] { Bar, Quox }, CheckSuite(Pass))
                    )
                );
            }

            foreach (var version in V24_V25Versions) {
                ParseErrors("WithStmt.py", version,
                    new ErrorInfo("unexpected token 'foo'", 5, 1, 6, 8, 1, 9),
                    new ErrorInfo("unexpected token ':'", 8, 1, 9, 9, 1, 10),
                    new ErrorInfo("unexpected token 'pass'", 10, 1, 11, 14, 1, 15),
                    new ErrorInfo("unexpected token 'foo'", 23, 3, 6, 26, 3, 9),
                    new ErrorInfo("unexpected token 'bar'", 30, 3, 13, 33, 3, 16),
                    new ErrorInfo("unexpected token ':'", 33, 3, 16, 34, 3, 17),
                    new ErrorInfo("unexpected token 'pass'", 35, 3, 18, 39, 3, 22),
                    new ErrorInfo("unexpected token 'foo'", 48, 5, 6, 51, 5, 9),
                    new ErrorInfo("unexpected token ','", 51, 5, 9, 52, 5, 10),
                    new ErrorInfo("unexpected token 'bar'", 53, 5, 11, 56, 5, 14),
                    new ErrorInfo("unexpected token ':'", 56, 5, 14, 57, 5, 15),
                    new ErrorInfo("unexpected token 'pass'", 58, 5, 16, 62, 5, 20),
                    new ErrorInfo("unexpected token 'foo'", 71, 7, 6, 74, 7, 9),
                    new ErrorInfo("unexpected token 'bar'", 78, 7, 13, 81, 7, 16),
                    new ErrorInfo("unexpected token ','", 81, 7, 16, 82, 7, 17),
                    new ErrorInfo("unexpected token 'baz'", 83, 7, 18, 86, 7, 21),
                    new ErrorInfo("unexpected token 'quox'", 90, 7, 25, 94, 7, 29),
                    new ErrorInfo("unexpected token ':'", 94, 7, 29, 95, 7, 30),
                    new ErrorInfo("unexpected token 'pass'", 96, 7, 31, 100, 7, 35)
                );
            }
        }

        [TestMethod]
        public void Semicolon() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("Semicolon.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckSuite(
                            CheckConstantStmt(1),
                            CheckConstantStmt(2),
                            CheckConstantStmt(3)
                        ),
                        CheckSuite(
                            CheckNameStmt("foo"),
                            CheckNameStmt("bar"),
                            CheckNameStmt("baz")
                        )
                    )
                );
            }
        }

        [TestMethod]
        public void DelStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("DelStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckDelStmt(Foo),
                        CheckDelStmt(Foo, Bar),
                        CheckDelStmt(CheckMemberExpr(Foo, "bar")),
                        CheckDelStmt(CheckIndexExpression(Foo, Bar)),
                        CheckDelStmt(CheckTupleExpr(Foo, Bar)),
                        CheckDelStmt(CheckListExpr(Foo, Bar)),
                        CheckDelStmt(CheckParenExpr(Foo))
                    )
                );
            }
        }

        [TestMethod]
        public void IndexExpr() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("IndexExpr.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckIndexExpression(Foo, CheckConstant(.2)))
                    )
                );
            }
        }

        [TestMethod]
        public void DelStmtIllegal() {
            foreach (var version in AllVersions) {
                ParseErrors("DelStmtIllegal.py", version,
                    new ErrorInfo("can't delete literal", 4, 1, 5, 5, 1, 6),
                    new ErrorInfo("can't delete generator expression", 11, 2, 5, 31, 2, 25),
                    new ErrorInfo("can't delete function call", 37, 3, 5, 45, 3, 13)
                );
            }
        }

        [TestMethod]
        public void YieldStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("YieldStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters, 
                            CheckSuite(
                                CheckYieldStmt(One),
                                CheckYieldStmt(CheckTupleExpr(One, Two))
                            )
                        )
                    )
                );
            }
        }

        [TestMethod]
        public void YieldExpr() {
            foreach (var version in V25AndUp) {
                CheckAst(
                    ParseFile("YieldExpr.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters,
                            CheckSuite(
                                CheckYieldStmt(None)
                            )
                        )
                    )
                );
            }

            ParseErrors("YieldExpr.py", PythonLanguageVersion.V24, 
                new ErrorInfo("invalid syntax", 19, 2, 10, 19, 2, 10)
            );
        }

        [TestMethod]
        public void YieldStmtIllegal() {
            foreach (var version in AllVersions) {
                ParseErrors("YieldStmtIllegal.py", version,
                    new ErrorInfo("misplaced yield", 0, 1, 1, 5, 1, 6),
                    new ErrorInfo("'return' with argument inside generator", 25, 4, 5, 34, 4, 14),
                    new ErrorInfo("'return' with argument inside generator", 78, 9, 5, 87, 9, 14)
                );
            }
        }

        [TestMethod]
        public void ImportStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("ImportStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckImport(new[] { "sys" }),
                        CheckImport(new[] { "sys", "foo" }),
                        CheckImport(new[] { "sys" }, new[] { "bar" }),
                        CheckImport(new[] { "sys", "foo" }, new[] { "bar", "baz" }),
                        CheckImport(new[] { "sys.foo" }),
                        CheckImport(new[] { "sys.foo" }, new[] { "bar" })
                    )
                );
            }

            foreach (var version in AllVersions) {
                ParseErrors("ImportStmtIllegal.py", version,
                    new ErrorInfo("unexpected token '('", 17, 1, 18, 18, 1, 19),
                    new ErrorInfo("unexpected token '('", 17, 1, 18, 18, 1, 19),
                    new ErrorInfo("unexpected token '('", 17, 1, 18, 18, 1, 19),
                    new ErrorInfo("unexpected token ')'", 24, 1, 25, 25, 1, 26),
                    new ErrorInfo("unexpected token ')'", 25, 1, 26, 26, 1, 27)
                );
            }
        }

        [TestMethod]
        public void GlobalStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("GlobalStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters,
                            CheckSuite(
                                CheckGlobal("a"),
                                CheckGlobal("a", "b")
                            )
                        )
                    )
                );
            }
        }

        [TestMethod]
        public void NonlocalStmt() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("NonlocalStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("g", NoParameters,
                            CheckSuite(
                                CheckAssignment(Foo, One),
                                CheckAssignment(Bar, One),
                                CheckFuncDef("f", NoParameters,
                                    CheckSuite(
                                        CheckNonlocal("foo"),
                                        CheckNonlocal("foo", "bar")
                                    )
                                )
                            )
                        ),
                        CheckFuncDef("g", NoParameters,
                            CheckSuite(
                                CheckFuncDef("f", NoParameters,
                                    CheckSuite(
                                        CheckNonlocal("foo")
                                    )
                                ),
                                CheckAssignment(Foo, One)
                            )
                        ),
                        CheckFuncDef("f", NoParameters,                            
                            CheckSuite(
                                CheckClassDef("C", 
                                    CheckSuite(
                                        CheckNonlocal("foo"),
                                        CheckAssignment(Foo, One)
                                    )
                                ),
                                CheckAssignment(Foo, Two)
                            )
                        )
                    )
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("NonlocalStmt.py", version,
                    new ErrorInfo("unexpected token 'foo'", 67, 5, 18, 70, 5, 21),
                    new ErrorInfo("unexpected token '<newline>'", 70, 5, 21, 80, 6, 9),
                    new ErrorInfo("unexpected token 'nonlocal'", 80, 6, 9, 88, 6, 17),
                    new ErrorInfo("unexpected token 'foo'", 144, 11, 18, 147, 11, 21),
                    new ErrorInfo("unexpected token '<newline>'", 147, 11, 21, 149, 12, 1),
                    new ErrorInfo("unexpected token '<NL>'", 149, 12, 1, 155, 13, 5),
                    new ErrorInfo("unexpected token 'foo'", 209, 18, 18, 212, 18, 21),
                    new ErrorInfo("unexpected token '<newline>'", 212, 18, 21, 222, 19, 9),
                    new ErrorInfo("unexpected token 'foo'", 222, 19, 9, 225, 19, 12),
                    new ErrorInfo("unexpected token '='", 226, 19, 13, 227, 19, 14),
                    new ErrorInfo("unexpected token '1'", 228, 19, 15, 229, 19, 16),
                    new ErrorInfo("unexpected token '<newline>'", 229, 19, 16, 235, 20, 5),
                    new ErrorInfo("unexpected token '<dedent>'", 229, 19, 16, 235, 20, 5),
                    new ErrorInfo("unexpected end of file", 244, 21, 1, 244, 21, 1)
                );
            }
        }

        [TestMethod]
        public void NonlocalStmtIllegal() {
            foreach (var version in V3Versions) {
                ParseErrors("NonlocalStmtIllegal.py", version,
                    new ErrorInfo("nonlocal declaration not allowed at module level", 195, 17, 1, 203, 17, 9),
                    new ErrorInfo("name 'x' is nonlocal and global", 118, 10, 13, 128, 10, 23),
                    new ErrorInfo("name 'x' is a parameter and nonlocal", 181, 15, 13, 191, 15, 23),
                    new ErrorInfo("no binding for nonlocal 'x' found", 375, 34, 9, 407, 35, 23),
                    new ErrorInfo("no binding for nonlocal 'x' found", 285, 26, 2, 307, 27, 13),
                    new ErrorInfo("no binding for nonlocal 'globalvar' found", 227, 20, 1, 259, 21, 23),
                    new ErrorInfo("no binding for nonlocal 'a' found", 14, 2, 5, 42, 3, 19)
                );
            }

        }

        [TestMethod]
        public void WhileStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("WhileStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckWhileStmt(One, CheckSuite(Pass)),
                        CheckWhileStmt(One, CheckSuite(Pass), CheckSuite(Pass))
                    )
                );
            }
        }

        [TestMethod]
        public void TryStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("TryStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckTryStmt(
                            CheckSuite(Pass), 
                            new[] { CheckHandler(null, null, CheckSuite(Pass)) }
                        ),
                        CheckTryStmt(
                            CheckSuite(Pass),
                            new[] { CheckHandler(Exception, null, CheckSuite(Pass)) }
                        )
                    )
                );
            }

            // execpt Exception as e: vs except Exception, e:
            // comma supported in 2.4/2.5, both supported in 2.6 - 2.7, as supported in 3.x
            foreach (var version in V24_V25Versions) {
                TryStmtV2(version);

                ParseErrors(
                    "TryStmtV3.py", version,
                    new ErrorInfo("'as' requires Python 2.6 or later", 33, 3, 18, 35, 3, 20)
                );
            }

            foreach(var version in V26_V27Versions) {
                TryStmtV2(version);
                TryStmtV3(version);
            }

            foreach (var version in V3Versions) {
                TryStmtV3(version);

                ParseErrors(
                    "TryStmtV2.py", version,
                    new ErrorInfo("\", variable\" not allowed in 3.x - use \"as variable\" instead.", 32, 3, 17, 35, 3, 20)
                );
            }
        }

        private void TryStmtV3(PythonLanguageVersion version) {
            CheckAst(
                ParseFile("TryStmtV3.py", ErrorSink.Null, version),
                CheckSuite(
                    CheckTryStmt(
                        CheckSuite(Pass),
                        new[] { CheckHandler(Exception, CheckNameExpr("e"), CheckSuite(Pass)) }
                    )
                )
            );
        }

        private void TryStmtV2(PythonLanguageVersion version) {
            CheckAst(
                ParseFile("TryStmtV2.py", ErrorSink.Null, version),
                CheckSuite(
                    CheckTryStmt(
                        CheckSuite(Pass),
                        new[] { CheckHandler(Exception, CheckNameExpr("e"), CheckSuite(Pass)) }
                    )
                )
            );
        }

        [TestMethod]
        public void RaiseStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("RaiseStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckRaiseStmt(),
                        CheckRaiseStmt(Foo)
                    )
                );
            }

            foreach(var version in V2Versions) {
                CheckAst(
                    ParseFile("RaiseStmtV2.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckRaiseStmt(Foo, Bar),
                        CheckRaiseStmt(Foo, Bar, Baz)
                    )
                );

                ParseErrors(
                    "RaiseStmtV3.py", version,
                    new ErrorInfo("invalid syntax, from cause not allowed in 2.x.", 10, 1, 11, 18, 1, 19)
                );
            }

            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("RaiseStmtV3.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckRaiseStmt(Foo, cause: Bar)
                    )
                );

                ParseErrors(
                    "RaiseStmtV2.py", version,
                    new ErrorInfo("invalid syntax, only exception value is allowed in 3.x.", 9, 1, 10, 14, 1, 15),
                    new ErrorInfo("invalid syntax, only exception value is allowed in 3.x.", 25, 2, 10, 30, 2, 15)
                );
            }
        }

        [TestMethod]
        public void PrintStmt() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("PrintStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckPrintStmt(new Action<Expression>[0]),
                        CheckPrintStmt(new [] { One }),
                        CheckPrintStmt(new[] { One }, trailingComma: true),
                        CheckPrintStmt(new[] { One, Two }),
                        CheckPrintStmt(new[] { One, Two }, trailingComma: true),
                        CheckPrintStmt(new[] { One, Two }, Foo),
                        CheckPrintStmt(new[] { One, Two }, Foo, trailingComma: true),
                        CheckPrintStmt(new Action<Expression>[0], Foo),
                        CheckPrintStmt(new[] { CheckBinaryExpression(One, PythonOperator.Equal, Two) }),
                        CheckPrintStmt(new[] { CheckLambda(new Action<Parameter>[0], One) })
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors(
                    "PrintStmt.py", version,
                    new ErrorInfo("invalid syntax", 13, 2, 7, 14, 2, 8),
                    new ErrorInfo("invalid syntax", 22, 3, 7, 23, 3, 8),
                    new ErrorInfo("invalid syntax", 32, 4, 7, 33, 4, 8),
                    new ErrorInfo("invalid syntax", 44, 5, 7, 45, 5, 8),
                    new ErrorInfo("invalid syntax", 110, 9, 7, 111, 9, 8),
                    new ErrorInfo("unexpected token 'lambda'", 124, 10, 7, 130, 10, 13),
                    new ErrorInfo("unexpected token ':'", 130, 10, 13, 131, 10, 14),
                    new ErrorInfo("invalid syntax", 132, 10, 15, 133, 10, 16)
                );
            }
        }

        [TestMethod]
        public void AssertStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("AssertStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssertStmt(One),
                        CheckAssertStmt(One, Foo)
                    )
                );
            }
        }

        [TestMethod]
        public void ListComp() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("ListComp.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckListComp(Foo, CompFor(Foo, Bar))),
                        CheckExprStmt(CheckListComp(Foo, CompFor(Foo, Bar), CompIf(Baz))),
                        CheckExprStmt(CheckListComp(Foo, CompFor(Foo, Bar), CompFor(Baz, Quox)))
                    )
                );
            }
        }

        [TestMethod]
        public void ListComp2x() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("ListComp2x.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckListComp(Foo, CompFor(Foo, CheckTupleExpr(Bar, Baz))))
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("ListComp2x.py", version,
                    new ErrorInfo("unexpected token ','", 19, 1, 20, 20, 1, 21),
                    new ErrorInfo("unexpected token ']'", 24, 1, 25, 25, 1, 26)
                );
            }
        }

        [TestMethod]
        public void GenComp() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("GenComp.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckGeneratorComp(Foo, CompFor(Foo, Bar))),
                        CheckExprStmt(CheckGeneratorComp(Foo, CompFor(Foo, Bar), CompIf(Baz))),
                        CheckExprStmt(CheckGeneratorComp(Foo, CompFor(Foo, Bar), CompFor(Baz, Quox))),
                        CheckCallStmt(Baz, PositionalArg(CheckGeneratorComp(Foo, CompFor(Foo, Bar))))
                    )                    
                );
            }
        }

        [TestMethod]
        public void DictComp() {
            foreach (var version in V27AndUp) {
                CheckAst(
                    ParseFile("DictComp.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckDictComp(Foo, Bar, CompFor(CheckTupleExpr(Foo, Bar), Baz))),
                        CheckExprStmt(CheckDictComp(Foo, Bar, CompFor(CheckTupleExpr(Foo, Bar), Baz), CompIf(Quox))),
                        CheckExprStmt(CheckDictComp(Foo, Bar, CompFor(CheckTupleExpr(Foo, Bar), Baz), CompFor(Quox, Exception)))
                    )
                );
            }

            foreach (var version in V24_V26Versions) {
                ParseErrors("DictComp.py", version,
                    new ErrorInfo("invalid syntax", 9, 1, 10, 12, 1, 13),
                    new ErrorInfo("invalid syntax", 39, 2, 10, 42, 2, 13),
                    new ErrorInfo("invalid syntax", 77, 3, 10, 80, 3, 13)
                );
            }
        }

        [TestMethod]
        public void SetComp() {
            foreach (var version in V27AndUp) {
                CheckAst(
                    ParseFile("SetComp.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckSetComp(Foo, CompFor(Foo, Baz))),
                        CheckExprStmt(CheckSetComp(Foo, CompFor(Foo, Baz), CompIf(Quox))),
                        CheckExprStmt(CheckSetComp(Foo, CompFor(Foo, Baz), CompFor(Quox, Exception)))
                    )
                );
            }

            foreach (var version in V24_V26Versions) {
                ParseErrors("SetComp.py", version,
                    new ErrorInfo("invalid syntax, set literals require Python 2.7 or later.", 1, 1, 2, 4, 1, 5),
                    new ErrorInfo("invalid syntax, set literals require Python 2.7 or later.", 23, 2, 2, 26, 2, 5),
                    new ErrorInfo("invalid syntax, set literals require Python 2.7 or later.", 53, 3, 2, 56, 3, 5)
                );
            }
        }

        [TestMethod]
        public void SetLiteral() {
            foreach (var version in V27AndUp) {
                CheckAst(
                    ParseFile("SetLiteral.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckSetLiteral(One)),
                        CheckExprStmt(CheckSetLiteral(One, Two))
                    )
                );
            }

            foreach (var version in V24_V26Versions) {
                ParseErrors("SetLiteral.py", version,
                    new ErrorInfo("invalid syntax, set literals require Python 2.7 or later.", 1, 1, 2, 2, 1, 3),
                    new ErrorInfo("invalid syntax, set literals require Python 2.7 or later.", 6, 2, 2, 7, 2, 3)
                );
            }
        }

        [TestMethod]
        public void IfStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("IfStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckIfStmt(IfTests(IfTest(One, CheckSuite(Pass)))),
                        CheckIfStmt(IfTests(IfTest(One, CheckSuite(Pass)), IfTest(Two, CheckSuite(Pass)))),
                        CheckIfStmt(IfTests(IfTest(One, CheckSuite(Pass))), CheckSuite(Pass))
                    )
                );
            }
        }

        [TestMethod]
        public void FromImportStmt() {

            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("FromImportStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFromImport("sys", new[] { "winver" }),
                        CheckFromImport("sys", new[] { "winver" }, new[] { "baz" }),
                        CheckFromImport("sys.foo", new[] { "winver" }),
                        CheckFromImport("sys.foo", new[] { "winver" }, new[] { "baz" }),
                        CheckFromImport("...foo", new[] { "bar" }),
                        CheckFromImport("....foo", new[] { "bar" }),
                        CheckFromImport("......foo", new[] { "bar" }),
                        CheckFromImport(".......foo", new[] { "bar" }),
                        CheckFromImport("foo", new[] { "foo", "baz" }, new string[] { "bar", "quox"})
                    )
                );
            }

            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("FromImportStmtV2.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters, 
                            CheckSuite(CheckFromImport("sys", new[] { "*" }))
                        ),
                        CheckClassDef("C",
                            CheckSuite(CheckFromImport("sys", new[] { "*" }))
                        )
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors(
                    "FromImportStmtV2.py", 
                    version,
                    new ErrorInfo("import * only allowed at module level", 14, 2, 5, 31, 2, 22),
                    new ErrorInfo("import * only allowed at module level", 49, 5, 5, 66, 5, 22)
                );
            }
        }

        [TestMethod]
        public void FromImportStmtIllegal() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("FromImportStmtIllegal.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFromImport("", new[] { "foo" })
                    )
                );

                ParseErrors(
                    "FromImportStmtIllegal.py",
                    version,
                    new ErrorInfo("missing module name", 5, 1, 6, 11, 1, 12)
                );
            }
        }
        
        [TestMethod]
        public void FromImportStmtIncomplete() {

            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("FromImportStmtIncomplete.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef(
                            "f",
                            NoParameters,
                            CheckSuite(
                                CheckFromImport("sys", new[] { "abc", "" })
                            )
                        )
                    )
                );

                ParseErrors(
                    "FromImportStmtIncomplete.py",
                    version,
                    new ErrorInfo("unexpected token '<newline>'", 35, 2, 26, 37, 3, 1)
                );
            }
        }

        [TestMethod]
        public void DecoratorsFuncDef() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("DecoratorsFuncDef.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { Foo }),
                        CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { CheckMemberExpr(Foo, "bar") }),
                        CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { CheckCallExpression(Foo, PositionalArg(Bar)) }),
                        CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { Foo, Bar })
                    )
                );
            }
        }

        [TestMethod]
        public void DecoratorsClassDef() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("DecoratorsClassDef.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckClassDef("C", CheckSuite(Pass), decorators: new[] { Foo }),
                        CheckClassDef("C", CheckSuite(Pass), decorators: new[] { CheckMemberExpr(Foo, "bar") }),
                        CheckClassDef("C", CheckSuite(Pass), decorators: new[] { CheckCallExpression(Foo, PositionalArg(Bar)) }),
                        CheckClassDef("C", CheckSuite(Pass), decorators: new[] { Foo, Bar })
                    )
                );
            }

            foreach (var version in V24_V25Versions) {
                ParseErrors("DecoratorsClassDef.py", 
                    version,
                    new ErrorInfo("invalid syntax, class decorators require 2.6 or later.", 6, 2, 1, 11, 2, 6),
                    new ErrorInfo("invalid syntax, class decorators require 2.6 or later.", 33, 5, 1, 38, 5, 6),
                    new ErrorInfo("invalid syntax, class decorators require 2.6 or later.", 63, 9, 1, 68, 9, 6),
                    new ErrorInfo("invalid syntax, class decorators require 2.6 or later.", 92, 13, 1, 97, 13, 6)
                );
            }
        }

        [TestMethod]
        public void DecoratorsIllegal() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("DecoratorsIllegal.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckErrorStmt(),
                        CheckAssignment(Foo, One)
                    )
                );
            }

            foreach (var version in AllVersions) {
                ParseErrors("DecoratorsIllegal.py",
                    version,
                    new ErrorInfo("unexpected token 'foo'", 6, 2, 1, 9, 2, 4)
                );
            }
        }

        [TestMethod]
        public void Calls() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("Calls.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckCallStmt(Foo),
                        CheckCallStmt(Foo, PositionalArg(One)),
                        CheckCallStmt(Foo, NamedArg("bar", One)),
                        CheckCallStmt(Foo, ListArg(Bar)),
                        CheckCallStmt(Foo, DictArg(Bar)),
                        CheckCallStmt(Foo, ListArg(Bar), DictArg(Baz)),
                        CheckCallStmt(Foo, NamedArg("bar", One), NamedArg("baz", Two)),
                        CheckCallStmt(Foo, PositionalArg(Bar), PositionalArg(Baz))
                    )
                );
            }
        }

        [TestMethod]
        public void CallsIllegal() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("CallsIllegal.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckCallStmt(Foo, NamedArg("bar", One), NamedArg("bar", Two)),
                        CheckCallStmt(Foo, NamedArg(null, Two))
                    )
                );
            }

            foreach (var version in AllVersions) {
                ParseErrors("CallsIllegal.py",
                    version,
                    new ErrorInfo("duplicate keyword argument", 20, 1, 21, 21, 1, 22),
                    new ErrorInfo("expected name", 27, 2, 5, 28, 2, 6)
                );
            }
        }

        [TestMethod]
        public void LambdaExpr() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("LambdaExpr.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckLambdaStmt(new[] { CheckParameter("x") }, One),
                        CheckLambdaStmt(new[] { CheckParameter("x", ParameterKind.List) }, One),
                        CheckLambdaStmt(new[] { CheckParameter("x", ParameterKind.Dictionary) }, One)
                    )
                );
            }
        }

        [TestMethod]
        public void FuncDef() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("FuncDef.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters, CheckSuite(Pass)),
                        CheckFuncDef("f", new [] { CheckParameter("a") }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a"), CheckParameter("b", ParameterKind.List) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a"), CheckParameter("b", ParameterKind.List), CheckParameter("c", ParameterKind.Dictionary) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.List) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.Dictionary) }, CheckSuite(Pass)),

                        CheckFuncDef("f", NoParameters, CheckSuite(CheckReturnStmt(One))),
                        CheckFuncDef("f", NoParameters, CheckSuite(CheckReturnStmt()))
                    )
                );
            }
        }

        [TestMethod]
        public void FuncDefV2() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("FuncDefV2.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", new[] { CheckParameter("a"), CheckSublistParameter("b", "c"), CheckParameter("d") }, CheckSuite(Pass))
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("FuncDefV2.py", version,
                    new ErrorInfo("sublist parameters are not supported in 3.x",  9, 1, 10, 14, 1, 15)
                );
            }
        }

        [TestMethod]
        public void FuncDefV3() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("FuncDefV3.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.List), CheckParameter("x", ParameterKind.KeywordOnly) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.List), CheckParameter("x", ParameterKind.KeywordOnly, defaultValue: One) }, CheckSuite(Pass)),

                        CheckFuncDef("f", new[] { CheckParameter("a", annotation: One) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.List, annotation: One) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.Dictionary, annotation: One) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", annotation: Zero), CheckParameter("b", ParameterKind.List, annotation: One), CheckParameter("c", ParameterKind.Dictionary, annotation: Two) }, CheckSuite(Pass)),

                        CheckFuncDef("f", NoParameters, CheckSuite(Pass), returnAnnotation: One),

                        CheckFuncDef("f", new[] { CheckParameter("a", annotation: One) }, CheckSuite(Pass), returnAnnotation: One),

                        CheckFuncDef("f", new[] { CheckParameter("a", defaultValue: Two, annotation: One) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.KeywordOnly) }, CheckSuite(Pass))

                    )
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("FuncDefV3.py", version,
                    new ErrorInfo("positional parameter after * args not allowed", 10, 1, 11, 11, 1, 12),
                    new ErrorInfo("positional parameter after * args not allowed", 30, 2, 11, 35, 2, 16),
                    new ErrorInfo("invalid syntax, parameter annotations require 3.x", 53, 4, 8, 56, 4, 11),
                    new ErrorInfo("invalid syntax, parameter annotations require 3.x", 72, 5, 8, 74, 5, 10),
                    new ErrorInfo("invalid syntax, parameter annotations require 3.x", 93, 6, 9, 95, 6, 11),
                    new ErrorInfo("invalid syntax, parameter annotations require 3.x", 113, 7, 8, 116, 7, 11),
                    new ErrorInfo("invalid syntax, parameter annotations require 3.x", 119, 7, 14, 121, 7, 16),
                    new ErrorInfo("invalid syntax, parameter annotations require 3.x", 127, 7, 22, 129, 7, 24),
                    new ErrorInfo("unexpected token '-'", 151, 9, 9, 152, 9, 10),
                    new ErrorInfo("unexpected token '>'", 152, 9, 10, 153, 9, 11),
                    new ErrorInfo("invalid syntax", 154, 9, 12, 155, 9, 13),
                    new ErrorInfo("unexpected token ':'", 155, 9, 13, 156, 9, 14),
                    new ErrorInfo("invalid syntax, parameter annotations require 3.x", 172, 11, 8, 175, 11, 11),
                    new ErrorInfo("unexpected token '-'", 177, 11, 13, 178, 11, 14),
                    new ErrorInfo("unexpected token '>'", 178, 11, 14, 179, 11, 15),
                    new ErrorInfo("invalid syntax", 180, 11, 16, 181, 11, 17),
                    new ErrorInfo("unexpected token ':'", 181, 11, 17, 182, 11, 18),
                    new ErrorInfo("invalid syntax, parameter annotations require 3.x", 198, 13, 8, 201, 13, 11),
                    new ErrorInfo("unexpected token ','", 223, 15, 8, 224, 15, 9),
                    new ErrorInfo("unexpected token 'a'", 225, 15, 10, 226, 15, 11),
                    new ErrorInfo("unexpected token 'a'", 225, 15, 10, 226, 15, 11),
                    new ErrorInfo("unexpected token ')'", 226, 15, 11, 227, 15, 12),
                    new ErrorInfo("unexpected token ':'", 227, 15, 12, 228, 15, 13)
                );
            }
        }

        [TestMethod]
        public void FuncDefV3Illegal() {
            foreach (var version in V3Versions) {
                ParseErrors("FuncDefV3Illegal.py", version,
                    new ErrorInfo("unexpected token ')'", 7, 1, 8, 8, 1, 9),
                    new ErrorInfo("unexpected token ':'", 8, 1, 9, 9, 1, 10),
                    new ErrorInfo("named arguments must follow bare *", 22, 2, 7, 26, 2, 11)
                );
            }
        }

        [TestMethod]
        public void ClassDef() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("ClassDef.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckClassDef("C", CheckSuite(Pass)),
                        CheckClassDef("C", CheckSuite(Pass), new[] { CheckArg("object") }),
                        CheckClassDef("C", CheckSuite(Pass), new[] { CheckArg("list"), CheckArg("object") }),
                        CheckClassDef("C", 
                            CheckSuite(
                                CheckClassDef("_C__D", 
                                    CheckSuite(
                                        CheckClassDef("_D__E", 
                                            CheckSuite(Pass)
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
            }
        }

        [TestMethod]
        public void ClassDef3x() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("ClassDef3x.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckClassDef("C", CheckSuite(Pass), new[] { CheckNamedArg("metaclass", One) }),
                        CheckClassDef("C", CheckSuite(Pass), new[] { CheckArg("object"), CheckNamedArg("metaclass", One) }),
                        CheckClassDef("C", CheckSuite(Pass), new[] { CheckArg("list"), CheckArg("object"), CheckNamedArg("foo", One) })
                    )
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("ClassDef3x.py", version,
                    new ErrorInfo("unexpected token '='", 17, 1, 18, 18, 1, 19),
                    new ErrorInfo("unexpected token '='", 17, 1, 18, 18, 1, 19),
                    new ErrorInfo("unexpected token ')'", 19, 1, 20, 20, 1, 21),
                    new ErrorInfo("unexpected token ':'", 20, 1, 21, 21, 1, 22),
                    new ErrorInfo("unexpected token 'pass'", 22, 1, 23, 26, 1, 27),
                    new ErrorInfo("unexpected token '='", 53, 2, 26, 54, 2, 27),
                    new ErrorInfo("unexpected token '='", 53, 2, 26, 54, 2, 27),
                    new ErrorInfo("unexpected token ')'", 55, 2, 28, 56, 2, 29),
                    new ErrorInfo("unexpected token ':'", 56, 2, 29, 57, 2, 30),
                    new ErrorInfo("unexpected token 'pass'", 58, 2, 31, 62, 2, 35),
                    new ErrorInfo("unexpected token '='", 89, 3, 26, 90, 3, 27),
                    new ErrorInfo("unexpected token '='", 89, 3, 26, 90, 3, 27),
                    new ErrorInfo("unexpected token ')'", 91, 3, 28, 92, 3, 29),
                    new ErrorInfo("unexpected token ':'", 92, 3, 29, 93, 3, 30),
                    new ErrorInfo("unexpected token 'pass'", 94, 3, 31, 98, 3, 35)
                );
            }
        }

        [TestMethod]
        public void AssignStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("AssignStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(CheckIndexExpression(Foo, One), Two),
                        CheckAssignment(CheckMemberExpr(Foo, "bar"), One),
                        CheckAssignment(Foo, One),
                        CheckAssignment(CheckParenExpr(Foo), One),
                        CheckAssignment(CheckTupleExpr(Foo, Bar), CheckTupleExpr(One, Two)),
                        CheckAssignment(CheckTupleExpr(Foo, Bar), CheckTupleExpr(One, Two)),
                        CheckAssignment(CheckTupleExpr(Foo, Bar), Baz),
                        CheckAssignment(CheckListExpr(Foo, Bar), CheckTupleExpr(One, Two)),
                        CheckAssignment(CheckListExpr(Foo, Bar), Baz),
                        CheckAssignment(new [] { Foo, Bar }, Baz)
                    )   
                );
            }
        }

        [TestMethod]
        public void AssignStmt2x() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("AssignStmt2x.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(Foo, CheckUnaryExpression(PythonOperator.Negate, CheckBinaryExpression(CheckConstant((BigInteger)2), PythonOperator.Power, CheckConstant(31))))
                    )
                );
                ParseErrors("AssignStmt2x.py", version);
            }
        }

        [TestMethod]
        public void AssignStmt25() {
            foreach (var version in V25AndUp) {
                CheckAst(
                    ParseFile("AssignStmt25.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef(
                            "f",
                            NoParameters,
                            CheckSuite(
                                CheckAssignment(Foo, CheckYieldExpr(One)),
                                CheckAssignment(Foo, PythonOperator.Add, CheckYieldExpr(One))
                            )
                        )
                    )
                );
            }

            ParseErrors("AssignStmt25.py", PythonLanguageVersion.V24,
                new ErrorInfo("unexpected token 'yield'", 20, 2, 11, 25, 2, 16),
                new ErrorInfo("invalid syntax", 26, 2, 17, 27, 2, 18),
                new ErrorInfo("unexpected token 'yield'", 40, 3, 12, 45, 3, 17),
                new ErrorInfo("invalid syntax", 46, 3, 18, 47, 3, 19)
            );
        }
        
        [TestMethod]
        public void AssignStmtV3() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("AssignStmtV3.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment( CheckTupleExpr(CheckStarExpr(Foo), Bar, Baz), CheckTupleExpr(One, Two, Three, Four)),
                        CheckAssignment( CheckTupleExpr(Foo, CheckStarExpr(Bar), Baz), CheckTupleExpr(One, Two, Three, Four) ),
                        CheckAssignment( CheckListExpr(Foo, CheckStarExpr(Bar), Baz), CheckTupleExpr(One, Two, Three, Four) ),
                        CheckAssignment( CheckListExpr(CheckStarExpr(Foo), Bar, Baz), CheckTupleExpr(One, Two, Three, Four) )
                    )
                );                
            }

            foreach (var version in V2Versions) {
                ParseErrors("AssignStmtV3.py", version,
                    new ErrorInfo("invalid syntax", 1, 1, 2, 4, 1, 5),
                    new ErrorInfo("invalid syntax", 35, 2, 7, 38, 2, 10),
                    new ErrorInfo("invalid syntax", 65, 3, 8, 68, 3, 11),
                    new ErrorInfo("invalid syntax", 91, 4, 3, 94, 4, 6)
                );
            }
        }

        [TestMethod]
        public void AssignStmtIllegalV3() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("AssignStmtIllegalV3.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(CheckTupleExpr(Foo, CheckStarExpr(Bar), CheckStarExpr(Baz)), CheckTupleExpr(One, Two, Three, Four))
                    )
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("AssignStmtIllegalV3.py", version,
                    new ErrorInfo("invalid syntax", 6, 1, 7, 9, 1, 10),
                    new ErrorInfo("invalid syntax", 12, 1, 13, 15, 1, 16)
                );
            }
        }

        [TestMethod]
        public void AssignStmtIllegal() {
           foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("AssignStmtIllegal.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(CheckBinaryExpression(Foo, PythonOperator.Add, Bar), One),
                        CheckAssignment(CheckCallExpression(Foo), One),
                        CheckAssignment(None, One),
                        CheckAssignment(Two, One),
                        CheckAssignment(CheckGeneratorComp(Foo, CompFor(Foo, Bar)), One),
                        CheckAssignment(CheckTupleExpr(Foo, Bar), PythonOperator.Add, One),
                        CheckFuncDef("f", NoParameters, 
                            CheckSuite(    
                                CheckAssignment(CheckYieldExpr(Foo), One)
                            )
                        )
                    )
                );
            }

            foreach (var version in AllVersions) {
                ParseErrors("AssignStmtIllegal.py", version,
                    new ErrorInfo("can't assign to binary operator", 0, 1, 1, 9, 1, 10),
                    new ErrorInfo("can't assign to function call", 15, 2, 1, 20, 2, 6),
                    new ErrorInfo("assignment to None", 26, 3, 1, 30, 3, 5),
                    new ErrorInfo("can't assign to literal", 36, 4, 1, 37, 4, 2),
                    new ErrorInfo("can't assign to generator expression", 43, 5, 1, 63, 5, 21),
                    new ErrorInfo("illegal expression for augmented assignment", 69, 6, 1, 77, 6, 9),
                    new ErrorInfo("can't assign to yield expression", 98, 8, 5, 109, 8, 16)
                );
            }
        }

        [TestMethod]
        public void ExecStmt() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("ExecStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExecStmt(Foo),
                        CheckExecStmt(Foo, Bar),
                        CheckExecStmt(Foo, Bar, Baz)
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("ExecStmt.py", version,
                    new ErrorInfo("unexpected token 'foo'", 5, 1, 6, 8, 1, 9),
                    new ErrorInfo("unexpected token 'foo'", 15, 2, 6, 18, 2, 9),
                    new ErrorInfo("unexpected token 'in'", 19, 2, 10, 21, 2, 12),
                    new ErrorInfo("unexpected token 'bar'", 22, 2, 13, 25, 2, 16),
                    new ErrorInfo("unexpected token 'foo'", 32, 3, 6, 35, 3, 9),
                    new ErrorInfo("unexpected token 'in'", 36, 3, 10, 38, 3, 12),
                    new ErrorInfo("unexpected token 'bar'", 39, 3, 13, 42, 3, 16),
                    new ErrorInfo("unexpected token ','", 42, 3, 16, 43, 3, 17),
                    new ErrorInfo("unexpected token 'baz'", 44, 3, 18, 47, 3, 21)
                );
            }

        }

        [TestMethod]
        public void EllipsisExpr() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("Ellipsis.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckCallStmt(Foo, PositionalArg(Ellipsis)),
                        CheckBinaryStmt(One, PythonOperator.Add, Ellipsis)
                    )
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("Ellipsis.py", version,
                    new ErrorInfo("unexpected token '.'", 4, 1, 5, 5, 1, 6),
                    new ErrorInfo("syntax error", 6, 1, 7, 7, 1, 8),
                    new ErrorInfo("syntax error", 7, 1, 8, 8, 1, 9),
                    new ErrorInfo("unexpected token '.'", 14, 2, 5, 15, 2, 6),
                    new ErrorInfo("syntax error", 16, 2, 7, 17, 2, 8),
                    new ErrorInfo("syntax error", 17, 2, 8, 19, 3, 1)
                );
            }
        }

        [TestMethod]
        public void FromFuture() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("FromFuture24.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFromImport("__future__", new[] { "division" }),
                        CheckFromImport("__future__", new[] { "generators" })
                    )
                );

                if (version == PythonLanguageVersion.V24) {
                    ParseErrors("FromFuture25.py", version,
                        new ErrorInfo("future feature is not defined: with_statement", 0, 1, 1, 37, 1, 38),
                        new ErrorInfo("future feature is not defined: absolute_import", 39, 2, 1, 77, 2, 39)
                    );
                } else {
                    CheckAst(
                        ParseFile("FromFuture25.py", ErrorSink.Null, version),
                        CheckSuite(
                            CheckFromImport("__future__", new[] { "with_statement" }),
                            CheckFromImport("__future__", new[] { "absolute_import" })
                        )
                    );
                }

                if (version == PythonLanguageVersion.V24 || version == PythonLanguageVersion.V25) {
                    ParseErrors("FromFuture26.py", version,
                        new ErrorInfo("future feature is not defined: print_function", 0, 1, 1, 37, 1, 38),
                        new ErrorInfo("future feature is not defined: unicode_literals", 39, 2, 1, 78, 2, 40)
                    );
                } else {
                    CheckAst(
                        ParseFile("FromFuture26.py", ErrorSink.Null, version),
                        CheckSuite(
                            CheckFromImport("__future__", new[] { "print_function" }),
                            CheckFromImport("__future__", new[] { "unicode_literals" })
                        )
                    );
                }
            }
        }

        [TestMethod,  Priority(2)]
        public void StdLib() {
            var versions = new[] { 
                new { Path = "C:\\Python32\\Lib", Version = PythonLanguageVersion.V32 },
                new { Path = "C:\\Python24\\Lib", Version = PythonLanguageVersion.V24 },
                new { Path = "C:\\Python25\\Lib", Version = PythonLanguageVersion.V25 },
                new { Path = "C:\\Python26\\Lib", Version = PythonLanguageVersion.V26 },
                new { Path = "C:\\Python27\\Lib", Version = PythonLanguageVersion.V27 },
                
                new { Path = "C:\\Python30\\Lib", Version = PythonLanguageVersion.V30 },
                new { Path = "C:\\Python31\\Lib", Version = PythonLanguageVersion.V31 }
                
            };

            foreach (var curVersion in versions) {
                string dir = Path.Combine(curVersion.Path);
                List<string> files = new List<string>();
                CollectFiles(dir, files);

                var skippedFiles = new HashSet<string>(new[] { 
                    "py3_test_grammar.py",  // included in 2x distributions but includes 3x grammar
                    "py2_test_grammar.py",  // included in 3x distributions but includes 2x grammar
                    "proxy_base.py"         // included in Qt port to Py3k but installed in 2.x distributions
                });
                var errorSink = new CollectingErrorSink();
                var errors = new Dictionary<string, CollectingErrorSink>();
                foreach (var file in files) {
                    string filename = Path.GetFileName(file);
                    if (skippedFiles.Contains(filename) || filename.StartsWith("badsyntax_") || file.IndexOf("\\lib2to3\\tests\\") != -1) {
                        continue;
                    }
                    using (var parser = Parser.CreateParser(new StreamReader(file), curVersion.Version, new ParserOptions() { ErrorSink = errorSink })) {
                        var ast = parser.ParseFile();
                    }

                    if (errorSink.Errors.Count != 0) {
                        errors["\"" + file + "\""] = errorSink;
                        errorSink = new CollectingErrorSink();
                    }
                }

                if (errors.Count != 0) {
                    StringBuilder errorList = new StringBuilder();
                    foreach (var keyValue in errors) {
                        errorList.Append(keyValue.Key + " :" + Environment.NewLine);
                        foreach (var error in keyValue.Value.Errors) {
                            errorList.AppendFormat("     {0} {1}{2}", error.Span, error.Message, Environment.NewLine);
                        }

                    }
                    Assert.AreEqual(0, errors.Count, errorList.ToString());
                }
            }
        }

        #endregion

        #region Checker Factories / Helpers

        class ErrorInfo {
            public readonly string Message;
            public readonly SourceSpan Span;

            public ErrorInfo(string msg, int startIndex, int startLine, int startCol, int endIndex, int endLine, int endCol) {
                Message = msg;
                Span = new SourceSpan(new SourceLocation(startIndex, startLine, startCol), new SourceLocation(endIndex, endLine, endCol));
            }
        }

        private void ParseErrors(string filename, PythonLanguageVersion version, params ErrorInfo[] errors) {
            ParseErrors(filename, version, Severity.Ignore, errors);
        }

        private void ParseErrors(string filename, PythonLanguageVersion version, Severity indentationInconsistencySeverity, params ErrorInfo[] errors) {
            var sink = new CollectingErrorSink();
            ParseFile(filename, sink, version, indentationInconsistencySeverity);

            StringBuilder foundErrors = new StringBuilder();
            for (int i = 0; i < sink.Errors.Count; i++) {
                foundErrors.AppendFormat("new ErrorInfo(\"{0}\", {1}, {2}, {3}, {4}, {5}, {6})," + Environment.NewLine,
                    sink.Errors[i].Message,
                    sink.Errors[i].Span.Start.Index,
                    sink.Errors[i].Span.Start.Line,
                    sink.Errors[i].Span.Start.Column,
                    sink.Errors[i].Span.End.Index,
                    sink.Errors[i].Span.End.Line,
                    sink.Errors[i].Span.End.Column
                );
            }

            string finalErrors = foundErrors.ToString();
            Assert.AreEqual(errors.Length, sink.Errors.Count, "Unexpected errors: " + Environment.NewLine + finalErrors);

            for (int i = 0; i < errors.Length; i++) {
                if (sink.Errors[i].Message != errors[i].Message) {
                    Assert.Fail("Wrong msg for error {0}: expected {1}, got {2}", i, errors[i].Message, sink.Errors[i].Message);
                }
                if (sink.Errors[i].Span != errors[i].Span) {
                    Assert.Fail("Wrong span for error {0}: expected ({1}, {2}, {3} - {4}, {5}, {6}), got ({7}, {8}, {9}, {10}, {11}, {12})",
                        i,
                        errors[i].Span.Start.Index,
                        errors[i].Span.Start.Line,
                        errors[i].Span.Start.Column,
                        errors[i].Span.End.Index,
                        errors[i].Span.End.Line,
                        errors[i].Span.End.Column,
                        sink.Errors[i].Span.Start.Index,
                        sink.Errors[i].Span.Start.Line,
                        sink.Errors[i].Span.Start.Column,
                        sink.Errors[i].Span.End.Index,
                        sink.Errors[i].Span.End.Line,
                        sink.Errors[i].Span.End.Column
                    );
                }
            }
        }

        private static PythonAst ParseFile(string filename, ErrorSink errorSink, PythonLanguageVersion version, Severity indentationInconsistencySeverity = Severity.Ignore) {
            var parser = Parser.CreateParser(new StreamReader(Path.GetFullPath(Path.Combine("Python.VS.TestData\\Grammar", filename))), version, new ParserOptions() { ErrorSink = errorSink, IndentationInconsistencySeverity = indentationInconsistencySeverity });
            var ast = parser.ParseFile();
            return ast;
        }

        private void CheckAst(PythonAst ast, Action<Statement> checkBody) {
            checkBody(ast.Body);
        }

        private static Action<Expression> Zero = CheckConstant(0);
        private static Action<Expression> One = CheckConstant(1);
        private static Action<Expression> Two = CheckConstant(2);
        private static Action<Expression> Three = CheckConstant(3);
        private static Action<Expression> Four = CheckConstant(4);
        private static Action<Expression> None = CheckConstant(null);
        private static Action<Expression> Foo = CheckNameExpr("foo");
        private static Action<Expression> Ellipsis = CheckConstant(Microsoft.PythonTools.Parsing.Ellipsis.Value);
        private static Action<Expression> Bar = CheckNameExpr("bar");
        private static Action<Expression> Baz = CheckNameExpr("baz");
        private static Action<Expression> Quox = CheckNameExpr("quox");
        private static Action<Expression> Exception = CheckNameExpr("Exception");
        private static Action<Statement> Pass = CheckEmptyStmt();
        private static Action<Statement> Break = CheckBreakStmt();
        private static Action<Statement> Continue = CheckContinueStmt();


        private static Action<Statement> CheckSuite(params Action<Statement>[] statements) {
            return stmt => {
                Assert.AreEqual(typeof(SuiteStatement), stmt.GetType());
                SuiteStatement suite = (SuiteStatement)stmt;
                Assert.AreEqual(statements.Length, suite.Statements.Count);
                for (int i = 0; i < suite.Statements.Count; i++) {
                    try {
                        statements[i](suite.Statements[i]);
                    } catch (AssertFailedException e) {
                        throw new AssertFailedException(String.Format("Suite Item {0}: {1}", i, e.Message), e);
                    }
                }
            };
        }

        private static Action<Statement> CheckForStmt(Action<Expression> left, Action<Expression> list, Action<Statement> body, Action<Statement> _else = null) {
            return stmt => {
                Assert.AreEqual(typeof(ForStatement), stmt.GetType());
                ForStatement forStmt = (ForStatement)stmt;

                left(forStmt.Left);
                list(forStmt.List);
                body(forStmt.Body);
                if (_else != null) {
                    _else(forStmt.Else);
                } else {
                    Assert.AreEqual(forStmt.Else, null);
                }
            };
        }

        private static Action<Statement> CheckWhileStmt(Action<Expression> test, Action<Statement> body, Action<Statement> _else = null) {
            return stmt => {
                Assert.AreEqual(typeof(WhileStatement), stmt.GetType());
                var whileStmt = (WhileStatement)stmt;

                test(whileStmt.Test);
                body(whileStmt.Body);
                if (_else != null) {
                    _else(whileStmt.ElseStatement);
                } else {
                    Assert.AreEqual(whileStmt.ElseStatement, null);
                }
            };
        }

        private static Action<TryStatementHandler> CheckHandler(Action<Expression> test, Action<Expression> target, Action<Statement> body) {
            return handler => {
                body(handler.Body);

                if (test != null) {
                    test(handler.Test);
                } else {
                    Assert.AreEqual(null, handler.Test);
                }

                if (target != null) {
                    target(handler.Target);
                } else {
                    Assert.AreEqual(null, handler.Target);
                }
            };
        }

        private static Action<Statement> CheckTryStmt(Action<Statement> body, Action<TryStatementHandler>[] handlers, Action<Statement> _else = null, Action<Statement> _finally = null) {
            return stmt => {
                Assert.AreEqual(typeof(TryStatement), stmt.GetType());
                var tryStmt = (TryStatement)stmt;

                body(tryStmt.Body);

                Assert.AreEqual(handlers.Length, tryStmt.Handlers.Count);
                for (int i = 0; i < handlers.Length; i++) {
                    handlers[i](tryStmt.Handlers[i]);
                }

                if (_else != null) {
                    _else(tryStmt.Else);
                } else {
                    Assert.AreEqual(tryStmt.Else, null);
                }

                if (_finally != null) {
                    _finally(tryStmt.Finally);
                } else {
                    Assert.AreEqual(tryStmt.Finally, null);
                }
            };
        }

        private static Action<Statement> CheckRaiseStmt(Action<Expression> exceptionType = null, Action<Expression> exceptionValue = null, Action<Expression> traceBack = null, Action<Expression> cause = null) {
            return stmt => {
                Assert.AreEqual(typeof(RaiseStatement), stmt.GetType());
                var raiseStmt = (RaiseStatement)stmt;

                if (exceptionType != null) {
                    exceptionType(raiseStmt.ExceptType);
                } else {
                    Assert.AreEqual(raiseStmt.ExceptType, null);
                }

                if (exceptionValue != null) {
                    exceptionValue(raiseStmt.Value);
                } else {
                    Assert.AreEqual(raiseStmt.Value, null);
                }

                if (traceBack != null) {
                    traceBack(raiseStmt.Traceback);
                } else {
                    Assert.AreEqual(raiseStmt.Traceback, null);
                }

            };
        }

        private static Action<Statement> CheckPrintStmt(Action<Expression>[] expressions,  Action<Expression> destination = null, bool trailingComma = false) {
            return stmt => {
                Assert.AreEqual(typeof(PrintStatement), stmt.GetType());
                var printStmt = (PrintStatement)stmt;

                Assert.AreEqual(expressions.Length, printStmt.Expressions.Count);
                Assert.AreEqual(printStmt.TrailingComma, trailingComma);

                for (int i = 0; i < expressions.Length; i++) {
                    expressions[i](printStmt.Expressions[i]);
                }
                
                if (destination != null) {
                    destination(printStmt.Destination);
                } else {
                    Assert.AreEqual(printStmt.Destination, null);
                }
            };
        }


        private static Action<Statement> CheckAssertStmt(Action<Expression> test, Action<Expression> message = null) {
            return stmt => {
                Assert.AreEqual(typeof(AssertStatement), stmt.GetType());
                var assertStmt = (AssertStatement)stmt;

                test(assertStmt.Test);


                if (message != null) {
                    message(assertStmt.Message);
                } else {
                    Assert.AreEqual(assertStmt.Message, null);
                }
            };
        }

        private static Action<IfStatementTest> IfTest(Action<Expression> expectedTest, Action<Statement> body) {
            return test => {
                expectedTest(test.Test);
                body(test.Body);
            };
        }

        private static Action<IList<IfStatementTest>> IfTests(params Action<IfStatementTest>[] expectedTests) {
            return tests => {
                Assert.AreEqual(expectedTests.Length, tests.Count);
                for (int i = 0; i < expectedTests.Length; i++) {
                    expectedTests[i](tests[i]);
                }
            };
        }

        private static Action<Statement> CheckIfStmt(Action<IList<IfStatementTest>> tests, Action<Statement> _else = null) {
            return stmt => {
                Assert.AreEqual(typeof(IfStatement), stmt.GetType());
                var ifStmt = (IfStatement)stmt;

                tests(ifStmt.Tests);

                if (_else != null) {
                    _else(ifStmt.ElseStatement);
                } else {
                    Assert.AreEqual(null, ifStmt.ElseStatement);
                }
            };
        }

        private static Action<Statement> CheckFromImport(string fromName, string[] names, string[] asNames = null) {
            return stmt => {
                Assert.AreEqual(typeof(FromImportStatement), stmt.GetType());
                var fiStmt = (FromImportStatement)stmt;

                Assert.AreEqual(fiStmt.Root.MakeString(), fromName);
                Assert.AreEqual(names.Length, fiStmt.Names.Count);
                for (int i = 0; i < names.Length; i++) {
                    Assert.AreEqual(names[i], fiStmt.Names[i].Name);
                }

                if (asNames == null) {
                    if (fiStmt.AsNames != null) {
                        for (int i = 0; i < fiStmt.AsNames.Count; i++) {
                            Assert.AreEqual(null, fiStmt.AsNames[i]);
                        }
                    }
                } else {
                    Assert.AreEqual(asNames.Length, fiStmt.AsNames.Count);
                    for (int i = 0; i < asNames.Length; i++) {
                        Assert.AreEqual(asNames[i], fiStmt.AsNames[i].Name);
                    }
                }
            };
        }

        private static Action<Statement> CheckImport(string[] names, string[] asNames = null) {
            return stmt => {
                Assert.AreEqual(typeof(ImportStatement), stmt.GetType());
                var fiStmt = (ImportStatement)stmt;

                Assert.AreEqual(names.Length, fiStmt.Names.Count);
                for (int i = 0; i < names.Length; i++) {
                    Assert.AreEqual(names[i], fiStmt.Names[i].MakeString());
                }

                if (asNames == null) {
                    if (fiStmt.AsNames != null) {
                        for (int i = 0; i < fiStmt.AsNames.Count; i++) {
                            Assert.AreEqual(null, fiStmt.AsNames[i]);
                        }
                    }
                } else {
                    Assert.AreEqual(asNames.Length, fiStmt.AsNames.Count);
                    for (int i = 0; i < asNames.Length; i++) {
                        Assert.AreEqual(asNames[i], fiStmt.AsNames[i].Name);
                    }
                }
            };
        }

        private static Action<Statement> CheckExprStmt(Action<Expression> expr) {
            return stmt => {
                Assert.AreEqual(typeof(ExpressionStatement), stmt.GetType());
                ExpressionStatement exprStmt = (ExpressionStatement)stmt;
                expr(exprStmt.Expression);
            };
        }

        private static Action<Statement> CheckConstantStmt(object value) {
            return CheckExprStmt(CheckConstant(value));
        }

        private static Action<Statement> CheckLambdaStmt(Action<Parameter>[] args, Action<Expression> body) {
            return CheckExprStmt(CheckLambda(args, body));
        }

        private static Action<Expression> CheckLambda(Action<Parameter>[] args, Action<Expression> body) {
            return expr => {
                Assert.AreEqual(typeof(LambdaExpression), expr.GetType());

                var lambda = (LambdaExpression)expr;

                CheckFuncDef(null, args, (bodyCheck) => CheckReturnStmt(body)(bodyCheck))(lambda.Function);
            };
        }

        private static Action<Statement> CheckReturnStmt(Action<Expression> retVal = null) {
            return stmt => {
                Assert.AreEqual(typeof(ReturnStatement), stmt.GetType());
                var retStmt = (ReturnStatement)stmt;

                if (retVal != null) {
                    retVal(retStmt.Expression);
                } else {
                    Assert.AreEqual(null, retStmt.Expression);
                }
            };
        }

        private static Action<Statement> CheckFuncDef(string name, Action<Parameter>[] args, Action<Statement> body, Action<Expression>[] decorators = null, Action<Expression> returnAnnotation = null) {
            return stmt => {
                Assert.AreEqual(typeof(FunctionDefinition), stmt.GetType());
                var funcDef = (FunctionDefinition)stmt;

                if (name != null) {
                    Assert.AreEqual(name, funcDef.Name);
                }

                Assert.AreEqual(args.Length, funcDef.Parameters.Count);
                for (int i = 0; i < args.Length; i++) {
                    args[i](funcDef.Parameters[i]);
                }

                body(funcDef.Body);

                if (returnAnnotation != null) {
                    returnAnnotation(funcDef.ReturnAnnotation);
                } else {
                    Assert.AreEqual(null, funcDef.ReturnAnnotation);
                }

                CheckDecorators(decorators, funcDef.Decorators);
            };
        }

        private static void CheckDecorators(Action<Expression>[] decorators, DecoratorStatement foundDecorators) {
            if (decorators != null) {
                Assert.AreEqual(decorators.Length, foundDecorators.Decorators.Count);
                for (int i = 0; i < decorators.Length; i++) {
                    decorators[i](foundDecorators.Decorators[i]);
                }
            } else {
                Assert.AreEqual(null, foundDecorators);
            }
        }

        private static Action<Statement> CheckClassDef(string name, Action<Statement> body, Action<Arg>[] bases = null, Action<Expression>[] decorators = null) {
            return stmt => {
                Assert.AreEqual(typeof(ClassDefinition), stmt.GetType());
                var classDef = (ClassDefinition)stmt;

                if (name != null) {
                    Assert.AreEqual(name, classDef.Name);
                }

                if (bases != null) {
                    Assert.AreEqual(bases.Length, classDef.Bases.Count);
                    for (int i = 0; i < bases.Length; i++) {
                        bases[i](classDef.Bases[i]);
                    }
                } else {
                    Assert.AreEqual(0, classDef.Bases.Count);
                }

                body(classDef.Body);

                CheckDecorators(decorators, classDef.Decorators);
            };
        }

        private static Action<Parameter> CheckParameter(string name, ParameterKind kind = ParameterKind.Normal, Action<Expression> defaultValue = null, Action<Expression> annotation = null) {
            return param => {
                Assert.AreEqual(name, param.Name);
                Assert.AreEqual(kind, param.Kind);

                if (defaultValue != null) {
                    defaultValue(param.DefaultValue);
                } else {
                    Assert.AreEqual(null, param.DefaultValue);
                }

                if (annotation != null) {
                    annotation(param.Annotation);
                } else {
                    Assert.AreEqual(null, param.Annotation);
                }
            };
        }

        private static Action<Parameter> CheckSublistParameter(params string[] names) {
            return param => {
                Assert.AreEqual(typeof(SublistParameter), param.GetType());
                var sublistParam = (SublistParameter)param;

                Assert.AreEqual(names.Length, sublistParam.Tuple.Items.Count);
                for (int i = 0; i < names.Length; i++) {
                    Assert.AreEqual(names[i], ((NameExpression)sublistParam.Tuple.Items[i]).Name);
                }
            };
        }

        private static Action<Statement> CheckBinaryStmt(Action<Expression> lhs, PythonOperator op, Action<Expression> rhs) {
            return CheckExprStmt(CheckBinaryExpression(lhs, op, rhs));
        }

        private static Action<Expression> CheckBinaryExpression(Action<Expression> lhs, PythonOperator op, Action<Expression> rhs) {
            return expr => {
                Assert.AreEqual(typeof(BinaryExpression), expr.GetType());
                BinaryExpression bin = (BinaryExpression)expr;
                Assert.AreEqual(bin.Operator, op);
                lhs(bin.Left);
                rhs(bin.Right);
            };
        }

        private static Action<Statement> CheckUnaryStmt(PythonOperator op, Action<Expression> value) {
            return CheckExprStmt(CheckUnaryExpression(op, value));
        }

        private static Action<Expression> CheckUnaryExpression(PythonOperator op, Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(UnaryExpression), expr.GetType());
                var unary = (UnaryExpression)expr;
                Assert.AreEqual(unary.Op, op);
                value(unary.Expression);
            };
        }

        private static Action<Statement> CheckBackquoteStmt(Action<Expression> value) {
            return CheckExprStmt(CheckBackquoteExpr(value));
        }

        private static Action<Expression> CheckBackquoteExpr(Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(BackQuoteExpression), expr.GetType());
                var bq = (BackQuoteExpression)expr;
                value(bq.Expression);
            };
        }

        private static Action<Expression> CheckAndExpression(Action<Expression> lhs, Action<Expression> rhs) {
            return expr => {
                Assert.AreEqual(typeof(AndExpression), expr.GetType());
                AndExpression bin = (AndExpression)expr;
                lhs(bin.Left);
                rhs(bin.Right);
            };
        }

        private static Action<Expression> CheckOrExpression(Action<Expression> lhs, Action<Expression> rhs) {
            return expr => {
                Assert.AreEqual(typeof(OrExpression), expr.GetType());
                OrExpression bin = (OrExpression)expr;
                lhs(bin.Left);
                rhs(bin.Right);
            };
        }

        private static Action<Statement> CheckCallStmt(Action<Expression> target, params Action<Arg>[] args) {
            return CheckExprStmt(CheckCallExpression(target, args));
        }
        
        private static Action<Expression> CheckCallExpression(Action<Expression> target, params Action<Arg>[] args) {
            return expr => {
                Assert.AreEqual(typeof(CallExpression), expr.GetType());
                var call = (CallExpression)expr;
                target(call.Target);

                Assert.AreEqual(args.Length, call.Args.Count);
                for (int i = 0; i < args.Length; i++) {
                    args[i](call.Args[i]);
                }
            };
        }

        private static Action<Expression> DictItem(Action<Expression> key, Action<Expression> value) {
            return CheckSlice(key, value);
        }

        private static Action<Expression> CheckSlice(Action<Expression> start, Action<Expression> stop, Action<Expression> step = null) {
            return expr => {
                Assert.AreEqual(typeof(SliceExpression), expr.GetType());
                var slice = (SliceExpression)expr;

                if (start != null) {
                    start(slice.SliceStart);
                } else {
                    Assert.AreEqual(null, slice.SliceStart);
                }

                if (stop != null) {
                    stop(slice.SliceStop);
                } else {
                    Assert.AreEqual(null, slice.SliceStop);
                }

                if (step != null) {
                    step(slice.SliceStep);
                } else {
                    Assert.AreEqual(null, slice.SliceStep);
                }
            };
        }

        private static Action<Statement> CheckMemberStmt(Action<Expression> target, string name) {
            return CheckExprStmt(CheckMemberExpr(target, name));
        }

        private static Action<Expression> CheckMemberExpr(Action<Expression> target, string name) {
            return expr => {
                Assert.AreEqual(typeof(MemberExpression), expr.GetType());
                var member = (MemberExpression)expr;
                Assert.AreEqual(member.Name, name);
                target(member.Target);
            };
        }

        private static Action<Arg> CheckArg(string name) {
            return expr => {
                Assert.AreEqual(null, expr.Name);
                Assert.AreEqual(typeof(NameExpression), expr.Expression.GetType());
                var nameExpr = (NameExpression)expr.Expression;
                Assert.AreEqual(nameExpr.Name, name);
            };
        }


        private static Action<Arg> CheckNamedArg(string argName, Action<Expression> value) {
            return expr => {
                Assert.AreEqual(argName, expr.Name);
                value(expr.Expression);
            };
        }


        private static Action<Expression> CheckNameExpr(string name) {
            return expr => {
                Assert.AreEqual(typeof(NameExpression), expr.GetType());
                var nameExpr = (NameExpression)expr;
                Assert.AreEqual(nameExpr.Name, name);
            };
        }

        private static Action<Statement> CheckNameStmt(string name) {
            return CheckExprStmt(CheckNameExpr(name));
        }

        private static Action<Arg> PositionalArg(Action<Expression> value) {
            return arg => {
                Assert.AreEqual(true, String.IsNullOrEmpty(arg.Name));
                value(arg.Expression);
            };
        }

        private static Action<Arg> NamedArg(string name, Action<Expression> value) {
            return arg => {
                Assert.AreEqual(name, arg.Name);
                value(arg.Expression);
            };
        }

        private static Action<Arg> ListArg(Action<Expression> value) {
            return NamedArg("*", value);
        }

        private static Action<Arg> DictArg(Action<Expression> value) {
            return NamedArg("**", value);
        }

        private static Action<Statement> CheckIndexStmt(Action<Expression> target, Action<Expression> index) {
            return CheckExprStmt(CheckIndexExpression(target, index));
        }

        private static Action<Expression> CheckIndexExpression(Action<Expression> target, Action<Expression> index) {
            return expr => {
                Assert.AreEqual(typeof(IndexExpression), expr.GetType());
                var indexExpr = (IndexExpression)expr;
                target(indexExpr.Target);
                index(indexExpr.Index);
            };
        }

        private static Action<Statement> CheckDictionaryStmt(params Action<SliceExpression>[] items) {
            return CheckExprStmt(CheckDictionaryExpr(items));
        }

        private static Action<Expression> CheckDictionaryExpr(params Action<SliceExpression>[] items) {
            return expr => {
                Assert.AreEqual(typeof(DictionaryExpression), expr.GetType());
                var dictExpr = (DictionaryExpression)expr;
                Assert.AreEqual(items.Length, dictExpr.Items.Count);

                for (int i = 0; i < dictExpr.Items.Count; i++) {
                    items[i](dictExpr.Items[i]);
                }
            };
        }

        private static Action<Statement> CheckTupleStmt(params Action<Expression>[] items) {
            return CheckExprStmt(CheckTupleExpr(items));
        }

        private static Action<Expression> CheckTupleExpr(params Action<Expression>[] items) {
            return expr => {
                Assert.AreEqual(typeof(TupleExpression), expr.GetType());
                var tupleExpr = (TupleExpression)expr;
                Assert.AreEqual(items.Length, tupleExpr.Items.Count);

                for (int i = 0; i < tupleExpr.Items.Count; i++) {
                    items[i](tupleExpr.Items[i]);
                }
            };
        }

        private static Action<Expression> CheckListExpr(params Action<Expression>[] items) {
            return expr => {
                Assert.AreEqual(typeof(ListExpression), expr.GetType());
                var listExpr = (ListExpression)expr;
                Assert.AreEqual(items.Length, listExpr.Items.Count);

                for (int i = 0; i < listExpr.Items.Count; i++) {
                    items[i](listExpr.Items[i]);
                }
            };
        }

        private static Action<Statement> CheckAssignment(Action<Expression> lhs, Action<Expression> rhs) {
            return CheckAssignment(new[] { lhs }, rhs);
        }

        private static Action<Statement> CheckAssignment(Action<Expression>[] lhs, Action<Expression> rhs) {
            return expr => {
                Assert.AreEqual(typeof(AssignmentStatement), expr.GetType());
                var assign = (AssignmentStatement)expr;

                Assert.AreEqual(assign.Left.Count, lhs.Length);
                for (int i = 0; i < lhs.Length; i++) {
                    lhs[i](assign.Left[i]);
                }
                rhs(assign.Right);
            };
        }

        private static Action<Statement> CheckErrorStmt() {
            return expr => {
                Assert.AreEqual(typeof(ErrorStatement), expr.GetType());
            };
        }

        private static Action<Statement> CheckEmptyStmt() {
            return expr => {
                Assert.AreEqual(typeof(EmptyStatement), expr.GetType());
            };
        }

        private static Action<Statement> CheckBreakStmt() {
            return expr => {
                Assert.AreEqual(typeof(BreakStatement), expr.GetType());
            };
        }

        private static Action<Statement> CheckContinueStmt() {
            return expr => {
                Assert.AreEqual(typeof(ContinueStatement), expr.GetType());
            };
        }
        
        private static Action<Statement> CheckAssignment(Action<Expression> lhs, PythonOperator op, Action<Expression> rhs) {
            return stmt => {
                Assert.AreEqual(typeof(AugmentedAssignStatement), stmt.GetType());
                var assign = (AugmentedAssignStatement)stmt;

                Assert.AreEqual(assign.Operator, op);

                lhs(assign.Left);                
                rhs(assign.Right);
            };
        }

        private Action<Statement> CheckExecStmt(Action<Expression> code, Action<Expression> globals = null, Action<Expression> locals = null) {
            return stmt => {
                Assert.AreEqual(typeof(ExecStatement), stmt.GetType());
                var exec = (ExecStatement)stmt;

                code(exec.Code);
                if (globals != null) {
                    globals(exec.Globals);
                } else {
                    Assert.AreEqual(null, exec.Globals);
                }

                if (locals != null) {
                    locals(exec.Locals);
                } else {
                    Assert.AreEqual(null, exec.Locals);
                }
            };
        }

        private Action<Statement> CheckWithStmt(Action<Expression> expr, Action<Statement> body) {
            return CheckWithStmt(expr, null, body);
        }

        private Action<Statement> CheckWithStmt(Action<Expression> expr, Action<Expression> target, Action<Statement> body) {
            return CheckWithStmt(new[] { expr }, new[] { target }, body);
        }

        private Action<Statement> CheckWithStmt(Action<Expression>[] expr, Action<Statement> body) {
            return CheckWithStmt(expr, new Action<Expression>[expr.Length], body);
        }

        private Action<Statement> CheckWithStmt(Action<Expression>[] expr, Action<Expression>[] target, Action<Statement> body) {
            return stmt => {
                Assert.AreEqual(typeof(WithStatement), stmt.GetType());
                var with = (WithStatement)stmt;

                Assert.AreEqual(expr.Length, with.Items.Count);
                for (int i = 0; i < with.Items.Count; i++) {
                    expr[i](with.Items[i].ContextManager);

                    if (target[i] != null) {
                        target[i](with.Items[i].Variable);
                    } else {
                        Assert.AreEqual(null, with.Items[0].Variable);
                    }
                }

                body(with.Body);
            };
        }

        private static Action<Expression> CheckConstant(object value) {
            return expr => {
                Assert.AreEqual(typeof(ConstantExpression), expr.GetType());

                if (value is byte[]) {
                    Assert.AreEqual(typeof(AsciiString), ((ConstantExpression)expr).Value.GetType());
                    byte[] b1 = (byte[])value;
                    byte[] b2 = ((AsciiString)((ConstantExpression)expr).Value).Bytes;
                    Assert.AreEqual(b1.Length, b2.Length);

                    for (int i = 0; i < b1.Length; i++) {
                        Assert.AreEqual(b1[i], b2[i]);
                    }
                } else {
                    Assert.AreEqual(value, ((ConstantExpression)expr).Value);
                }
            };
        }

        private Action<Statement> CheckDelStmt(params Action<Expression>[] deletes) {
            return stmt => {
                Assert.AreEqual(typeof(DelStatement), stmt.GetType());
                var del = (DelStatement)stmt;

                Assert.AreEqual(deletes.Length, del.Expressions.Count);
                for (int i = 0; i < deletes.Length; i++) {
                    deletes[i](del.Expressions[i]);
                }
            };
        }

        private Action<Expression> CheckParenExpr(Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(ParenthesisExpression), expr.GetType());
                var paren = (ParenthesisExpression)expr;

                value(paren.Expression);
            };
        }

        private Action<Expression> CheckStarExpr(Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(StarredExpression), expr.GetType());
                var starred = (StarredExpression)expr;

                value(starred.Expression);
            };
        }

        private Action<Statement> CheckGlobal(params string[] names) {
            return stmt => {
                Assert.AreEqual(typeof(GlobalStatement), stmt.GetType());
                var global = (GlobalStatement)stmt;

                Assert.AreEqual(names.Length, global.Names.Count);
                for (int i = 0; i < names.Length; i++) {
                    Assert.AreEqual(names[i], global.Names[i].Name);
                }
            };
        }

        private Action<Statement> CheckNonlocal(params string[] names) {
            return stmt => {
                Assert.AreEqual(typeof(NonlocalStatement), stmt.GetType());
                var nonlocal = (NonlocalStatement)stmt;

                Assert.AreEqual(names.Length, nonlocal.Names.Count);
                for (int i = 0; i < names.Length; i++) {
                    Assert.AreEqual(names[i], nonlocal.Names[i].Name);
                }
            };
        }

        private Action<Statement> CheckStrOrBytesStmt(PythonLanguageVersion version, string str) {
            return CheckExprStmt(CheckStrOrBytes(version, str));
        }

        private Action<Expression> CheckStrOrBytes(PythonLanguageVersion version, string str) {
            return expr => {
                if (version.Is2x()) {
                    CheckConstant(ToBytes(str));
                } else {
                    CheckConstant(str);
                }
            };
        }

        private Action<Statement> CheckYieldStmt(Action<Expression> value) {
            return CheckExprStmt(CheckYieldExpr(value));
        }

        private Action<Expression> CheckYieldExpr(Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(YieldExpression), expr.GetType());
                var yield = (YieldExpression)expr;

                value(yield.Expression);
            };
        }

        private Action<Expression> CheckListComp(Action<Expression> item, params Action<ComprehensionIterator>[] iterators) {
            return expr => {
                Assert.AreEqual(typeof(ListComprehension), expr.GetType());
                var listComp = (ListComprehension)expr;

                Assert.AreEqual(iterators.Length, listComp.Iterators.Count);

                item(listComp.Item);
                for (int i = 0; i < iterators.Length; i++) {
                    iterators[i](listComp.Iterators[i]);
                }
            };
        }

        private Action<Expression> CheckGeneratorComp(Action<Expression> item, params Action<ComprehensionIterator>[] iterators) {
            return expr => {
                Assert.AreEqual(typeof(GeneratorExpression), expr.GetType());
                var listComp = (GeneratorExpression)expr;

                Assert.AreEqual(iterators.Length, listComp.Iterators.Count);

                item(listComp.Item);
                for (int i = 0; i < iterators.Length; i++) {
                    iterators[i](listComp.Iterators[i]);
                }
            };
        }

        private Action<Expression> CheckDictComp(Action<Expression> key, Action<Expression> value, params Action<ComprehensionIterator>[] iterators) {
            return expr => {
                Assert.AreEqual(typeof(DictionaryComprehension), expr.GetType());
                var dictComp = (DictionaryComprehension)expr;

                Assert.AreEqual(iterators.Length, dictComp.Iterators.Count);

                key(dictComp.Key);
                value(dictComp.Value);

                for (int i = 0; i < iterators.Length; i++) {
                    iterators[i](dictComp.Iterators[i]);
                }
            };
        }

        private Action<Expression> CheckSetComp(Action<Expression> item, params Action<ComprehensionIterator>[] iterators) {
            return expr => {
                Assert.AreEqual(typeof(SetComprehension), expr.GetType());
                var setComp = (SetComprehension)expr;

                Assert.AreEqual(iterators.Length, setComp.Iterators.Count);

                item(setComp.Item);

                for (int i = 0; i < iterators.Length; i++) {
                    iterators[i](setComp.Iterators[i]);
                }
            };
        }

        private Action<Expression> CheckSetLiteral(params Action<Expression>[] values) {
            return expr => {
                Assert.AreEqual(typeof(SetExpression), expr.GetType());
                var setLiteral = (SetExpression)expr;

                Assert.AreEqual(values.Length, setLiteral.Items.Count);
                for (int i = 0; i < values.Length; i++) {
                    values[i](setLiteral.Items[i]);
                }
            };
        }
        

        private Action<ComprehensionIterator> CompFor(Action<Expression> lhs, Action<Expression> list) {
            return iter => {
                Assert.AreEqual(typeof(ComprehensionFor), iter.GetType());
                var forIter = (ComprehensionFor)iter;

                lhs(forIter.Left);
                list(forIter.List);
            };
        }

        private Action<ComprehensionIterator> CompIf(Action<Expression> test) {
            return iter => {
                Assert.AreEqual(typeof(ComprehensionIf), iter.GetType());
                var ifIter = (ComprehensionIf)iter;

                test(ifIter.Test);
            };
        }

        private byte[] ToBytes(string str) {
            byte[] res = new byte[str.Length];
            for (int i = 0; i < str.Length; i++) {
                res[i] = (byte)str[i];
            }
            return res;
        }

        private static Action<Expression> IgnoreExpr() {
            return expr => { };
        }

        private static Action<Statement> IgnoreStmt() {
            return stmt => { };
        }

        private static Action<Parameter>[] NoParameters = new Action<Parameter>[0];

        private static void CollectFiles(string dir, List<string> files) {
            foreach (string file in Directory.GetFiles(dir)) {
                if (file.EndsWith(".py", StringComparison.OrdinalIgnoreCase)) {
                    files.Add(file);
                }
            }
            foreach (string nestedDir in Directory.GetDirectories(dir)) {
                CollectFiles(nestedDir, files);
            }
        }

        #endregion
    }
}
