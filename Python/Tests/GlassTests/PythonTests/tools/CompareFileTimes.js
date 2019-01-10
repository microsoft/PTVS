var fileSystem = WScript.CreateObject("Scripting.FileSystemObject");

function Main() {
    if (WScript.Arguments.length == 1 && (WScript.Arguments(0) == "/?" || WScript.Arguments(0) == "-?")) {
        WScript.StdOut.WriteLine("Syntax: CompareFileTimes.js: <file1> <file2>");
        WScript.StdOut.WriteLine("Sets the %ERRORLEVEL% based on the comparison of the two files (-1=file2 is newer; 0=same time stamp; 1=file1 is newer)");
        return -2;
    }

    if (WScript.Arguments.length != 2)
    {
        WScript.StdOut.WriteLine("CompareFileTimes.js: Syntax error");
        return -2;
    }

    if (!fileSystem.FileExists(WScript.Arguments(0)))
    {
        WScript.StdOut.WriteLine("First argument file does not exist: " + WScript.Arguments(0));
        return 2;
    }

    if (!fileSystem.FileExists(WScript.Arguments(1)))
    {
        WScript.StdOut.WriteLine("Second argument file does not exist: " + WScript.Arguments(1));
        return 3;
    }

    var file0 = fileSystem.GetFile(WScript.Arguments(0));
    // Note: the 'Date' object returned from 'DateLastModified' is rather defective, so go through the Date constructor to work around
    var modifyTime0 = new Date(file0.DateLastModified).valueOf();
    var file1 = fileSystem.GetFile(WScript.Arguments(1));
    var modifyTime1 = new Date(file1.DateLastModified).valueOf();
    
    if (modifyTime0 < modifyTime1)
        return -1;
    else if (modifyTime0 == modifyTime1)
        return 0;
    else
        return 1;
}

var exitCode = Main();
WScript.Quit(exitCode);
