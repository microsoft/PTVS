import bottle
import os
import sys
from bottle import route, template, redirect

if '--debug' in sys.argv[1:] or 'SERVER_DEBUG' in os.environ:
    # Debug mode will enable more verbose output in the console window.
    # It must be set at the beginning of the script.
    bottle.debug(True)


@route('/')
def hello():
    redirect('/hello/world')

@route('/hello/<name>')
def example(name):
    return template('<b>Hello {{name}}</b>!', name=name)



def wsgi_app():
    # Returns the application to make available through wfastcgi. This is used
    # when the site is published to Microsoft Azure.
    return bottle.default_app()

if __name__ == '__main__':
    # Starts a local test server.
    host = os.environ.get('SERVER_HOST', 'localhost')
    try:
        port = int(os.environ.get('SERVER_PORT', '5555'))
    except ValueError:
        port = 5555
    bottle.run(server='wsgiref', host=host, port=port)
