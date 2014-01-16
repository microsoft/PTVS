from flask import Flask
app = Flask(__name__)

# Make the WSGI interface available at the top level so wfastcgi can get it.
wsgi_app = app.wsgi_app


@app.route('/')
def hello():
    return "Hello World!"

if __name__ == '__main__':
    import os
    host = os.environ.get('SERVER_HOST', 'localhost')
    try:
        port = int(os.environ.get('SERVER_PORT', '5555'))
    except ValueError:
        port = 5555
    app.run(host, port)
