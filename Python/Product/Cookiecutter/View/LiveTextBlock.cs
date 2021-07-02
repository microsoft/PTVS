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

namespace Microsoft.CookiecutterTools.View
{
    //public sealed class LiveTextBlock : TextBlock {
    //    public LiveTextBlock() {
    //        // Bind LiveText to the Text property.
    //        // They are not set independently - this just gives us the
    //        // notification when it changes
    //        SetBinding(
    //            LiveTextProperty,
    //            new Binding {
    //                Path = new PropertyPath(TextProperty),
    //                Source = this,
    //                Mode = BindingMode.OneWay
    //            }
    //        );

    //        Loaded += LiveTextBlock_Loaded;
    //        IsVisibleChanged += LiveTextBlock_IsVisibleChanged;
    //    }

    //    private void LiveTextBlock_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
    //        if (e.NewValue is bool && (bool)e.NewValue) {
    //            SetLiveProperty();
    //        }
    //    }

    //    private void LiveTextBlock_Loaded(object sender, RoutedEventArgs e) {
    //        SetLiveProperty();
    //        Loaded -= LiveTextBlock_Loaded;
    //    }

    //    private static void SetLivePropertyWorker(UIElement obj) {
    //        var peer = UIElementAutomationPeer.CreatePeerForElement(obj) as LiveTextBlockAutomationPeer;
    //        if (peer == null) {
    //            return;
    //        }

    //        CustomAutomationProperties.SetLiveSetting(obj, AutomationLiveSetting.Polite);
    //        peer.RaiseCustomAutomationEvent(CustomAutomationEvents.LiveRegionChangedEvent);
    //    }

    //    private void SetLiveProperty() {
    //        // HACK: This function uses an internal API that may change.
    //        // We're using it for now until the support arrives in WPF properly,
    //        // and we will try to detect and warn about changes so we degrade
    //        // gracefully.
    //        try {
    //            SetLivePropertyWorker(this);
    //        } catch (Exception) {
    //            Debug.Fail("Internal LiveSetting API is broken");
    //        }
    //    }

    //    private static readonly DependencyProperty LiveTextProperty =
    //        DependencyProperty.Register("LiveText", typeof(string), typeof(LiveTextBlock), new PropertyMetadata(null, OnTextChanged));

    //    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
    //        (d as LiveTextBlock).SetLiveProperty();
    //    }

    //    protected override AutomationPeer OnCreateAutomationPeer() {
    //        return new LiveTextBlockAutomationPeer(this);
    //    }

    //    private class LiveTextBlockAutomationPeer : TextBlockAutomationPeer, ICustomAutomationEventSource {
    //        public LiveTextBlockAutomationPeer(LiveTextBlock owner) : base(owner) { }

    //        public IRawElementProviderSimple GetProvider() => ProviderFromPeer(this);

    //        protected override string GetClassNameCore() => nameof(LiveTextBlock);
    //    }
    //}
}
