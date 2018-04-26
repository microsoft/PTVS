using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Analysis.Pythia
{
    internal static class Consts
    {
        public const string ModelsPath = @"Pythia\model\";
        public const string SequenceModelPath = ModelsPath + @"model-sequence.json.gz";

        public const string ExtensionVersion = "1.1.0";
        public const string ModelVersion = "1.1.0";

        public const string NullSequence = "N";
        public const string SequenceDelimiter = "~";
        public const string UnicodeStar = "\u2605 ";

        public const int MaxRecommendation = 5;
        public const int PrecedingSequenceLength = 2;
        public const int MaxSearchLength = 20;
    }
}
