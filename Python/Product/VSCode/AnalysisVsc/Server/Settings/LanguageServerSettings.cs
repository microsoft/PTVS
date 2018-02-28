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
        public EditorSettings Editor { get; set; }
        public LinterSettings Linting { get; set; }
    }

    public sealed class EditorSettings {
        public int TabSize { get; set; }
        public bool InsertSpaces { get; set; }
        public bool SpaceAfterKeyword { get; set; }
        public bool SpacesAroundEquals { get; set; }
        public bool SpaceBeforeCurly { get; set; }
        public bool BreakMultipleStatements { get; set; }
    }

    public sealed class LinterSettings {
        public bool Enabled { get; set; }
        public bool NoTabs { get; set; }
        public bool TrailingWhitespace { get; set; }
        public bool TrailingBlankLines { get; set; }
        public bool LineLength { get; set; }
        public int MaxLineLength { get; set; }
        public bool MultipleStatements { get; set; }
    }
}
