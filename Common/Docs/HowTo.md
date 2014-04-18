How to write Documentation
==========================

File Format
-----------

Source documents are written in Markdown with, as usual for Markdown, a few variations specific to our documents.

Source files must have the `.md` extension and be UTF-8 encoded (BOM is optional).

All of the basic formatting elements, such as **bold** and *italics* exist, however, underscores are ignored, so that we can write __dunder__ names.

* Bulleted lists use an asterisk at the start of the line
 * Subitems use spaces before the bullet

1. Numbered lists use any leading number...
 1. ...can also be indented...
* or specified with an asterisk once you've started

`Monospace` uses backticks, and blocks of code are delimited with three adjacent backticks:

```
My monochromatic code block
```

Language names (such as `python`, `javascript` or `csharp`) can be specified immediately after the first three backticks to perform formatting:

With `python`:

```python
def fob():
    for x in range(10):
        print x
    return True
```

With `javascript`:

```javascript
exports.index = function(req, res){
  res.render('index', { title: 'Express' })
};
```

With `csharp`:

```csharp
bool fob() {
    for (int x = 0; x < 10; ++i) {
        Console.WriteLine(x);
    }
    return True
}
```

-

One or more hyphens or asterisks alone on a line will create a horizontal rule.

* * *

Links are specified using `[text](url)` format: [text](url)

For simpler links and emails, just put them in `<angle brackets>`: <vspython@microsoft.com>

Images are specified using `![text](url)` format: ![an image example](Images\EditHTML.png)

Or to include style, just use an `<img>` tag: <img src="Images\EditHTML.png" style="border: #0000ff 3px solid; vertical-align: middle" />

---

We have some special link formats:

* `[src:Full.Class.Name]` will create a link to the source file on CodePlex containing the class:
 * [src:Microsoft.VisualStudioTools.Project.CommonProjectNode]
* `[file:Filename.ext]` will create a link based the filename (if unambiguous) or the path from the root:
 * [file:Common\Product\SharedProject\ProjectNode.cs]
 * [file:PythonProjectNode.cs]
* `[wiki:Page Title]` will create a link to a page on the CodePlex wiki, and `[wiki:"New Title" Page Title]` will change the display text:
 * [wiki:Python Environments]
 * [wiki:"page about Python envs"Python Environments]
* `[issue:number]` will link to an issue, and `[issue:"New Title"number]` will change the display text:
 * [issue:1234]
 * [issue:"Our very first issue" 1]
* `[video:"Video title" ID]` will create a centered thumbnail of YouTube video with the given ID with alt text of the thumbnail set to "Video title", which will open that video when clicked:
 * [video:"Mixed-mode debugging" wvJaKQ94lBY]

Other special conversions include:

>> ![Floated to the right](Images\EditHTML.png)

* `--(` and `--)` to delimit a layout block. These get converted into a div that does not allow floats to escape.
* `>>` floats the line to the right, like the image.


Documentation Project
---------------------

The source for the documentation is under source control. It is part of a Python Tools for Visual Studio project (`.pyproj`) -- the script that converts the documentation and uploads it to CodePlex is written in Python. The Visual Studio project contains all the Markdown files, references to the convert/upload script and necessary script arguments in the project settings.

To edit existing documentation, open the `.md` file using Solution Explorer and start editing. To add new documentation pages, add a New Item or copy/paste an existing .md file using Solution Explorer.

When you are done with your changes, convert the documentation to HTML to make sure your Markdown is rendered as expected (previews in Markdown editors won't be accurate).  To do this, run the project (F5 or CTRL-F5).  You will be prompted for some information:
1. Convert to HTML, answer **y**.
1. Upload to CodePlex, answer **n**.
After all files are converted, verify the contents of the generated HTML files.

When you are ready to upload your changes, check-out `User\codeplex.map` from source control. Then run the project (F5 or CTRL-F5). You will be prompted for some information:
1. Convert to HTML, answer **y** unless you are absolutely sure the HTML is already up to date (ie. you haven't modified anything since the last conversion).
1. Upload to CodePlex, answer **y**.
1. Enter your CodePlex user.
1. Enter your CodePlex password.

For more details on how the script works, including requirements and how to use it from the command line, see the following sections.


Converting to HTML
------------------

The [file:build.py] script can be run with Python 3.3 to generate HTML files for all `*.md` files in the branch. This script handles filename mapping and the tweaks that we use for Markdown. It includes a shebang line that is recognized by the `py.exe` launcher, allowing it to be run directly.

Running the script looks like:

```
cd Python\Docs
py ..\..\Common\Docs\build.py --convert --site pytools --doc-root Python\Docs
```
or
```
cd Nodejs\Docs
py ..\..\Common\Docs\build.py --convert --site nodejstools --doc-root Nodejs\Docs
```

If you omit required parameters, the script will prompt for the requirement information.

Requirements are:

* Python 3.3
* markdown2 (`pip install markdown2`)
* Pygments (`pip install pygments`)
* Pillow with JPEG and PNG codecs (`pip install Pillow`)

The first time the script is run, it will generate a file `maps.cache`, which contains all of the mappings used for the `src` and `file` links. This file should be deleted when source files are added, removed, moved or renamed, but will significantly speed up later refresh times.

Uploading to CodePlex
---------------------

Generated documentation can be uploaded to CodePlex using the [file:build.py] script. This script requires a CodePlex username and password with permissions to edit the wiki. Uploading must be done after the files have been converted to HTML.  Convert and upload can be done in one invocation of the script.

Running the script looks like:

```
cd Python\Docs
tf edit User\codeplex.map
py ..\..\Common\Docs\build.py --convert --upload --site pytools --doc-root Python\Docs --dir User --user MyName --password "MyPassword123"
```
or
```
cd Nodejs\Docs
tf edit User\codeplex.map
py ..\..\Common\Docs\build.py --convert --upload --site nodejstools --doc-root Nodejs\Docs --dir User --user MyName --password "MyPassword123"
```

If you omit required parameters, the script will prompt for the requirement information.

The `codeplex.map` file in the uploaded directory may be modified and needs to be writable before running the script. If it is not writable, an error will be displayed. The contents of the file include URLs for images and hashes for files that have been uploaded before. These will be used to determine whether the file has changes since it was last published to CodePlex, and to skip unchanged files.
