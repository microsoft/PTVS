// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
// Licensed under the Apache License, Version 2.0.

extern alias pythontools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using pythontools::Microsoft.PythonTools;
using pythontools::Microsoft.PythonTools.Project;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class PythonNotSupportedInfoBarTests {
        [ClassInitialize]
        public static void Initialize(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void SupportedVersionsDoNotWarn() {
            using (var context = new NotificationContext()) {
                foreach (var version in new[] { new Version(3, 9), new Version(3, 14), new Version(3, 15) }) {
                    context.Active = CreateFactory(version);
                    using (var bar = context.CreateBar()) {
                        Check(bar);
                    }
                }
                Assert.AreEqual(0, context.Factory.Elements.Count);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void UnknownVersionShowsNonblockingDismissibleInfoBar() {
            using (var context = new NotificationContext())
            using (var bar = context.CreateBar()) {
                Check(bar);

                var element = context.Factory.Elements.Single();
                Assert.IsTrue(element.Model.IsCloseButtonVisible);
                StringAssert.Contains(element.Model.TextSpans.GetSpan(0).Text, "3.16");
                Assert.AreEqual(1, element.Model.ActionItems.Count);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void Python2KeepsItsCompatibilityWarning() {
            using (var context = new NotificationContext()) {
                context.Active = CreateFactory(new Version(2, 7));
                using (var bar = context.CreateBar()) {
                    Check(bar);
                    Assert.AreEqual(Microsoft.PythonTools.Strings.WarningPython2NotSupported, context.Factory.Elements.Single().Model.TextSpans.GetSpan(0).Text);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void MissingAndUnavailableInterpretersDoNotWarn() {
            using (var context = new NotificationContext()) {
                var noInterpreters = CreateFactory(new Version(), InterpreterRegistryConstants.NoInterpretersFactoryId);
                var missing = new MockPythonInterpreterFactory(new InterpreterConfiguration(
                    "missing", "missing", pythonExePath: null, version: new Version(3, 16)));
                foreach (var factory in new IPythonInterpreterFactory[] { null, new MockPythonInterpreterFactory(null), noInterpreters, missing }) {
                    context.Active = factory;
                    using (var bar = context.CreateBar()) {
                        Check(bar);
                    }
                }
                Assert.AreEqual(0, context.Factory.Elements.Count);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void RepeatedChecksAndEquivalentFactoriesDoNotDuplicateWarning() {
            using (var context = new NotificationContext())
            using (var bar = context.CreateBar()) {
                context.UIThread.QueueChecks = true;
                var checks = Enumerable.Range(0, 20).Select(_ => bar.CheckAsync()).ToArray();
                context.UIThread.RunQueuedChecks();
                Task.WhenAll(checks).GetAwaiter().GetResult();
                context.UIThread.QueueChecks = false;
                context.Active = CreateFactory(new Version(3, 16, 9));
                Check(bar);

                Assert.AreEqual(1, context.Factory.Elements.Count);
                Assert.IsFalse(context.Factory.Elements[0].IsClosed);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void InterpreterChangeClosesStaleWarningAndWarnsForAnotherVersion() {
            using (var context = new NotificationContext())
            using (var bar = context.CreateBar()) {
                Check(bar);
                context.Active = CreateFactory(new Version(3, 15));
                Check(bar);
                Assert.IsTrue(context.Factory.Elements[0].IsClosed);
                Assert.AreEqual(1, context.Factory.Elements.Count);

                context.Active = CreateFactory(new Version(3, 17));
                Check(bar);
                Assert.AreEqual(2, context.Factory.Elements.Count);
                StringAssert.Contains(context.Factory.Elements[1].Model.TextSpans.GetSpan(0).Text, "3.17");
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void DismissalPersistsAcrossContextsAndServiceRestartForOnlyThatVersion() {
            MockPythonToolsOptionsService store;
            using (var context = new NotificationContext())
            using (var bar = context.CreateBar()) {
                store = context.Store;
                Check(bar);
                context.Factory.Elements.Single().ClickDismiss(bar);
                Assert.IsTrue(context.Factory.Elements.Single().IsClosed);
                Assert.IsTrue(context.Service.GeneralOptions.PromptForPythonVersionNotSupported);

                using (var otherProject = context.CreateBar())
                using (var workspace = context.CreateBar(InfoBarContexts.Workspace)) {
                    Check(otherProject);
                    Check(workspace);
                    Assert.AreEqual(1, context.Factory.Elements.Count);
                }
            }

            using (var restarted = new NotificationContext(store)) {
                restarted.Active = CreateFactory(new Version(3, 16, 42), "another-environment");
                using (var bar = restarted.CreateBar()) {
                    Check(bar);
                    Assert.AreEqual(0, restarted.Factory.Elements.Count);
                    restarted.Active = CreateFactory(new Version(3, 17));
                    Check(bar);
                    Assert.AreEqual(1, restarted.Factory.Elements.Count);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ExistingGlobalOptOutIsHonored() {
            using (var context = new NotificationContext()) {
                context.Service.GeneralOptions.PromptForPythonVersionNotSupported = false;
                context.Service.GeneralOptions.Save();
                using (var bar = context.CreateBar()) {
                    Check(bar);
                }
                Assert.AreEqual(0, context.Factory.Elements.Count);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void GlobalOptOutAndMissingInterpreterCloseVisibleWarnings() {
            using (var context = new NotificationContext())
            using (var bar = context.CreateBar()) {
                Check(bar);
                context.Service.GeneralOptions.PromptForPythonVersionNotSupported = false;
                Check(bar);
                Assert.IsTrue(context.Factory.Elements.Single().IsClosed);

                context.Service.GeneralOptions.PromptForPythonVersionNotSupported = true;
                context.Active = CreateFactory(new Version(3, 17));
                Check(bar);
                Assert.AreEqual(2, context.Factory.Elements.Count);
                context.Active = null;
                Check(bar);
                Assert.IsTrue(context.Factory.Elements[1].IsClosed);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ClosingWithoutOptOutOnlySuppressesTheCurrentContext() {
            using (var context = new NotificationContext()) {
                using (var bar = context.CreateBar()) {
                    Check(bar);
                    context.Factory.Elements.Single().Close();
                    Check(bar);
                    Assert.AreEqual(1, context.Factory.Elements.Count);
                }
                using (var reopened = context.CreateBar()) {
                    Check(reopened);
                    Assert.AreEqual(2, context.Factory.Elements.Count);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void StaleActionCannotDismissTheNewInterpreterVersion() {
            using (var context = new NotificationContext())
            using (var bar = context.CreateBar()) {
                Check(bar);
                var previous = context.Factory.Elements.Single();
                context.Active = CreateFactory(new Version(3, 17));
                Check(bar);
                previous.ClickDismiss(bar);

                Assert.IsFalse(context.Factory.Elements[1].IsClosed);
                using (var reopened = context.CreateBar()) {
                    Check(reopened);
                    Assert.AreEqual(3, context.Factory.Elements.Count);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void StaleActionAfterCloseDoesNotPersistDismissal() {
            using (var context = new NotificationContext())
            using (var bar = context.CreateBar()) {
                Check(bar);
                var previous = context.Factory.Elements.Single();
                previous.Close();
                previous.ClickDismiss(bar);

                using (var reopened = context.CreateBar()) {
                    Check(reopened);
                    Assert.AreEqual(2, context.Factory.Elements.Count);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void LateCloseCallbackCannotDiscardTheReplacementInfoBar() {
            using (var context = new NotificationContext()) {
                var bar = context.CreateBar();
                Check(bar);
                var previous = context.Factory.Elements.Single();
                context.Active = CreateFactory(new Version(3, 17));
                Check(bar);
                bar.OnClosed(previous);
                bar.Dispose();

                Assert.AreEqual(2, context.Factory.Elements.Count);
                Assert.IsTrue(context.Factory.Elements[1].IsClosed);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void DismissingPython2DoesNotSuppressAnUnknownPython3Version() {
            using (var context = new NotificationContext()) {
                context.Active = CreateFactory(new Version(2, 7));
                using (var bar = context.CreateBar()) {
                    Check(bar);
                    context.Factory.Elements.Single().ClickDismiss(bar);
                    context.Active = CreateFactory(new Version(3, 16));
                    Check(bar);
                    Assert.AreEqual(2, context.Factory.Elements.Count);
                    Assert.IsTrue(context.Service.GeneralOptions.PromptForPythonVersionNotSupported);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void PendingCreationIsNotDuplicated() {
            using (var context = new NotificationContext()) {
                context.SetHostReady(false);
                using (var bar = context.CreateBar()) {
                    Check(bar);
                    Check(bar);
                    context.ComponentManager.Idle();
                    Assert.AreEqual(0, context.Factory.Elements.Count);
                    context.SetHostReady(true);
                    context.ComponentManager.Idle();
                    Assert.AreEqual(1, context.Factory.Elements.Count);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void InterpreterSwitchCancelsPendingCreationWithoutLosingFutureWarning() {
            using (var context = new NotificationContext()) {
                context.SetHostReady(false);
                using (var bar = context.CreateBar()) {
                    Check(bar);
                    context.Active = CreateFactory(new Version(3, 15));
                    Check(bar);
                    context.SetHostReady(true);
                    context.ComponentManager.Idle();
                    Assert.AreEqual(0, context.Factory.Elements.Count);

                    context.Active = CreateFactory(new Version(3, 16));
                    Check(bar);
                    Assert.AreEqual(1, context.Factory.Elements.Count);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void DisposeCancelsPendingCreationAndQueuedChecks() {
            using (var context = new NotificationContext()) {
                context.SetHostReady(false);
                var bar = context.CreateBar();
                Check(bar);
                context.UIThread.QueueChecks = true;
                var check = bar.CheckAsync();
                bar.Dispose();
                bar.Dispose();
                Assert.IsTrue(bar.CheckAsync().IsCompleted);
                context.ThrowOnInterpreterRead = true;
                context.SetHostReady(true);
                context.UIThread.RunQueuedChecks();
                check.GetAwaiter().GetResult();
                context.ComponentManager.Idle();
                Assert.AreEqual(0, context.Factory.Elements.Count);
            }
        }

        private static void Check(PythonNotSupportedInfoBar bar) => bar.CheckAsync().GetAwaiter().GetResult();

        private static IPythonInterpreterFactory CreateFactory(Version version, string id = "environment") {
            return new MockPythonInterpreterFactory(new InterpreterConfiguration(
                id,
                "env (Python " + version + ")",
                typeof(PythonNotSupportedInfoBarTests).Assembly.Location,
                version: version));
        }

        private sealed class NotificationContext : IDisposable {
            private readonly MockServiceProvider _site;
            private readonly MockVsShell _shell = new MockVsShell();
            public readonly FakeInfoBarFactory Factory = new FakeInfoBarFactory();
            public readonly FakeComponentManager ComponentManager = new FakeComponentManager();
            public readonly QueuedUIThread UIThread = new QueuedUIThread();
            public readonly MockPythonToolsOptionsService Store;
            public readonly PythonToolsService Service;
            public IPythonInterpreterFactory Active = CreateFactory(new Version(3, 16));
            public bool ThrowOnInterpreterRead;

            public NotificationContext(MockPythonToolsOptionsService store = null) {
                _site = PythonToolsTestUtilities.CreateMockServiceProvider();
                var previous = (PythonToolsService)_site.GetService(typeof(PythonToolsService));
                Store = store ?? (MockPythonToolsOptionsService)_site.GetService(typeof(IPythonToolsOptionsService));
                previous.Dispose();
                _site.AddService(typeof(IPythonToolsOptionsService), Store);
                Service = new PythonToolsService(_site, true);
                _site.AddService(typeof(PythonToolsService), Service);
                _site.AddService(typeof(UIThreadBase), UIThread);
                _site.AddService(typeof(SVsShell), _shell);
                _site.AddService(typeof(SVsInfoBarUIFactory), Factory);
                _site.AddService(typeof(SOleComponentManager), ComponentManager);
                SetHostReady(true);
            }

            public PythonNotSupportedInfoBar CreateBar(string context = InfoBarContexts.Project) {
                return new PythonNotSupportedInfoBar(_site, context, () => {
                    if (ThrowOnInterpreterRead) {
                        throw new InvalidOperationException("The interpreter was read after context disposal.");
                    }
                    return Active;
                });
            }

            public void SetHostReady(bool ready) {
                _shell.Properties[(int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost] =
                    ready ? (object)Factory : null;
            }

            public void Dispose() => Service.Dispose();
        }

        private sealed class FakeInfoBarFactory : IVsInfoBarUIFactory, IVsInfoBarHost {
            public readonly List<FakeInfoBarElement> Elements = new List<FakeInfoBarElement>();

            public IVsInfoBarUIElement CreateInfoBar(IVsInfoBar model) {
                var element = new FakeInfoBarElement(model);
                Elements.Add(element);
                return element;
            }

            public void AddInfoBar(IVsUIElement element) { }
            public void RemoveInfoBar(IVsUIElement element) { }
        }

        private sealed class FakeInfoBarElement : IVsInfoBarUIElement {
            private IVsInfoBarUIEvents _events;
            public readonly IVsInfoBar Model;
            public bool IsClosed;

            public FakeInfoBarElement(IVsInfoBar model) => Model = model;

            public void ClickDismiss(PythonNotSupportedInfoBar bar) {
                bar.OnActionItemClicked(this, Model.ActionItems.GetItem(0));
            }

            public int Close() {
                IsClosed = true;
                _events?.OnClosed(this);
                return 0;
            }

            public int Advise(IVsInfoBarUIEvents events, out uint cookie) {
                _events = events;
                cookie = 1;
                return 0;
            }

            public int Unadvise(uint cookie) {
                _events = null;
                return 0;
            }

            public int get_DataSource(out IVsUISimpleDataSource source) { throw new NotImplementedException(); }
            public int put_DataSource(IVsUISimpleDataSource source) { throw new NotImplementedException(); }
            public int TranslateAccelerator(IVsUIAccelerator accelerator) { throw new NotImplementedException(); }
            public int GetUIObject(out object uiObject) { throw new NotImplementedException(); }
        }

        private sealed class QueuedUIThread : UIThreadBase {
            private readonly Queue<Action> _checks = new Queue<Action>();
            public bool QueueChecks;
            public override bool InvokeRequired => false;
            public override void Invoke(Action action) => action();
            public override T Invoke<T>(Func<T> action) => action();
            public override void MustBeCalledFromUIThreadOrThrow() { }

            public override Task InvokeAsync(Action action) {
                if (!QueueChecks) {
                    action();
                    return Task.CompletedTask;
                }
                var completion = new TaskCompletionSource<object>();
                _checks.Enqueue(() => {
                    try {
                        action();
                        completion.SetResult(null);
                    } catch (Exception ex) {
                        completion.SetException(ex);
                    }
                });
                return completion.Task;
            }

            public void RunQueuedChecks() {
                while (_checks.Count != 0) {
                    _checks.Dequeue()();
                }
            }

            public override Task<T> InvokeAsync<T>(Func<T> action) => Task.FromResult(action());
            public override Task InvokeAsync(Action action, CancellationToken token) => InvokeAsync(action);
            public override Task<T> InvokeAsync<T>(Func<T> action, CancellationToken token) => InvokeAsync(action);
            public override Task InvokeTask(Func<Task> action) => action();
            public override Task<T> InvokeTask<T>(Func<Task<T>> action) => action();
            public override void InvokeTaskSync(Func<Task> action, CancellationToken token) => action().GetAwaiter().GetResult();
            public override T InvokeTaskSync<T>(Func<Task<T>> action, CancellationToken token) => action().GetAwaiter().GetResult();
        }

        private sealed class FakeComponentManager : IOleComponentManager {
            private readonly Dictionary<uint, IOleComponent> _components = new Dictionary<uint, IOleComponent>();
            private uint _nextCookie;

            public void Idle() {
                foreach (var component in _components.Values.ToArray()) {
                    component.FDoIdle(0);
                }
            }

            public int FRegisterComponent(IOleComponent component, OLECRINFO[] info, out uint cookie) {
                cookie = ++_nextCookie;
                _components.Add(cookie, component);
                return 0;
            }

            public int FRevokeComponent(uint cookie) {
                _components.Remove(cookie);
                return 0;
            }

            public int FContinueIdle() { throw new NotImplementedException(); }
            public int FCreateSubComponentManager(object outer, object provider, ref Guid iid, out IntPtr result) { throw new NotImplementedException(); }
            public int FGetActiveComponent(uint flags, out IOleComponent component, OLECRINFO[] info, uint reserved) { throw new NotImplementedException(); }
            public int FGetParentComponentManager(out IOleComponentManager manager) { throw new NotImplementedException(); }
            public int FInState(uint state, IntPtr value) { throw new NotImplementedException(); }
            public int FOnComponentActivate(uint cookie) { throw new NotImplementedException(); }
            public int FOnComponentExitState(uint cookie, uint state, uint context, uint count, IOleComponentManager[] excluded) { throw new NotImplementedException(); }
            public int FPushMessageLoop(uint cookie, uint reason, IntPtr data) { throw new NotImplementedException(); }
            public int FReserved1(uint reserved, uint message, IntPtr wParam, IntPtr lParam) { throw new NotImplementedException(); }
            public int FSetTrackingComponent(uint cookie, int track) { throw new NotImplementedException(); }
            public int FUpdateComponentRegistration(uint cookie, OLECRINFO[] info) { throw new NotImplementedException(); }
            public void OnComponentEnterState(uint cookie, uint state, uint context, uint count, IOleComponentManager[] excluded, uint reserved) { throw new NotImplementedException(); }
            public void QueryService(ref Guid service, ref Guid iid, out IntPtr value) { throw new NotImplementedException(); }
        }
    }
}
