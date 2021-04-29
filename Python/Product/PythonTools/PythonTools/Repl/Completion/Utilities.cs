using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.VisualStudio.Core.Imaging;

namespace Microsoft.PythonTools.Repl.Completion {
    internal static class Utilities {
        private static Span GetTranslatedSpan(int startPosition, int length, ITextSnapshot oldSnapshot, ITextSnapshot newSnapshot) {
            var span = new Span(startPosition, length);

            if (oldSnapshot.Version != newSnapshot.Version) {
                var snapshotSpan = new SnapshotSpan(oldSnapshot, span);
                var translatedSnapshotSpan = snapshotSpan.TranslateTo(newSnapshot, SpanTrackingMode.EdgeInclusive);
                span = translatedSnapshotSpan.Span;
            }

            return span;
        }

        internal static void ApplyTextEdit(TextEdit textEdit, ITextSnapshot snapshot, ITextBuffer textBuffer) {
            Utilities.ApplyTextEdits(new[] { textEdit }, snapshot, textBuffer);
        }

        internal static void ApplyTextEdits(IEnumerable<TextEdit> textEdits, ITextSnapshot snapshot, ITextBuffer textBuffer) {
            using (var vsTextEdit = textBuffer.CreateEdit()) {
                foreach (var textEdit in textEdits) {
                    if (textEdit.Range.Start == textEdit.Range.End) {
                        var position = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.Start);
                        if (position > -1) {
                            var span = GetTranslatedSpan(position, 0, snapshot, vsTextEdit.Snapshot);
                            vsTextEdit.Insert(span.Start, textEdit.NewText);
                        }
                    } else if (string.IsNullOrEmpty(textEdit.NewText)) {
                        var startPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.Start);
                        var endPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.End);
                        var difference = endPosition - startPosition;
                        if (startPosition > -1 && endPosition > -1 && difference > 0) {
                            var span = GetTranslatedSpan(startPosition, difference, snapshot, vsTextEdit.Snapshot);
                            vsTextEdit.Delete(span);
                        }
                    } else {
                        var startPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.Start);
                        var endPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.End);
                        var difference = endPosition - startPosition;

