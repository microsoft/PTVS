namespace Microsoft.PythonTools.Intellisense {
    internal class AnalysisVariable {
        public AnalysisVariable(VariableType type, LocationInfo location, int? version = null) {
            Location = location;
            Type = type;
            Version = version;
        }

        public LocationInfo Location { get; }
        public VariableType Type { get; }
        public int? Version { get; }
    }
}