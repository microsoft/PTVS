// Visual Studio Shared Project
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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Microsoft.VisualStudioTools.Wpf
{
    public static class Converters
    {
        public static IValueConverter Scale055 { get; } = LambdaConverter.Create<double>(x => x * 0.55);
        public static IValueConverter Scale122 { get; } = LambdaConverter.Create<double>(x => x * 1.22);
        public static IValueConverter Scale155 { get; } = LambdaConverter.Create<double>(x => x * 1.55);
        public static IValueConverter Scale190 { get; } = LambdaConverter.Create<double>(x => x * 1.90);
        public static IValueConverter StringJoin { get; } = LambdaConverter.Create<IEnumerable<string>>(x => string.Join(", ", x));
        public static IValueConverter NullIsTrue { get; } = LambdaConverter.Create<object>(x => x == null);
        public static IValueConverter NullIsFalse { get; } = LambdaConverter.Create<object>(x => x != null);
        public static IValueConverter NullIsCollapsed { get; } = LambdaConverter.Create<object>(x => x == null ? Visibility.Collapsed : Visibility.Visible);
        public static IValueConverter NullIsNotCollapsed { get; } = LambdaConverter.Create<object>(x => x == null ? Visibility.Visible : Visibility.Collapsed);
        public static IValueConverter TrueIsCollapsed { get; } = LambdaConverter.Create<bool>(x => x ? Visibility.Collapsed : Visibility.Visible);
        public static IValueConverter TrueIsNotCollapsed { get; } = LambdaConverter.Create<bool>(x => x ? Visibility.Visible : Visibility.Collapsed);
        public static IValueConverter FalseIsCollapsed { get; } = LambdaConverter.Create<bool>(x => !x ? Visibility.Collapsed : Visibility.Visible);
        public static IValueConverter FalseIsNotCollapsed { get; } = LambdaConverter.Create<bool>(x => !x ? Visibility.Visible : Visibility.Collapsed);
        public static IValueConverter TrueIsHidden { get; } = LambdaConverter.Create<bool>(x => x ? Visibility.Hidden : Visibility.Visible);
        public static IValueConverter TrueIsNotHidden { get; } = LambdaConverter.Create<bool>(x => x ? Visibility.Visible : Visibility.Hidden);
        public static IValueConverter FalseIsHidden { get; } = LambdaConverter.Create<bool>(x => !x ? Visibility.Hidden : Visibility.Visible);
        public static IValueConverter FalseIsNotHidden { get; } = LambdaConverter.Create<bool>(x => !x ? Visibility.Visible : Visibility.Hidden);
        public static IValueConverter Not { get; } = LambdaConverter.Create<bool>(x => !x);
        public static IValueConverter NullOrEmptyIsTrue { get; } = LambdaConverter.Create<IEnumerable>(x => x == null || !x.GetEnumerator().MoveNext());
        public static IValueConverter NullOrEmptyIsFalse { get; } = LambdaConverter.Create<IEnumerable>(x => x != null && x.GetEnumerator().MoveNext());
        public static IValueConverter NullOrEmptyIsCollapsed { get; } = LambdaConverter.Create<IEnumerable>(x => x == null || !x.GetEnumerator().MoveNext() ? Visibility.Collapsed : Visibility.Visible);
        public static IValueConverter NullOrEmptyIsNotCollapsed { get; } = LambdaConverter.Create<IEnumerable>(x => x == null || !x.GetEnumerator().MoveNext() ? Visibility.Visible : Visibility.Collapsed);
        public static IValueConverter EmptyIsCollapsed { get; } = LambdaConverter.Create<IEnumerable>(x => x != null && !x.GetEnumerator().MoveNext() ? Visibility.Collapsed : Visibility.Visible);
        public static IValueConverter EmptyIsNotCollapsed { get; } = LambdaConverter.Create<IEnumerable>(x => x != null && !x.GetEnumerator().MoveNext() ? Visibility.Visible : Visibility.Collapsed);
        public static IValueConverter TrueIsCrossCursor { get; } = LambdaConverter.Create<bool>(x => x ? Cursors.Cross : Cursors.Arrow);
        public static IValueConverter TrueIsBold { get; } = LambdaConverter.Create<bool>(x => x ? FontWeights.Bold : FontWeights.Normal);

        public static IMultiValueConverter Any { get; } = LambdaConverter.CreateMulti<bool>(args => args.Any(x => x));
        public static IMultiValueConverter AnyIsHidden { get; } = LambdaConverter.CreateMulti<bool>(args => args.Any(x => x) ? Visibility.Hidden : Visibility.Visible);
        public static IMultiValueConverter AnyIsNotHidden { get; } = LambdaConverter.CreateMulti<bool>(args => args.Any(x => x) ? Visibility.Visible : Visibility.Hidden);
        public static IMultiValueConverter AnyIsCollapsed { get; } = LambdaConverter.CreateMulti<bool>(args => args.Any(x => x) ? Visibility.Collapsed : Visibility.Visible);
        public static IMultiValueConverter AnyIsNotCollapsed { get; } = LambdaConverter.CreateMulti<bool>(args => args.Any(x => x) ? Visibility.Visible : Visibility.Collapsed);
        public static IMultiValueConverter All { get; } = LambdaConverter.CreateMulti<bool>(args => args.All(x => x));
        public static IMultiValueConverter AllIsHidden { get; } = LambdaConverter.CreateMulti<bool>(args => args.All(x => x) ? Visibility.Hidden : Visibility.Visible);
        public static IMultiValueConverter AllIsNotHidden { get; } = LambdaConverter.CreateMulti<bool>(args => args.All(x => x) ? Visibility.Visible : Visibility.Hidden);
        public static IMultiValueConverter AllIsCollapsed { get; } = LambdaConverter.CreateMulti<bool>(args => args.All(x => x) ? Visibility.Collapsed : Visibility.Visible);
        public static IMultiValueConverter AllIsNotCollapsed { get; } = LambdaConverter.CreateMulti<bool>(args => args.All(x => x) ? Visibility.Visible : Visibility.Collapsed);
        public static IMultiValueConverter Max { get; } = LambdaConverter.CreateMulti<double>(x => x.Max());
        public static IMultiValueConverter Min { get; } = LambdaConverter.CreateMulti<double>(x => x.Min());
        public static IMultiValueConverter ShowScrollbarForMinWidth { get; } = LambdaConverter.Create<double, double>((x, y) => x < y ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
    }
}
