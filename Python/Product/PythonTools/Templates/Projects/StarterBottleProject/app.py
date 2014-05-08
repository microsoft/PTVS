import bottle
import os
import sys
from routes import *

if '--debug' in sys.argv[1:] or 'SERVER_DEBUG' in os.environ:
    # Debug mode will enable more verbose output in the console window.
    # It must be set at the beginning of the script.
    bottle.debug(True)

def wsgi_app():
    # Returns the application to make available through wfastcgi. This is used
    # when the site is published to Windows Azure.
    return bottle.default_app()

if __name__ == '__main__':
    PROJECT_ROOT = os.path.abspath(os.path.dirname(__file__))
    STATIC_ROOT = os.path.join(PROJECT_ROOT, 'static').replace('\\','/')

    @bottle.route('/static/<filepath:path>')
    def server_static(filepath):
        return bottle.static_file(filepath, root=STATIC_ROOT)

    # Starts a local test server.
    host = os.environ.get('SERVER_HOST', 'localhost')
    try:
        port = int(os.environ.get('SERVER_PORT', '5555'))
    except ValueError:
        port = 5555
    bottle.run(server='wsgiref', host=host, port=port)
