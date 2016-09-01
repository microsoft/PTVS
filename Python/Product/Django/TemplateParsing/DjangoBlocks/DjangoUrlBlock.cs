using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Django.TemplateParsing.DjangoBlocks {
    class DjangoUrlBlock : DjangoBlock {
        public readonly BlockClassification[] Args;

        public DjangoUrlBlock(BlockParseInfo parseInfo, params BlockClassification[] args)
            : base(parseInfo) {
            Args = args;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            string[] words = parseInfo.Args.Split(' ');
            List<BlockClassification> argClassifications = new List<BlockClassification>();

            int wordStart = parseInfo.Start + parseInfo.Command.Length;
            foreach (string word in words) {
                if (!string.IsNullOrEmpty(word)) {
                    // TODO: do we have to say that the first word after url is a string (Classification.Literal)?
                    Classification currentArgKind = word.Equals("as") ? Classification.Keyword : Classification.Identifier;
                    argClassifications.Add(new BlockClassification(new Span(wordStart, word.Length), currentArgKind));
                }
                wordStart += word.Length + 1;
            }

            return new DjangoUrlBlock(parseInfo, argClassifications.ToArray());
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
            BlockClassification? argPenultimateBeforePosition = GetArgBeforePosition(position);
            if (argBeforePosition == null)
                return GetUrlCompletion(context);

            // If not during or after the 'as' keyword, propose variables for url parameter and the 'as' keyword
            return IsAfterAsKeyword(argBeforePosition, argPenultimateBeforePosition) ? Enumerable.Empty<CompletionInfo>() :
                Enumerable.Concat(
                    base.GetCompletions(context, position),
                    new[] {
                        new CompletionInfo("as", StandardGlyphGroup.GlyphKeyword)
                    }
                );
        }

        private IEnumerable<CompletionInfo> GetUrlCompletion(IDjangoCompletionContext context) {
            return CompletionInfo.ToCompletionInfo(context.Urls.Select(url => string.Format("'{0}'", url.FullUrl)), StandardGlyphGroup.GlyphGroupField);
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
    }
}
