Quick answer
============

PTVS 2.0 is installed in:

```
%ProgramFiles(x86)%\Microsoft Visual Studio <VS version>\Common7\IDE\Extensions\Microsoft\Python Tools for Visual Studio\2.0
```

A registry value `InstallDir` containing the full installation directory is stored at:

```
(64-bit Windows) HKEY_LOCAL_MACHINE\Software\Wow6432Node\Microsoft\PythonTools\<VS version>
(32-bit Windows) HKEY_LOCAL_MACHINE\Software\Microsoft\PythonTools\<VS version>
```

where `VS version` is `12.0`, `11.0` or `10.0`. See below for full installation paths.

All Users
=========

By default, PTVS is installed for all users on the computer. Full installation paths for all released versions are shown below. (For 32-bit versions of Windows, replace `%ProgramFiles(x86)%` with `%ProgramFiles%`.)

|| **PTVS Version** || **VS Version** || **Path** ||
|| 2.1 || 2013 || `%ProgramFiles(x86)%\Microsoft Visual Studio 12.0\Common7\IDE\Extensions\Microsoft\Python Tools for Visual Studio\2.1` ||
|| 2.1 || 2012 || `%ProgramFiles(x86)%\Microsoft Visual Studio 11.0\Common7\IDE\Extensions\Microsoft\Python Tools for Visual Studio\2.1` ||
|| 2.1 || 2010 || `%ProgramFiles(x86)%\Microsoft Visual Studio 10.0\Common7\IDE\Extensions\Microsoft\Python Tools for Visual Studio\2.1` ||
|| 2.0 || 2013 || `%ProgramFiles(x86)%\Microsoft Visual Studio 12.0\Common7\IDE\Extensions\Microsoft\Python Tools for Visual Studio\2.0` ||
|| 2.0 || 2012 || `%ProgramFiles(x86)%\Microsoft Visual Studio 11.0\Common7\IDE\Extensions\Microsoft\Python Tools for Visual Studio\2.0` ||
|| 2.0 || 2010 || `%ProgramFiles(x86)%\Microsoft Visual Studio 10.0\Common7\IDE\Extensions\Microsoft\Python Tools for Visual Studio\2.0` ||
|| 1.5 || 2012 || `%ProgramFiles(x86)%\Microsoft Visual Studio 11.0\Common7\IDE\Extensions\Microsoft\Python Tools for Visual Studio\1.5` ||
|| 1.5 || 2010 || `%ProgramFiles(x86)%\Microsoft Visual Studio 10.0\Common7\IDE\Extensions\Microsoft\Python Tools for Visual Studio\1.5` ||
|| 1.1 || 2010 || `%ProgramFiles(x86)%\Microsoft Visual Studio 10.0\Common7\IDE\Extensions\Microsoft\Python Tools for Visual Studio\1.1` ||
|| 1.0 || 2010 || `%ProgramFiles(x86)%\Microsoft Visual Studio 10.0\Common7\IDE\Extensions\Microsoft\Python Tools for Visual Studio\1.0` ||


Just For You
============

Prior to PTVS 2.0, an option was available to install PTVS for all users or just for you. If installed for all users, the installation paths are shown above in the previous section. Full installation paths for all released versions are shown below.

|| **PTVS Version** || **VS Version** || **Path** ||
|| 2.1 || All || Not supported. ||
|| 2.0 || All || Not supported. ||
|| 1.5 || 2012 || `%LocalAppData%\Microsoft\VisualStudio\11.0\Extensions\Microsoft\Python Tools for Visual Studio\1.5` ||
|| 1.5 || 2010 || `%LocalAppData%\Microsoft\VisualStudio\10.0\Extensions\Microsoft\Python Tools for Visual Studio\1.5` ||
|| 1.1 || 2010 || `%LocalAppData%\Microsoft\VisualStudio\10.0\Extensions\Microsoft\Python Tools for Visual Studio\1.1` ||
|| 1.0 || 2010 || `%LocalAppData%\Microsoft\VisualStudio\10.0\Extensions\Microsoft\Python Tools for Visual Studio\1.0` ||
