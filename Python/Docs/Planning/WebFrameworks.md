Note
====

This is a planning document that reflects our intent at time of writing. The final release may not match the feature set detailed here based on, among other things, user feedback.

Overview
========

Web project
-----------

Add project properties for:

* debug/run server commands
 * these two commands are used by F5 and Ctrl+F5 respectively and do not appear in menus
 * they are exposed as properties to allow user/project customization
 * our .targets file will convert them into PTVSCommand items (see below) to avoid code duplication
* custom environment variables (p1, maybe add to all project launchers)

There will be a new launcher for web projects that will use these commands. We will not add a "Web" tab alongside the existing "Debug" tab in Project Properties.

These environment variables are always set based on the existing properties when running commands:

|| **Key** || **Value** || **Default** ||
|| `%SERVER_HOST%` || Configured local address || `127.0.0.1` ||
|| `%SERVER_PORT%` || Configured local port || random ||

*Currently, these properties are only used on Django's `manage.py` command line. However, not all frameworks have a launcher script that accepts command line arguments, so using environment variables is more flexible.*

Projects will have a command to install/upgrade their dependencies, which will be implemented as PTVSCommand items and invoke pip.

Targets
-------

* Add "Microsoft.PythonTools.targets" and "...Web.targets" files to MSBuild directory.
 * This will take most of the contents of Django.targets and reuse it (Django.targets will them import it from Web.targets).
 * Generalize the web.config generator in Web.targets
* Add build task for specifying commands (see below) to PythonTools.targets
 * Also one for parsing/converting from Python command line ("path\to\file.py args..." and "-m package.module args...")
* Add build task to resolve interpreter path to PythonTools.targets
 * This will replace the current hardcoded GUIDs/regkeys
* Add build task to convert .py filename to importable module name to PythonTools.targets
* Add properties $(StartupPath) and $(StartupModule) derived from $(StartupFile) and $(ProjectHome) to PythonTools.targets
 * Properties derived from the active environment are already part of Django.targets and will also be available

We can add the PythonTools.targets file to all of our normal project templates, but there is no need to upgrade old projects (yet).

The command format allows .pyproj/.targets files to declare the commands they support and provide new commands that are integrated with PTVS. The target returns one item created with the PTVSCommand task detailing the command to execute.

The PTVSCommands property is a list of targets that should be displayed in the Project menu.

Conceptually (barring invalid syntax):

```xml
    <PropertyGroup>
        <PTVSCommands>Name1;...</PTVSCommands>
    </PropertyGroup>
    ...
    <Target Name="Name1" Label="Display Name" Returns="@(Commands)">
        <PTVSCommand Target="filename or module name"
                     TargetType="executable/script/module"
                     Arguments="..."
                     Immediate="'$(BuildingInsideVisualStudio)' != 'true'"
                     ExecuteIn="console/repl/output">
            <Output TaskParameter="Command"
                    ItemName="Commands" />
        </PTVSCommand>
    </Target>
```

The three target types determine whether `Target` is passed directly to `Process.Start` (executable), passed as an argument to the active interpreter (script), or passed as a `-m` argument to the active interpreter (module).

ExecuteIn must be "console" for executables or when Immediate is true. *We will probably need to add `IPythonReplEvaluator2` to support executing modules.*

Immediate is used to execute the command immediately rather than returning the details. It defaults to false and should normally be set based on the `$(BuildingInsideVisualStudio)` property. When the target is invoked using `msbuild /t:Name1 ...`, the command will be executed immediately.

The Label attribute can be localized by using the following format: `resource:AssemblyName;ResourcesName;StringId`

For example: `resource:Microsoft.PythonTools;Microsoft.PythonTools.Resources;Name1`

*We won't be localizing labels initially, though we will use resource strings. We need to declare the resource format now because we can't easily add another property later.*

The assembly name is used to load the assembly and pass it with the resources name to `new ResourceManager`. `ResourceManager.GetString()` is used with the string ID to retrieve the display string.
If Label is unspecified, or the resource cannot be loaded, the target's Name attribute is displayed.

An example of the 'install' command:

```xml
    <Target Name="InstallFlask" Label="Install Flask package" Return="@(Commands)">
        <PTVSCommand Target="pip"
                     TargetType="module"
                     Arguments="install --upgrade flask"
                     Immediate="'$(BuildingInsideVisualStudio)' != 'true'"
                     ExecuteIn="output">
            <Output TaskParameter="Command"
                    ItemName="Commands" />
        </PTVSCommand>
    </Target>
```

Templates
---------

* Add "Web" subdirectory for new project templates
* Add "Web" subdirectory for new item templates
 * Rename "Python Editor" to "Python"

New project templates are:

* Flask Project
 * runserver.py
 * One app
* Bottle Project
 * One app
* Pyramid Project
 * One app
* Empty Web Project

New item templates are:

* Flask App
 * App\
 * App\static\
 * App\templates\
 * App\templates\index.html
 * App\__init__.py
 * App\views.py
* Bottle App
 * App\
 * App\static\
 * App\templates\
 * App\templates\index.html
 * App\__init__.py (uses the `app = default_app.pop()` model)
