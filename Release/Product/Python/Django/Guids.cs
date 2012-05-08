// Guids.cs
// MUST match guids.h
using System;

namespace Microsoft.PythonTools.Django {
    static class GuidList {
        public const string guidDjangoPkgString = "a8637c34-aa55-46e2-973c-9c3e09afc17b";
        public const string guidDjangoCmdSetString = "5b3281a5-d037-4e84-93aa-a6819304dbd9";
        public const string guidDjangoEditorFactoryString = "96108b8f-2a98-4f6b-a6b6-69e04e7b7d3f";

        public static readonly Guid guidDjangoCmdSet = new Guid(guidDjangoCmdSetString);
        public static readonly Guid guidDjangoEditorFactory = new Guid(guidDjangoEditorFactoryString);
    }
}