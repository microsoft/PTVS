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

namespace Microsoft.PythonTools.Debugger
{
    class PythonException
    {
        public PythonException() { }

        public string ExceptionMessage { get; set; }
        public string ExceptionObjectExpression { get; set; }
        public string FormattedDescription { get; set; }
        public uint HResult { get; set; }
        public PythonException InnerException { get; set; }
        public string Source { get; set; }
        public string StackTrace { get; set; }
        public string TypeName { get; set; }
        public bool UserUnhandled { get; set; }

        public string GetDescription(bool formatted)
        {
            if (formatted && !string.IsNullOrEmpty(FormattedDescription))
            {
                return FormattedDescription;
            }

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(TypeName))
            {
                sb.Append(TypeName);
                if (!string.IsNullOrEmpty(ExceptionMessage))
                {
                    sb.Append(": ");
                    sb.AppendLine(ExceptionMessage);
                }
            }
            else if (!string.IsNullOrEmpty(ExceptionMessage))
            {
                sb.AppendLine(ExceptionMessage);
            }

            return sb.ToString();
        }

        internal static string Unescape(StringBuilder sb)
        {
            for (int i = 0; i < sb.Length - 1; ++i)
            {
                if (sb[i] != '\\')
                {
                    continue;
                }

                char c = sb[i + 1];
                switch (c)
                {
                    case 'n': c = '\n'; break;
                    case 'r': c = '\r'; break;
                    case '\\': break;
                    case '\'': break;
                    case '"': break;
                    default: c = '\0'; break;
                }

                if (c != '\0')
                {
                    sb[i] = c;
                    sb.Remove(i, 1);
                }
            }

            while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1]))
            {
                sb.Length -= 1;
            }
            return sb.ToString();
        }

        internal static IEnumerable<string> SplitReprs(string line)
        {
            string endQuote = null;
            var element = new StringBuilder();
            foreach (var bit in line.Split(','))
            {
                bool added = false;

                if (endQuote == null)
                {
                    if (string.IsNullOrEmpty(bit))
                    {
                        yield return string.Empty;
                    }
                    else if (bit.StartsWithOrdinal("\"") || bit.StartsWithOrdinal("'"))
                    {
                        endQuote = bit.Remove(1);
                        element.Append(bit.Substring(1));
                        added = true;
                    }
                    else
                    {
                        yield return bit;
                    }
                }

                if (endQuote != null && bit.EndsWithOrdinal(endQuote))
                {
                    if (!added)
                    {
                        element.Append(',');
                        element.Append(bit);
                    }
                    element.Length -= endQuote.Length;
                    yield return Unescape(element);
                    endQuote = null;
                    element.Clear();
                }
            }
        }

        internal static string ReformatStackTrace(PythonProcess process, string data)
        {
            var lines = new Stack<IEnumerable<string>>();
            var sb = new StringBuilder();
            using (var reader = new StringReader(data))
            {
                for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    lines.Push(SplitReprs(line).ToArray());
                }
            }

            foreach (var line in lines)
            {
                var filename = line.ElementAtOrDefault(0);
                var lineNumber = line.ElementAtOrDefault(1);
                var functionName = line.ElementAtOrDefault(2);
                //var text = stackLine.ElementAtOrDefault(3);

                int lineNo;
                if (process != null &&
                    !string.IsNullOrEmpty(filename) &&
                    !string.IsNullOrEmpty(lineNumber) &&
                    int.TryParse(lineNumber, out lineNo) &&
                    !string.IsNullOrEmpty(functionName))
                {
                    functionName = PythonStackFrame.GetQualifiedFunctionName(process, filename, lineNo, functionName);
                }

                if (string.IsNullOrEmpty(functionName))
                {
                    functionName = Strings.DebugUnknownFunctionName;
                }

                if (!string.IsNullOrEmpty(filename))
                {
                    sb.AppendLine(string.IsNullOrEmpty(lineNumber)
                        ? Strings.DebugPythonExceptionStackTraceFileOnly.FormatUI(filename, functionName)
                        : Strings.DebugPythonExceptionStackTraceFileAndLineNumber.FormatUI(filename, lineNumber, functionName)
                    );
                }
                else
                {
                    sb.AppendLine(functionName);
                }
            }

            while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1]))
            {
                sb.Length -= 1;
            }

            return sb.ToString();
        }

        public void SetValue(PythonProcess process, string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.Fail("Unexpected empty key");
                return;
            }
            if (value == null)
            {
                Debug.Fail("Unexpected null value");
                return;
            }

            switch (key.ToLowerInvariant())
            {
                case "typename":
                    TypeName = value;
                    break;
                case "message":
                    ExceptionMessage = value;
                    break;
                case "trace":
                    StackTrace = ReformatStackTrace(process, value);
                    break;
                case "excvalue":
                    ExceptionObjectExpression = value;
                    break;
                case "breaktype":
                    UserUnhandled = ("unhandled".Equals(value, StringComparison.OrdinalIgnoreCase));
                    break;
                default:
                    Debug.WriteLine("Unexpected key in rich exception: " + key);
                    break;
            }
        }
    }
}
