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
using System.IO;
using System.Text;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class MutateStdLibTest {
        public virtual PythonVersion Version {
            get {
                return PythonPaths.Python25;
            }
        }

        [TestMethod]
        public void TestMutateStdLib() {
            Version.AssertInstalled();

            for (int i = 0; i < 100; i++) {
                int seed = (int)DateTime.Now.Ticks;
                var random = new Random(seed);
                Console.WriteLine("Seed == " + seed);


                Console.WriteLine("Testing version {0} {1}", Version.Version, Version.LibPath);
                int ran = 0, succeeded = 0;
                string[] files;
                try {
                    files = Directory.GetFiles(Version.LibPath);
                } catch (DirectoryNotFoundException) {
                    continue;
                }

                foreach (var file in files) {
                    try {
                        if (file.EndsWith(".py")) {
                            ran++;
                            TestOneFileMutated(file, Version.Version, random);
                            succeeded++;
                        }
                    } catch (Exception e) {
                        Console.WriteLine(e);
                        Console.WriteLine("Failed: {0}", file);
                        break;
                    }
                }

                Assert.AreEqual(ran, succeeded);
            }
        }

        private static void TestOneFileMutated(string filename, PythonLanguageVersion version, Random random) {
            var originalText = File.ReadAllText(filename);
            int start = random.Next(originalText.Length);
            int end = random.Next(originalText.Length);

            int realStart = Math.Min(start, end);
            int length = Math.Max(start, end) - Math.Min(start, end);
            //Console.WriteLine("Removing {1} chars at {0}", realStart, length);
            originalText = originalText.Substring(realStart, length);

            ParserRoundTripTest.TestOneString(version, originalText);
        }
    }

    [TestClass]
    public class Mutate26 : MutateStdLibTest {
        public override PythonVersion Version {
            get { return PythonPaths.Python26; }
        }
    }

    [TestClass]
    public class Mutate27 : MutateStdLibTest {
        public override PythonVersion Version {
            get { return PythonPaths.Python27; }
        }
    }

    [TestClass]
    public class Mutate30 : MutateStdLibTest {
        public override PythonVersion Version {
            get { return PythonPaths.Python30; }
        }
    }

    [TestClass]
    public class Mutate31 : MutateStdLibTest {
        public override PythonVersion Version {
            get { return PythonPaths.Python31; }
        }
    }

    [TestClass]
    public class Mutate32 : MutateStdLibTest {
        public override PythonVersion Version {
            get { return PythonPaths.Python32; }
        }
    }

    [TestClass]
    public class Mutate33 : MutateStdLibTest {
        public override PythonVersion Version {
            get { return PythonPaths.Python33; }
        }
    }

    [TestClass]
    public class Mutate34 : MutateStdLibTest {
        public override PythonVersion Version {
            get { return PythonPaths.Python34; }
        }
    }
}