                        if (startPosition > -1 && endPosition > -1 && difference > 0) {
                            var span = GetTranslatedSpan(startPosition, difference, snapshot, vsTextEdit.Snapshot);
                            vsTextEdit.Replace(span, textEdit.NewText);
                        }
                    }
                }

                vsTextEdit.Apply();
            }
        }

        internal static UnicodeCategory GetUnicodeCategory(string categoryName) {
            switch (categoryName) {
                case "Lu":
                    return UnicodeCategory.UppercaseLetter;
                case "Ll":
                    return UnicodeCategory.LowercaseLetter;
                case "Lt":
                    return UnicodeCategory.TitlecaseLetter;
                case "Lm":
                    return UnicodeCategory.ModifierLetter;
                case "Lo":
                    return UnicodeCategory.OtherLetter;
                case "Mn":
                    return UnicodeCategory.NonSpacingMark;
                case "Mc":
                    return UnicodeCategory.SpacingCombiningMark;
                case "Me":
                    return UnicodeCategory.EnclosingMark;
                case "Nd":
                    return UnicodeCategory.DecimalDigitNumber;
                case "Nl":
                    return UnicodeCategory.LetterNumber;
                case "No":
                    return UnicodeCategory.OtherNumber;
                case "Zs":
                    return UnicodeCategory.SpaceSeparator;
                case "Zl":
                    return UnicodeCategory.LineSeparator;
                case "Zp":
                    return UnicodeCategory.ParagraphSeparator;
                case "Cc":
                    return UnicodeCategory.Control;
                case "Cf":
                    return UnicodeCategory.Format;
                case "Cs":
                    return UnicodeCategory.Surrogate;
                case "Co":
                    return UnicodeCategory.PrivateUse;
                case "Pc":
                    return UnicodeCategory.ConnectorPunctuation;
                case "Pd":
                    return UnicodeCategory.DashPunctuation;
                case "Ps":
                    return UnicodeCategory.OpenPunctuation;
                case "Pe":
                    return UnicodeCategory.ClosePunctuation;
                case "Pi":
                    return UnicodeCategory.InitialQuotePunctuation;
                case "Pf":
                    return UnicodeCategory.FinalQuotePunctuation;
                case "Po":
                    return UnicodeCategory.OtherPunctuation;
                case "Sm":
                    return UnicodeCategory.MathSymbol;
                case "Sc":
                    return UnicodeCategory.CurrencySymbol;
                case "Sk":
                    return UnicodeCategory.ModifierSymbol;
                case "So":
                    return UnicodeCategory.OtherSymbol;
                case "Cn":
                default:
                    return UnicodeCategory.OtherNotAssigned;
            }
        }

        internal static ImageElement GetCompletionImage(CompletionItemKind? kind) {
            var moniker = KnownMonikers.Method;
            switch (kind) {
                case CompletionItemKind.Text:
                    moniker = KnownMonikers.TextElement;
                    break;
                case CompletionItemKind.Method:
                    moniker = KnownMonikers.MethodPublic;
                    break;
                case CompletionItemKind.Function:
                    moniker = KnownMonikers.MethodPublic;
                    break;
                case CompletionItemKind.Constructor:
                    moniker = KnownMonikers.ClassPublic;
                    break;
                case CompletionItemKind.Field:
                    moniker = KnownMonikers.FieldPrivate;
                    break;
                case CompletionItemKind.Variable:
                    moniker = KnownMonikers.LocalVariable;
                    break;
                case CompletionItemKind.Class:
                    moniker = KnownMonikers.ClassPublic;
                    break;
                case CompletionItemKind.Interface:
                    moniker = KnownMonikers.InterfacePublic;
                    break;
                case CompletionItemKind.Module:
                    moniker = KnownMonikers.ModulePublic;
                    break;
                case CompletionItemKind.Property:
                    moniker = KnownMonikers.PropertyPublic;
                    break;
                case CompletionItemKind.Unit:
                    moniker = KnownMonikers.Numeric;
                    break;
                case CompletionItemKind.Value:
                    moniker = KnownMonikers.ValueType;
                    break;
                case CompletionItemKind.Enum:
                    moniker = KnownMonikers.EnumerationPublic;
                    break;
                case CompletionItemKind.Keyword:
                    moniker = KnownMonikers.IntellisenseKeyword;
                    break;
                case CompletionItemKind.Snippet:
                    moniker = KnownMonikers.Snippet;
                    break;
                case CompletionItemKind.Color:
                    moniker = KnownMonikers.ColorPalette;
                    break;
                case CompletionItemKind.File:
                    moniker = KnownMonikers.TextFile;
                    break;
                case CompletionItemKind.Reference:
                    moniker = KnownMonikers.Reference;
                    break;
                case CompletionItemKind.Folder:
                    moniker = KnownMonikers.FolderClosed;
                    break;
                case CompletionItemKind.EnumMember:
                    moniker = KnownMonikers.EnumerationItemPublic;
                    break;
                case CompletionItemKind.Constant:
                    moniker = KnownMonikers.ConstantPublic;
                    break;
                case CompletionItemKind.Struct:
                    moniker = KnownMonikers.StructurePublic;
                    break;
                case CompletionItemKind.Event:
                    moniker = KnownMonikers.EventPublic;
                    break;
                case CompletionItemKind.Operator:
                    moniker = KnownMonikers.Operator;
                    break;
                case CompletionItemKind.TypeParameter:
                    moniker = KnownMonikers.Type;
                    break;
                default:
                    break;
            }

            return new ImageElement(moniker.ToImageId());
        }

        internal static ImmutableArray<char> GetCommitCharacterArray(string[] commitCharacters) {
            if (commitCharacters == null) {
                return ImmutableArray<char>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<char>(commitCharacters.Length);
            for (int i = 0; i < commitCharacters.Length; i++) {
                builder.Add(commitCharacters[i][0]);
            }

            return builder.ToImmutable();
        }

        internal static SnapshotSpan ToSnapshotSpan(this Range range, ITextSnapshot snapshot) {
            try {
                var startLine = snapshot.GetLineFromLineNumber(range.Start.Line);
                var startPosition = startLine.Start + range.Start.Character;
                var endLine = snapshot.GetLineFromLineNumber(range.End.Line);
                var endPosition = endLine.Start + range.End.Character;
                return new SnapshotSpan(startPosition, endPosition);
            } catch (ArgumentOutOfRangeException) {
                return new SnapshotSpan();
            }
        }

        internal static CompletionContext GetContextFromTrigger(VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionTrigger trigger, bool hasRegisteredTriggerCharacter) {
            var triggerString = trigger.Character == default(char) ? null : trigger.Character.ToString(CultureInfo.InvariantCulture);
            return new CompletionContext {
                TriggerCharacter = hasRegisteredTriggerCharacter ? triggerString : null,
                TriggerKind = hasRegisteredTriggerCharacter ? CompletionTriggerKind.TriggerCharacter : CompletionTriggerKind.Invoked,
            };
        }

        internal static string GetFilterName(CompletionItemKind? completionKind) {
            return completionKind switch {
                null => string.Empty,
                CompletionItemKind.Class => Strings.CompletionFilterClass,
                CompletionItemKind.Color => Strings.CompletionFilterColor,
                CompletionItemKind.Constant => Strings.CompletionFilterConstant,
                CompletionItemKind.Constructor => Strings.CompletionFilterConstructor,
                CompletionItemKind.Enum => Strings.CompletionFilterEnum,
                CompletionItemKind.EnumMember => Strings.CompletionFilterEnumMember,
                CompletionItemKind.Event => Strings.CompletionFilterEvent,
                CompletionItemKind.Field => Strings.CompletionFilterField,
                CompletionItemKind.File => Strings.CompletionFilterFile,
                CompletionItemKind.Folder => Strings.CompletionFilterFolder,
                CompletionItemKind.Function => Strings.CompletionFilterFunction,
                CompletionItemKind.Interface => Strings.CompletionFilterInterface,
                CompletionItemKind.Keyword => Strings.CompletionFilterKeyword,
                CompletionItemKind.Method => Strings.CompletionFilterMethod,
                CompletionItemKind.Module => Strings.CompletionFilterModule,
                CompletionItemKind.Operator => Strings.CompletionFilterOperator,
                CompletionItemKind.Property => Strings.CompletionFilterProperty,
                CompletionItemKind.Reference => Strings.CompletionFilterReference,
                CompletionItemKind.Snippet => Strings.CompletionFilterSnippet,
                CompletionItemKind.Struct => Strings.CompletionFilterStruct,
                CompletionItemKind.Text => Strings.CompletionFilterText,
                CompletionItemKind.TypeParameter => Strings.CompletionFilterTypeParameter,
                CompletionItemKind.Unit => Strings.CompletionFilterUnit,
                CompletionItemKind.Value => Strings.CompletionFilterValue,
                CompletionItemKind.Variable => Strings.CompletionFilterVariable,
                _ => Enum.GetName(typeof(CompletionItemKind), completionKind)
            };
        }
    }
}
