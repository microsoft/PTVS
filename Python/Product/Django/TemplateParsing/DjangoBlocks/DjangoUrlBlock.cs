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

namespace Microsoft.PythonTools.Django.TemplateParsing.DjangoBlocks {
    class DjangoUrlBlock : DjangoBlock {
        public readonly BlockClassification[] Args;
        private readonly string _urlName;
        private readonly string[] _definedNamedParameters;
        private readonly uint _nbDefinedParameters;

        public DjangoUrlBlock(BlockParseInfo parseInfo, BlockClassification[] args, string urlName = null, string[] definedNamedParameters = null, uint nbDefinedParameters = 0)
            : base(parseInfo) {
            Args = args;
            _urlName = urlName;
            _definedNamedParameters = definedNamedParameters != null ? definedNamedParameters : Array.Empty<string>();
            _nbDefinedParameters = nbDefinedParameters;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            string[] words = parseInfo.Args.Split(' ');
            IList<BlockClassification> argClassifications = new List<BlockClassification>();
            IList<string> usedNamedParameters = new List<string>();
            uint nbParameters = 0;
            bool afterUrl = false, beforeAsKeyword = true;

            int wordStart = parseInfo.Start + parseInfo.Command.Length;
            string urlName = null;
            foreach (string word in words) {
                if (!string.IsNullOrEmpty(word)) {
                    Classification currentArgKind;
                    if (word.Equals("as")) {
                        currentArgKind = Classification.Keyword;
                        beforeAsKeyword = false;
                    } else {
                        currentArgKind = Classification.Identifier;
                    }
                    argClassifications.Add(new BlockClassification(new Span(wordStart, word.Length), currentArgKind));

                    if (afterUrl && beforeAsKeyword) {
                        ++nbParameters;
                    }

                    // Get url name
                    if (urlName == null) {
                        if (word.StartsWithOrdinal("'")) {
                            urlName = word.TrimStart('\'').TrimEnd('\'');
                            afterUrl = true;
                        } else if (word.StartsWithOrdinal("\"")) {
                            urlName = word.TrimStart('"').TrimEnd('"');
                            afterUrl = true;
                        }
                    }

                    // Test if word is a named parameter (but not in a string)
                    if (word.Contains('=') && !word.StartsWithOrdinal("'") && !word.StartsWithOrdinal("\"")) {
                        usedNamedParameters.Add(word.Split('=').First());
                    }
                }
                wordStart += word.Length + 1;
            }

            return new DjangoUrlBlock(parseInfo, argClassifications.ToArray(), urlName, usedNamedParameters.ToArray(), nbParameters);
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            yield return new BlockClassification(new Span(ParseInfo.Start, ParseInfo.Command.Length), Classification.Keyword);
            foreach (var arg in Args) {
                yield return arg;
            }
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            // If no argument, show urls
            if (Args.Length == 0) {
                return GetUrlCompletion(context);
            }

            BlockClassification? argBeforePosition = GetArgBeforePosition(position);
            BlockClassification? argPenultimateBeforePosition = GetPenultimateArgBeforePosition(position);
            if (argBeforePosition == null)
                return GetUrlCompletion(context);

            // No completion for the url variable name after the 'as' keyword
            if (IsAfterAsKeyword(argBeforePosition, argPenultimateBeforePosition))
                return Enumerable.Empty<CompletionInfo>();

            DjangoUrl url = FindCurrentDjangoUrl(context);
            // Too many parameters are already in the statement
            if (_nbDefinedParameters >= url.Parameters.Count) {
                return new[] {
                    new CompletionInfo("as", StandardGlyphGroup.GlyphKeyword),
                };
            }

            // Completion proposes unused named parameters, template variables and the 'as' keyword
            return Enumerable.Concat(
                Enumerable.Concat(
                    GetUnusedNamedParameters(url),
                    base.GetCompletions(context, position)
                ),
                new[] {
                    new CompletionInfo("as", StandardGlyphGroup.GlyphKeyword)
                }
            );
        }

        private IEnumerable<CompletionInfo> GetUrlCompletion(IDjangoCompletionContext context) {
            return CompletionInfo.ToCompletionInfo(context.Urls.Select(url => "'{0}'".FormatInvariant(url.FullName)), StandardGlyphGroup.GlyphGroupField);
        }

        private BlockClassification? GetArgBeforePosition(int position) {
            BlockClassification? argBeforePosition = null;
            foreach (BlockClassification arg in Args) {
                if (position > arg.Span.Start) {
                    argBeforePosition = arg;
                } else {
                    break;
                }
            }

            return argBeforePosition;
        }

        private BlockClassification? GetPenultimateArgBeforePosition(int position) {
            if (Args.Length < 2) {
                return null;
            }

            BlockClassification? argBeforePosition = null;
            BlockClassification? argPenultimateBeforePosition = null;
            foreach (BlockClassification arg in Args) {
                if (position > arg.Span.Start) {
                    argPenultimateBeforePosition = argBeforePosition;
                    argBeforePosition = arg;
                } else {
                    break;
                }
            }

            return argPenultimateBeforePosition;
        }

        private bool IsAfterAsKeyword(BlockClassification? argBeforePosition, BlockClassification? argPenultimateBeforePosition) {
            return argBeforePosition.Value.Classification == Classification.Keyword || (argPenultimateBeforePosition != null && argPenultimateBeforePosition.Value.Classification == Classification.Keyword);
        }

        private DjangoUrl FindCurrentDjangoUrl(IDjangoCompletionContext context) {
            return context.Urls.Where(url => url.FullName.Equals(_urlName)).FirstOrDefault();
        }

        private IEnumerable<CompletionInfo> GetUnusedNamedParameters(DjangoUrl url) {
            if (url == null) {
                return Enumerable.Empty<CompletionInfo>();
            }

            IList<string> undefinedNamedParameters = new List<string>();
            foreach (DjangoUrlParameter urlParam in url.NamedParameters) {
                if (!_definedNamedParameters.Contains(urlParam.Name)) {
                    undefinedNamedParameters.Add(urlParam.Name);
                }
            }

            return CompletionInfo.ToCompletionInfo(undefinedNamedParameters.Select(p => p += '='), StandardGlyphGroup.GlyphGroupField);
        }
    }
}
