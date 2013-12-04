'''A script that allows Flask servers to be started from the command line.

This is used by PTVS to launch the server with a port configured through Visual
Studio, either by the user or randomly selected.
'''

__author__ = 'Microsoft Corporation <ptvshelp@microsoft.com>'
__version__ = '1.0.0.0'

from argparse import ArgumentParser
from app import app

parser = ArgumentParser('runserver.py for Flask apps')
parser.add_argument('--host', help='The host for the server to listen on', type=str)
parser.add_argument('--port', help='The port for the server to listen on', type=int, default=5000)
# Note that PTVS will never pass the --debug flag, since Flask's debug mode
# prevents the PTVS debugger from working.
parser.add_argument('--debug', help='Enable debugging mode and auto-reload', default=False, action='store_true')

opts = parser.parse_args()

app.run(opts.host, opts.port, opts.debug)
