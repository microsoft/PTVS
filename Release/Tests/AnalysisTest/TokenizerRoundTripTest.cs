using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Microsoft.PythonTools.Parsing;

namespace AnalysisTest {
    /// <summary>
    /// Test cases to verify that the tokenizer successfully preserves all information for round tripping source code.
    /// </summary>
    [TestClass]
    public class TokenizerRoundTripTest {
        // TODO: Add an explicit test for grouping characters and white space, e.g.:
        // (a, b, [whitespace]
        //  [more whitespace]   c, d)
        //
        [TestMethod]
        public void SimpleTest() {
            //string filename = "C:\\Python26\\Lib\\abc.py";
            //PythonLanguageVersion version = PythonLanguageVersion.V26;

            var versions = new[] { 
                new { Path = "C:\\Python24\\Lib", Version = PythonLanguageVersion.V24 },
                new { Path = "C:\\Python25\\Lib", Version = PythonLanguageVersion.V25 },
                new { Path = "C:\\Python26\\Lib", Version = PythonLanguageVersion.V26 },
                new { Path = "C:\\Python27\\Lib", Version = PythonLanguageVersion.V27 },
                
                new { Path = "C:\\Python30\\Lib", Version = PythonLanguageVersion.V30 },
                new { Path = "C:\\Python31\\Lib", Version = PythonLanguageVersion.V31 },
                new { Path = "C:\\Python32\\Lib", Version = PythonLanguageVersion.V32 } 
            };

            foreach (var version in versions) {
                Console.WriteLine("Testing version {0} {1}", version.Version, version.Path);
                int ran = 0, succeeded = 0;
                foreach (var file in Directory.GetFiles(version.Path)) {
                    try {
                        if (file.EndsWith(".py")) {
                            ran++;
                            TestOneFile(file, version.Version);
                            succeeded++;
                        }
                    } catch (Exception e) {
                        Console.WriteLine(e);
                        Console.WriteLine("Failed: {0}", file);
                    }
                }

                Assert.AreEqual(ran, succeeded);
            }
        }

        private static void TestOneFile(string filename, PythonLanguageVersion version) {
            StringBuilder output = new StringBuilder();

            var tokenizer = new Tokenizer(version, verbatim: true);
            var originalText = File.ReadAllText(filename);
            tokenizer.Initialize(new StringReader(originalText));
            Token token;
            while ((token = tokenizer.GetNextToken()) != Tokens.EndOfFileToken) {
                output.Append(tokenizer.PreceedingWhiteSpace);
                output.Append(token.VerbatimImage);
            }

            const int contextSize = 50;
            for (int i = 0; i < originalText.Length && i < output.Length; i++) {
                if (originalText[i] != output[i]) {
                    // output some context
                    StringBuilder x = new StringBuilder();
                    StringBuilder y = new StringBuilder();
                    StringBuilder z = new StringBuilder();
                    for (int j = Math.Max(0, i - contextSize); j < Math.Min(Math.Min(originalText.Length, output.Length), i + contextSize); j++) {
                        x.AppendRepr(originalText[j]);
                        y.AppendRepr(output[j]);
                        if (j == i) {
                            z.Append("^");
                        } else {
                            z.Append(" ");
                        }
                    }

                    Console.WriteLine("Mismatch context at {0}:", i);
                    Console.WriteLine("Original: {0}", x.ToString());
                    Console.WriteLine("New     : {0}", y.ToString());
                    Console.WriteLine("Differs : {0}", z.ToString());

                    Assert.AreEqual(originalText[i], output[i], String.Format("Characters differ at {0}, got {1}, expected {2}", i, output[i], originalText[i]));
                }
            }

            Assert.AreEqual(originalText.Length, output.Length);
        }        
    }

    static class StringBuilderExtensions {
        public static void AppendRepr(this StringBuilder self, char ch) {
            switch (ch) {
                // we append funky characters unlikely to show up here just so we always append a single char and it's easier to compare strings
                case '\t': self.Append("»"); break;
                case '\r': self.Append("¬"); break;
                case '\n': self.Append("‼"); break;
                case '\f': self.Append("╢"); break;
                default:
                    self.Append(ch); break;
            }
        }
    }
}
