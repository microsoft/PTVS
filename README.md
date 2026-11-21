# Python tools for Visual Studio

[![Build Status](https://devdiv.visualstudio.com/DevDiv/_apis/build/status/Python/PTVS-Build-Dev17?branchName=main)](https://devdiv.visualstudio.com/DevDiv/_build/latest?definitionId=14121&branchName=main)
[![Join the chat at https://gitter.im/Microsoft/PTVS](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/Microsoft/PTVS?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Python support for Visual Studio is developed and managed here.
* Visit our [product overview and downloads page](https://aka.ms/PTVS), read our [documentation](https://aka.ms/PTVSDocs) and watch our (old) [YouTube tutorials](http://aka.ms/PTVSTutorial).
* Feel free to file issues or ask questions on our [issue tracker](http://github.com/Microsoft/PTVS/issues), or use the Send Feedback functionality in Visual Studio to report a problem or suggest a feature.
* We welcome code contributions - see [Contributing](https://github.com/Microsoft/PTVS/wiki/Contributing-to-PTVS) for information. Documentation contributions may be made at the [visualstudio-docs](https://github.com/Microsoft/visualstudio-docs/tree/master/docs/python) repository.
* For all other documentation, please see the [Wiki](https://github.com/Microsoft/PTVS/wiki)

## Unsupported Python version notifications

When a project or workspace uses an unsupported Python version, Visual Studio
shows a nonblocking information bar instead of a modal warning during solution
load. **Don't show this message again** remembers the dismissal for that Python
major/minor version across projects, workspaces, and Visual Studio restarts.
A different unsupported version can still show its own warning. Closing the bar
without that action only dismisses it for the current project or workspace
session. This does not change which Python versions or features are supported.

## Building the VS 18 language client

The VS 18 target uses `Microsoft.VisualStudio.LanguageServer.Protocol.Internal`
18.14.12 and System.Text.Json for its Pylance protocol messages. Older VS targets
retain their legacy protocol references and Newtonsoft.Json message handling.
This does not remove Newtonsoft.Json from unrelated PTVS components.

PTVS compiles against the Internal package but does not ship the DLL; at runtime
it binds to the copy that Visual Studio ships. Visual Studio redirects older
18.14 references to its newer copy but cannot redirect to an older one, so keep
the package pin at or below the version shipped by every VS build that PTVS
targets.

`Build\nuget.config` includes `msft_consumption` for general dependencies and
the public `vs-impl` and `vssdk` feeds for the VS 18 dependency cohort. Internal
18.14.12 is published by `vs-impl`; the matching editor packages are published
by `vssdk`. Keep those direct sources until all pinned versions are available
through `msft_consumption`; a Reader cannot populate that feed from its
configured upstreams.

Runtime qualification requires a VS main build containing the consolidated LSP
client, not just a newer build toolset or a newer protocol package. Verify
completion and commit in both the editor and Interactive Window, hover and
signature help, navigation, diagnostics, code actions and rename, workspace and
interpreter settings, file-change notifications, and solution shutdown/reopen.
The existing PTVS file-watching, workspace, cancellation, and stream
compatibility code is retained; replacing it with built-in VS support is a
separate change.

## Visual Studio 2017 and Visual Studio 2019 installation

The Python development workload is available as part of [Visual Studio](https://aka.ms/PTVS) Community, Professional and Enterprise. To install, run the [normal VS installer](https://visualstudio.com/vs/downloads?wt.mc_id=github_microsoft_com) and select either the **Python development workload** or the **Data science and analytical applications workload**.

## Visual Studio 2015 and earlier

Prior to Visual Studio 2017, Python support was released as a standalone extension. We are no longer actively developing these versions, but if you are unable to upgrade to Visual Studio 2017 yet, you can use these older installers to get Python support.

[PTVS 2.2.6](https://github.com/Microsoft/PTVS/releases/v2.2.6) is the latest available version for Visual Studio 2015. You can also find it in the Visual Studio 2015 installer by selecting "Custom" and looking under the "Programming Languages" category.

[PTVS 2.2.2](https://github.com/Microsoft/PTVS/releases/v2.2.2) is the last release for Visual Studio 2013 users.

Thank you for using and supporting Python Tools for Visual Studio.
