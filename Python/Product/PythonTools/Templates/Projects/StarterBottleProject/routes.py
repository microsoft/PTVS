from bottle import route, view
from datetime import datetime

@route('/')
@route('/home')
@view('index')
def home():
    return dict(
        year = datetime.now().year
    )

@route('/contact')
@view('contact')
def contact():
    return dict(
        title = 'Contact',
        message = 'Your contact page.',
        year = datetime.now().year
    )

@route('/about')
@view('about')
def about():
    return dict(
        title = 'About',
        message = 'Your application description page.',
        year = datetime.now().year
    )
