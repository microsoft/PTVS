using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Environments {
    
    // rule to validate environment names when creating a new virtual environment
    internal class AddVirtualEnvironmentEnvNameRule : ValidationRule {

        private static readonly string InvalidPrintableFileCharsString = GetInvalidPrintableFileChars();

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {

            var envName = value.ToString();

            // the name can't be empty
            if (string.IsNullOrWhiteSpace(envName)) {
                return new ValidationResult(false, Strings.AddVirtualEnvironmentNameEmpty);
            }

            // the name can't have any invalid chars
            if (!PathUtils.IsValidFile(envName)) {
                return new ValidationResult(false, Strings.AddVirtualEnvironmentNameInvalid.FormatUI(InvalidPrintableFileCharsString));
            }

            // if we get here, the environment name is valid
            return ValidationResult.ValidResult;
        }

        // returns a space-delimited string containing invalid chars for a filename
        private static string GetInvalidPrintableFileChars() {
            var invalidChars = Path.GetInvalidFileNameChars();
            var invalidPrintableChars = invalidChars.Where(c => !char.IsControl(c));
            return string.Join(" ", invalidPrintableChars);
        }
    }
}
