# Python Tools for Visual Studio
# Copyright(c) Microsoft Corporation
# All rights reserved.
# 
# Licensed under the Apache License, Version 2.0 (the License); you may not use
# this file except in compliance with the License. You may obtain a copy of the
# License at http://www.apache.org/licenses/LICENSE-2.0
# 
# THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
# IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
# MERCHANTABILITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.0.0.0"

import datetime
import os
import sys
import traceback

if sys.version_info[0] == 3:
    def to_str(value):
        return value.decode(sys.getfilesystemencoding())

    def execfile(path, global_dict):
        """Execute a file"""
        with open(path, 'r') as f:
            code = f.read()
        code = code.replace('\r\n', '\n') + '\n'
        exec(code, global_dict)
else:
    def to_str(value):
        return value.encode(sys.getfilesystemencoding())

def log(txt):
    """Logs fatal errors to a log file if WSGI_LOG env var is defined"""
    log_file = os.environ.get('WSGI_LOG')
    if log_file:
        f = open(log_file, 'a+')
        try:
            f.write('%s: %s' % (datetime.datetime.now(), txt))
        finally:
            f.close()

ptvsd_secret = os.getenv('WSGI_PTVSD_SECRET')
if ptvsd_secret:
    log('Enabling ptvsd ...\n')
    try:
        import ptvsd
        try:
            ptvsd.enable_attach(ptvsd_secret)
            log('ptvsd enabled.\n')
        except: 
            log('ptvsd.enable_attach failed\n')
    except ImportError:
        log('error importing ptvsd.\n');

def get_wsgi_handler(handler_name):
    if not handler_name:
        raise Exception('WSGI_ALT_VIRTUALENV_HANDLER env var must be set')
    
    if not isinstance(handler_name, str):
        handler_name = to_str(handler_name)
    
    module_name, _, callable_name = handler_name.rpartition('.')
    should_call = callable_name.endswith('()')
    callable_name = callable_name[:-2] if should_call else callable_name
    name_list = [(callable_name, should_call)]
    handler = None
    last_tb = ''

    while module_name:
        try:
            handler = __import__(module_name, fromlist=[name_list[0][0]])
            last_tb = ''
            for name, should_call in name_list:
                handler = getattr(handler, name)
                if should_call:
                    handler = handler()
            break
        except ImportError:
            module_name, _, callable_name = module_name.rpartition('.')
            should_call = callable_name.endswith('()')
            callable_name = callable_name[:-2] if should_call else callable_name
            name_list.insert(0, (callable_name, should_call))
            handler = None
            last_tb = ': ' + traceback.format_exc()
    
    if handler is None:
        raise ValueError('"%s" could not be imported%s' % (handler_name, last_tb))
    
    return handler

activate_this = os.getenv('WSGI_ALT_VIRTUALENV_ACTIVATE_THIS')
if not activate_this:
    raise Exception('WSGI_ALT_VIRTUALENV_ACTIVATE_THIS is not set')
if not isinstance(activate_this, str):
    activate_this = to_str(activate_this)
        
def get_virtualenv_handler():
    log('Activating virtualenv with %s\n' % activate_this)
    execfile(activate_this, dict(__file__=activate_this))

    log('Getting handler %s\n' % os.getenv('WSGI_ALT_VIRTUALENV_HANDLER'))
    handler = get_wsgi_handler(os.getenv('WSGI_ALT_VIRTUALENV_HANDLER'))
    log('Got handler: %r\n' % handler)
    return handler

def get_venv_handler():
    log('Activating venv with executable at %s\n' % activate_this)
    import site
    sys.executable = activate_this
    old_sys_path, sys.path = sys.path, []
    
    site.main()
    
    sys.path.insert(0, '')
    for item in old_sys_path:
        if item not in sys.path:
            sys.path.append(item)

    log('Getting handler %s\n' % os.getenv('WSGI_ALT_VIRTUALENV_HANDLER'))
    handler = get_wsgi_handler(os.getenv('WSGI_ALT_VIRTUALENV_HANDLER'))
    log('Got handler: %r\n' % handler)
    return handler
