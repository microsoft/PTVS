using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Microsoft.PythonTools.Parsing;

namespace AnalysisTests {
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
            var versions = new[] { 
                new { Path = "C:\\Python25\\Lib", Version = PythonLanguageVersion.V25 },
                new { Path = "C:\\Python26\\Lib", Version = PythonLanguageVersion.V26 },
                new { Path = "C:\\Python27\\Lib", Version = PythonLanguageVersion.V27 },
                
                new { Path = "C:\\Python30\\Lib", Version = PythonLanguageVersion.V30 },
                new { Path = "C:\\Python31\\Lib", Version = PythonLanguageVersion.V31 },
                new { Path = "C:\\Python32\\Lib", Version = PythonLanguageVersion.V32 } 
            };

            foreach (var optionSet in new[] { TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins, TokenizerOptions.Verbatim }) {
                foreach (var version in versions) {
                    Console.WriteLine("Testing version {0} {1} w/ Option Set {2}", version.Version, version.Path, optionSet);
                    int ran = 0, succeeded = 0;
                    foreach (var file in Directory.GetFiles(version.Path)) {
                        try {
                            if (file.EndsWith(".py")) {
                                ran++;
                                TestOneFile(file, version.Version, optionSet);
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
        }

        [TestMethod]
        public void BinaryTest() {
            var filename = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");
            TestOneFile(filename, PythonLanguageVersion.V27, TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins);
            TestOneFile(filename, PythonLanguageVersion.V27, TokenizerOptions.Verbatim);
        }

        [TestMethod]
        public void TestErrors() {
            TestOneString(PythonLanguageVersion.V27, TokenizerOptions.Verbatim, "http://xkcd.com/353/\")");
            TestOneString(PythonLanguageVersion.V27, TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins, "http://xkcd.com/353/\")");
            TestOneString(PythonLanguageVersion.V27, TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins, "lambda, U+039B");
            TestOneString(PythonLanguageVersion.V27, TokenizerOptions.Verbatim, "lambda, U+039B");
        }

        private static void TestOneFile(string filename, PythonLanguageVersion version, TokenizerOptions optionSet) {
            var originalText = File.ReadAllText(filename);

            TestOneString(version, optionSet, originalText);
        }

        private static void TestOneString(PythonLanguageVersion version, TokenizerOptions optionSet, string originalText) {
            StringBuilder output = new StringBuilder();

            var tokenizer = new Tokenizer(version, options: optionSet);
            tokenizer.Initialize(new StringReader(originalText));
            Token token;
            int prevOffset = 0;

            while ((token = tokenizer.GetNextToken()) != Tokens.EndOfFileToken) {
                output.Append(tokenizer.PreceedingWhiteSpace);
                output.Append(token.VerbatimImage);

                const int contextSize = 50;
                for (int i = prevOffset; i < originalText.Length && i < output.Length; i++) {
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
                        Console.WriteLine("Token   : {0}", token);

                        Assert.AreEqual(originalText[i], output[i], String.Format("Characters differ at {0}, got {1}, expected {2}", i, output[i], originalText[i]));
                    }
                }

                prevOffset = output.Length;
            }
            output.Append(tokenizer.PreceedingWhiteSpace);

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
                case (char)0: self.Append(' '); break;
                default:
                    self.Append(ch); break;
            }
        }
    }
}
