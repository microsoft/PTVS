using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.Pythia
{
    public class Helper
    {
        /// <summary>
        /// Get list of solutions already parsed
        /// </summary>
        /// <returns>List of solution paths already parsed</returns>
        public static List<string> GetSolutionsAlreadyParsed(string PREVIOUS_OUTPUT_PREFIX)
        {
            var list = new List<string>();

            string[] outputPaths = Directory.GetFiles(PREVIOUS_OUTPUT_PREFIX, "*.json");
            foreach (string outputPath in outputPaths)
            {
                var fileName = outputPath.Split(Path.DirectorySeparatorChar).Last();
                var parts = fileName.Split('_');
                var solution = parts.Skip(1);
                list.Add(string.Join("_", solution));
            }

            return list;
        }

        /// <summary>
        /// Evaluate previous assignments to resolve defined variable types
        /// </summary>
        /// <param name="variableName">The variable to resolve for</param>
        /// <returns>String representation of the defined variable type</returns>
        public static string ResolveVariable(List<KeyValuePair> assignments, string variableName)
        {
            // Resolve from back to front, variable type is always the latest defined
            for (int i = assignments.Count - 1; i >= 0; i--)
            {
                var prevAssignment = assignments[i];
                if (prevAssignment.Key.Equals(variableName))
                {
                    return prevAssignment.Value;
                }
            }
            return null;
        }
    }
}