* Pyramid App
 * App\
 * App\static\
 * App\templates\
 * App\templates\index.html
 * App\__init__.py
 * App\views.py
 * App\models.py

TurboGears will not get a template. The simple layout is irrelevantly simple and the complicated layout (generated by a script) has many user options. We can test it and (assuming it works) document how to start from existing code/Empty Web Project and set the commands.



Flask
=====

[Documentation](http://flask.pocoo.org/docs/)

[App layout](http://flask.pocoo.org/docs/patterns/packages/#larger-applications)

Commands
--------

* install: `-m pip install --upgrade flask` 
* testserver: `runserver.py --debug --host "%SERVER_HOST%" --port %SERVER_PORT% $(StartupModule)`
* runserver: `runserver.py --host "%SERVER_HOST%" --port %SERVER_PORT% $(StartupModule)`

Templating Engines
------------------

* [Jinja 2](#jinja-2)

Bottle
======

[Documentation](http://bottlepy.org/docs/dev/index.html)

Commands
--------

* install: `-m pip install --upgrade bottle`
* testserver: `-m bottle --debug -b "%SERVER_HOST%:%SERVER_PORT%" $(StartupModule):app`
* runserver: `-m bottle $(StartupModule):app`

Templating Engines
------------------

* [Jinja 2](#jinja-2)
* [Mako](#mako)
* [Cheetah](#cheetah)

TurboGears
==========

[Documentation](http://turbogears.readthedocs.org/en/latest/)

TurboGears uses a complicated app layout that is not going to work well as a fixed template.


Commands
--------

* install: `-m pip install --upgrade tg.devtools`
* testserver: `-m gearbox serve`
* runserver: `-m gearbox serve -c production.ini`

Templating Engines
------------------

* [Genshi](#genshi)
* [Mako](#mako)
* [Jinja](#jinja)

Pyramid
=======

[Documentation](http://docs.pylonsproject.org/projects/pyramid/en/1.4-branch/index.html)

[App layout](http://docs.pylonsproject.org/projects/pyramid/en/latest/tutorials/wiki2/basiclayout.html)

`pip install pyramid`

Templating Engines
------------------

* [Mako](#mako)
* [Chameleon](#chameleon)

Commands
--------

* testserver: `$(StartupPath) --debug`?
* runserver: `$(StartupPath)`

Web2Py
======

[Documentation](http://web2py.com/book)

Cannot be installed using pip.

Templating Engines
------------------

* Custom engine

Templating Engine Reference
===========================

***This section is for reference only. We are not necessary going to implement any support for these in 2.1.***

Jinja 2
-------

[http://jinja.pocoo.org/docs/](http://jinja.pocoo.org/docs/)

* [Flask](#flask)
* [Bottle](#bottle)

```
    <title>{% block title %}{% endblock %}</title>
    <ul>
    {% for user in users %}
      <li><a href="{{ user.url }}">{{ user.username }}</a></li>
    {% endfor %}
    </ul>
```

Genshi
------

[http://genshi.edgewall.org/wiki/Documentation/templates.html](http://genshi.edgewall.org/wiki/Documentation/templates.html)

* [TurboGears](#turbogears)

```
    <?python
      title = "A Genshi Template"
      fruits = ["apple", "orange", "kiwi"]
    ?>
    <html xmlns:py="http://genshi.edgewall.org/">
      <head>
        <title py:content="title">This is replaced.</title>
      </head>
      <body>
        <p>These are some of my favorite fruits:</p>
        <ul>
          <li py:for="fruit in fruits">
            I like ${fruit}s
          </li>
        </ul>
      </body>
    </html>
```

Mako
----

[http://www.makotemplates.org/](http://www.makotemplates.org/)

* [Pyramid](#pyramid)
* [Bottle](#bottle)

```
    <%inherit file="base.html"/>
    <%
        rows = [[v for v in range(0,10)] for row in range(0,10)]
    %>
    <table>
        % for row in rows:
            ${makerow(row)}
        % endfor
    </table>

    <%def name="makerow(row)">
        <tr>
        % for name in row:
            <td>${name}</td>\
        % endfor
        </tr>
    </%def>
```

Cheetah
-------

[http://www.cheetahtemplate.org/index.html](http://www.cheetahtemplate.org/index.html)

* [Bottle](#bottle)

```html
    <html>
    <head><title>$title</title></head>
    <body>
      <table>
        #for $client in $clients
        <tr>
          <td>$client.surname, $client.firstname</td>
          <td><a href="mailto:$client.email">$client.email</a></td>
        </tr>
        #end for
      </table>
    </body>
    </html>
```

Chameleon
---------

[http://chameleon.readthedocs.org/en/latest/](http://chameleon.readthedocs.org/en/latest/)

* [Pyramid](#pyramid)

```html
    <html>
      <body>
        <h1>Hello, ${'world'}!</h1>
        <table>
          <tr tal:repeat="row 'apple', 'banana', 'pineapple'">
            <td tal:repeat="col 'juice', 'muffin', 'pie'">
               ${row.capitalize()} ${col}
            </td>
          </tr>
        </table>
      </body>
    </html>
```
