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

namespace Microsoft.VisualStudioTools.MockVsTests
{
	[Export(typeof(IStandardClassificationService))]
	class MockStandardClassificationService : IStandardClassificationService
	{
		private readonly IClassificationTypeRegistryService _classRegistry;

		[ImportingConstructor]
		public MockStandardClassificationService(IClassificationTypeRegistryService classRegistry)
		{
			_classRegistry = classRegistry;
		}

		public IClassificationType CharacterLiteral
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.Character);
			}
		}

		public IClassificationType Comment
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
			}
		}

		public IClassificationType ExcludedCode
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.ExcludedCode);
			}
		}

		public IClassificationType FormalLanguage
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.FormalLanguage);
			}
		}

		public IClassificationType Identifier
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.Identifier);
			}
		}

		public IClassificationType Keyword
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
			}
		}

		public IClassificationType Literal
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.Literal);
			}
		}

		public IClassificationType NaturalLanguage
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.NaturalLanguage);
			}
		}

		public IClassificationType NumberLiteral
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.Number);
			}
		}

		public IClassificationType Operator
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.Operator);
			}
		}

		public IClassificationType Other
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.Other);
			}
		}

		public IClassificationType PreprocessorKeyword
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.PreprocessorKeyword);
			}
		}

		public IClassificationType StringLiteral
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.String);
			}
		}

		public IClassificationType SymbolDefinition
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.SymbolDefinition);
			}
		}

		public IClassificationType SymbolReference
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.SymbolReference);
			}
		}

		public IClassificationType WhiteSpace
		{
			get
			{
				return _classRegistry.GetClassificationType(PredefinedClassificationTypeNames.WhiteSpace);
			}
		}

		[Export]
		[Name(PredefinedClassificationTypeNames.NaturalLanguage)]
		internal ClassificationTypeDefinition naturalLanguageClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition formalLanguageClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.Comment)]
		[BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition commentClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.Identifier)]
		[BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition identifierClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.Keyword)]
		[BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition keywordClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.WhiteSpace)]
		[BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition whitespaceClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.Operator)]
		[BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition operatorClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.Literal)]
		[BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition literalClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.String)]
		[BaseDefinition(PredefinedClassificationTypeNames.Literal)]
		internal ClassificationTypeDefinition stringClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.Character)]
		[BaseDefinition(PredefinedClassificationTypeNames.Literal)]
		internal ClassificationTypeDefinition characterClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.Number)]
		[BaseDefinition(PredefinedClassificationTypeNames.Literal)]
		internal ClassificationTypeDefinition numberClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.Other)]
		[BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition otherClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.ExcludedCode)]
		[BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition excludedCodeClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.PreprocessorKeyword)]
		[BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition preprocessorKeywordClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.SymbolDefinition)]
		[BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition symbolDefinitionClassificationTypeDefinition = null;

		[Export]
		[Name(PredefinedClassificationTypeNames.SymbolReference)]
		[BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
		internal ClassificationTypeDefinition symbolReferenceClassificationTypeDefinition = null;

	}
}
