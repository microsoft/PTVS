
TODO: Write more here

Enabling FastCGI
================

Run wfastcgi-enable to add to IIS configuration.

TODO: Write more here


IIS Express
-----------

All arguments passed to ``wfastcgi-enable`` or ``wfastcgi-disable1` are assumed to be the start of the configure command
if the first argument is a valid file. 

For example, the following command will enable wfastcgi with IIS Express and a specific host configuration:

    wfastcgi-enable 'C:\Program Files (x86)\IIS Express\appcmd.exe' /apphostconfig:C:\Path\To\applicationhost.config

You can disable wfastcgi in the same configuration file using `wfastcgi-disable` with the same options:

    wfastcgi-disable 'C:\Program Files (x86)\IIS Express\appcmd.exe' /apphostconfig:C:\Path\To\applicationhost.config
