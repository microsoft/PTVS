using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Repl.Completion {
    internal static class MarkdownUtil {
        // This regex matches markdown code blocks. It extracts the content into two groups:
        // the language name and the code content.
        private const string CodeBlockRegex = "^```(.*)[\\S\\s]^([\\S\\s]*?)[\\S\\s]^```";

        public static IEnumerable<string> ExtractCodeBlocks(string markdown) {
            var matchList = new List<string>();
            var matches = Regex.Matches(markdown, CodeBlockRegex, RegexOptions.Multiline);

            if (matches.Count > 0) {
                foreach (Match match in matches) {
                    var code = match.Groups[2].Value;
                    matchList.Add(match.Groups[2].Value);
                }
            } else {
                matchList.Add(markdown);
            }

            return matchList;
        }
    }
}
