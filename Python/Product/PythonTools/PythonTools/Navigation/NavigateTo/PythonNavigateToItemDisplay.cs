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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using AnalysisReference = Microsoft.PythonTools.Intellisense.AnalysisProtocol.AnalysisReference;
using Completion = Microsoft.PythonTools.Intellisense.AnalysisProtocol.Completion;

namespace Microsoft.PythonTools.Navigation.NavigateTo {
    internal class PythonNavigateToItemDisplay : INavigateToItemDisplay {
        private static readonly Dictionary<StandardGlyphGroup, Icon> _iconCache = new Dictionary<StandardGlyphGroup, Icon>();
        private readonly PythonEditorServices _services;
        private readonly NavigateToItem _item;
        private readonly Completion _completion;
        private readonly Icon _icon;
        private readonly ReadOnlyCollection<DescriptionItem> _descrItems;
        private readonly AnalysisReference _location;

        public PythonNavigateToItemDisplay(NavigateToItem item) {
            _item = item;
            var tag = (PythonNavigateToItemProvider.ItemTag)item.Tag;
            _services = tag.Services;
            _completion = tag.Completion;
            _icon = GetIcon(_services.GlyphService, _completion.memberType.ToGlyphGroup());

            foreach (var v in _completion.detailedValues.MaybeEnumerate()) {
                foreach (var loc in v.locations.MaybeEnumerate()) {
                    if (loc.kind == "definition") {
                        _location = loc;
                        break;
                    }
                }
            }

            AdditionalInformation = "";
            Description = "";

            var descrItems = new List<DescriptionItem>();

            if (!string.IsNullOrEmpty(tag.ProjectName)) {
                descrItems.Add(new DescriptionItem(
                    Array.AsReadOnly(new[] { new DescriptionRun(Strings.PythonNavigateToItemDisplay_ProjectHeader, bold: true) }),
                    Array.AsReadOnly(new[] { new DescriptionRun(tag.ProjectName) })
                ));
                AdditionalInformation = Strings.PythonNavigateToItemDisplay_ProjectInfo.FormatUI(tag.ProjectName);
            }

            if (!string.IsNullOrEmpty(_location?.file)) {
                descrItems.Add(new DescriptionItem(
                    Array.AsReadOnly(new[] { new DescriptionRun(Strings.PythonNavigateToItemDisplay_FileHeader, bold: true) }),
                    Array.AsReadOnly(new[] { new DescriptionRun(_location.file) })
                ));
                if (string.IsNullOrEmpty(AdditionalInformation)) {
                    AdditionalInformation = Strings.PythonNavigateToItemDisplay_FileInfo.FormatUI(_location.file);
                }
                if (_location.startLine > 0) {
                    descrItems.Add(new DescriptionItem(
                        Array.AsReadOnly(new[] { new DescriptionRun(Strings.PythonNavigateToItemDisplay_LineHeader, bold: true) }),
                        Array.AsReadOnly(new[] { new DescriptionRun(_location.startLine.ToString()) })
                    ));
                }
            }
            _descrItems = descrItems.AsReadOnly();
        }

        public string Name {
            get { return _item.Name; }
        }

        public string AdditionalInformation { get; }

        public string Description { get; }

        public ReadOnlyCollection<DescriptionItem> DescriptionItems {
            get { return _descrItems; }
        }

        public Icon Glyph {
            get {
                return _icon;
            }
        }

        public void NavigateTo() {
            if (_location == null) {
                return;
            }

            PythonToolsPackage.NavigateTo(_services.Site, _location.file, Guid.Empty, _location.startLine - 1, _location.startColumn - 1);
        }

        private static Icon GetIcon(IGlyphService glyphService, StandardGlyphGroup glyphGroup) {
            Icon icon = null;
            if (_iconCache.TryGetValue(glyphGroup, out icon)) {
                return icon;
            }

            BitmapSource glyph = glyphService.GetGlyph(glyphGroup, StandardGlyphItem.GlyphItemPublic) as BitmapSource;
            if (glyph != null) {
                Bitmap bmp = new Bitmap(glyph.PixelWidth, glyph.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                glyph.CopyPixels(Int32Rect.Empty, bmpData.Scan0, bmpData.Height * bmpData.Stride, bmpData.Stride);
                bmp.UnlockBits(bmpData);
                icon = Icon.FromHandle(bmp.GetHicon());
            }

            _iconCache[glyphGroup] = icon;
            return icon;
        }
    }
}
