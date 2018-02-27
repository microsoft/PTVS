// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.PythonTools.VsCode.Settings {
    /// <summary>
    /// Settings that match 'configuration' section in package.json
    /// </summary>
    /// <remarks>
    /// Each member matches one property such as 'r.property'.
    /// For nested properties such as 'r.group.property' there 
    /// should be 'group' member of class type and that class
    /// should have 'property' member.
    /// 
    /// </remarks>
    public sealed class LanguageServerSettings {
        public int Interpreter { get; set; }
        public EditorSettings Editor { get; set; }
        public LinterSettings Linting { get; set; }
    }

    public sealed class EditorSettings {
        public int TabSize { get; set; }
        public bool FormatScope { get; set; }
        public bool SpaceAfterKeyword { get; set; }
        public bool SpacesAroundEquals { get; set; }
        public bool SpaceBeforeCurly { get; set; }
        public bool BreakMultipleStatements { get; set; }
    }

    public sealed class LinterSettings {
        public bool Enabled { get; set; }
        public bool CamelCase { get; set; }
        public bool SnakeCase { get; set; }
        public bool PascalCase { get; set; }
        public bool UpperCase { get; set; }
        public bool MultipleDots { get; set; }
        public bool NameLength { get; set; }
        public int MaxNameLength { get; set; }
        public bool TrueFalseNames { get; set; }
        public bool AssignmentType { get; set; }
        public bool SpacesAroundComma { get; set; }
        public bool SpacesAroundOperators { get; set; }
        public bool CloseCurlySeparateLine { get; set; }
        public bool SpaceBeforeOpenBrace { get; set; }
        public bool SpacesInsideParenthesis { get; set; }
        public bool NoSpaceAfterFunctionName { get; set; }
        public bool OpenCurlyPosition { get; set; }
        public bool NoTabs { get; set; }
        public bool TrailingWhitespace { get; set; }
        public bool TrailingBlankLines { get; set; }
        public bool DoubleQuotes { get; set; }
        public bool LineLength { get; set; }
        public int MaxLineLength { get; set; }
        public bool Semicolons { get; set; }
        public bool MultipleStatements { get; set; }
    }
}
