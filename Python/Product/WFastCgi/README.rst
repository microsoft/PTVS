WFastCGI
========

wfastcgi.py provides a bridge between `IIS <http://www.iis.net/>`__ and Python
using WSGI and FastCGI, similar to what ``mod_python`` provides for Apache HTTP
Server.

It can be used with any Python web application or framework that supports WSGI,
and provides an efficient way to handle requests and process pools through IIS.

Installation
============

Downloading Package
-------------------

To install via the Python Package Index (PyPI), type:

.. code:: shell

    pip install wfastcgi

Installing IIS and FastCGI
--------------------------

See the `IIS Installation <http://www.iis.net/learn/install>`__ page for
information about installing IIS on your version of Windows.

The Application Development/CGI package is required for use with `wfastcgi`.

Enabling wfastcgi
-----------------

Once ``wfastcgi`` and IIS are installed, run ``wfastcgi-enable`` as an
administrator to enable ``wfastcgi`` in the IIS configuration. This will
configure a CGI application that can then be specified as a 
`route handler <#route-handlers>`__.

.. code:: shell

    wfastcgi-enable

To disable ``wfastcgi`` before uninstalling, run ``wfastcgi-disable``.

.. code:: shell

    wfastcgi-disable
    pip uninstall wfastcgi

**Note**: uninstalling ``wfastcgi`` does not automatically unregister the CGI
application.


If the first argument passed to ``wfastcgi-enable`` or ``wfastcgi-disable`` is
a valid file, the entire command line is used to register or unregister the CGI
handler.

For example, the following command will enable wfastcgi with IIS Express and a
specific host configuration:

.. code:: shell

    wfastcgi-enable "C:\Program Files (x86)\IIS Express\appcmd.exe"
        /apphostconfig:C:\Path\To\applicationhost.config

You can disable wfastcgi in the same configuration file using
``wfastcgi-disable`` with the same options:

.. code:: shell

    wfastcgi-disable "C:\Program Files (x86)\IIS Express\appcmd.exe"
        /apphostconfig:C:\Path\To\applicationhost.config

.. route-handlers

Route Handlers
==============

Routing requests to your Python application requires some site-local
configuration. In your site's ``web.config`` file, you will need to add a
handler and some app settings:

.. code:: xml

    <configuration>
      <system.webServer>
        <handlers>
          <add name="Python FastCGI"
               path="*"
               verb="*"
               modules="FastCgiModule"
               scriptProcessor="C:\Python36\python.exe|C:\Python36\Lib\site-packages\wfastcgi.py"
               resourceType="Unspecified"
               requireAccess="Script" />
        </handlers>
      </system.webServer>
    
      <appSettings>
        <!-- Required settings -->
        <add key="WSGI_HANDLER" value="my_app.wsgi_app()" />
        <add key="PYTHONPATH" value="C:\MyApp" />
        
        <!-- Optional settings -->
        <add key="WSGI_LOG" value="C:\Logs\my_app.log" />
        <add key="WSGI_RESTART_FILE_REGEX" value=".*((\.py)|(\.config))$" />
        <add key="APPINSIGHTS_INSTRUMENTATIONKEY" value="__instrumentation_key__" />
        <add key="DJANGO_SETTINGS_MODULE" value="my_app.settings" />
        <add key="WSGI_PTVSD_SECRET" value="__secret_code__" />
        <add key="WSGI_PTVSD_ADDRESS" value="ipaddress:port" />
      </appSettings>
    </configuration>


The value for ``scriptProcessor`` is displayed in the output of
``wfastcgi-enable`` and may vary from machine to machine. The values for
``path`` and ``verb`` may also be customized to further restrict the requests
for which this handler will be used.

The ``name`` value may be used in nested ``web.config`` files to exclude this
handler. For example, adding a ``web.config`` to your ``static/`` subdirectory
containing ``<remove name="Python FastCGI" />`` will prevent IIS from serving
static files through your Python app.

The provided app settings are translated into environment variables and can be
accessed from your Python application using ``os.getenv``. The following
variables are used by ``wfastcgi``.

WSGI_HANDLER
------------

This is a Python name that evaluates to the WSGI application object. It is a
series of dotted names that are optionally called with no parameters. When
resolving the handler, the following steps are used:

1. As many names as possible are loaded using ``import``. The last name is
   never imported.

2. Once a module has been obtained, each remaining name is retrieved as an
   attribute. If ``()`` follows the name, it is called before getting the
   following name.

Errors while resolving the name are returned as a simple 500 error page.
Depending on your IIS configuration, you may only receive this page when
accessing the site from the same machine.

PYTHONPATH
----------

Python is already running when this setting is converted into an environment
variable, so ``wfastcgi`` performs extra processing to expand environment
variables in its value (including those added from app settings) and to expand
``sys.path``.

If you are running an implementation of Python that uses a variable named
something other than ``PYTHONPATH``, you should still specify this value as
``PYTHONPATH``.

WSGI_LOG
--------

This is a full path to a writable file where logging information is written.
This logging is not highly efficient, and it is recommended that this setting
only be specified for debugging purposes.

WSGI_RESTART_FILE_REGEX
-----------------------

The regular expression used to identify when changed files belong to your
website. If a file belonging to your site changes, all active CGI processes
will be terminated so that the new files can be loaded.

By default, all ``*.py`` and ``*.config`` files are included. Specify an empty
string to disable auto-restart.

APPINSIGHTS_INSTRUMENTATIONKEY
------------------------------

Providing an instrumentation key with this value will enable request tracing
with `Application Insights <http://pypi.org/project/applicationinsights>`__
for your entire site. If you have not installed the ``applicationinsights``
package, a warning is written to ``WSGI_LOG`` (if enabled) but the site will
operate normally.

Application Insights is a low-overhead monitoring system for tracking your
application's health and performance. When enabled, all errors in your site
will be reported through Application Insights.

DJANGO_SETTINGS_MODULE
----------------------

A commonly used registry key when deploying sites built using Django. Typically
Django sites will set ``WSGI_HANDLER`` to
``django.core.handlers.wsgi.WSGIHandler()`` and load app-specific settings
through the module specified by this value.

Sites using frameworks other than Django do not need to specify this value.

WSGI_PTVSD_SECRET
-----------------

Providing an arbitrary string here and including the
`ptvsd <https://pypi.org/project/ptvsd>`__ module in your environment will
automatically enable remote debugging of your web site. The string in this
application setting should be treated as a password, and needs to be provided
when attaching to the running site.

WSGI_PTVSD_ADDRESS
------------------

When ``WSGI_PTVSD_SECRET`` is specified, this value may also be specified to
override the default listening address for remote debugging. By default,
your site will listen on ``localhost:5678``, but in many cases you may need
to change this to ``0.0.0.0:some-port`` in order to attach remotely.

Remember that you will also need to forward the port through any firewalls
you might have configured.

