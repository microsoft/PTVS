"""
Routes and views for the bottle application.
"""

from datetime import datetime

from bottle import route, view, request, redirect, template, HTTPError

from models import PollNotFound
from models.factory import create_repository
from settings import REPOSITORY_NAME, REPOSITORY_SETTINGS

repository = create_repository(REPOSITORY_NAME, REPOSITORY_SETTINGS)

@route('/')
@route('/home')
@view('index')
def home():
    """Renders the home page, with a list of all polls."""
    return dict(
        title='Polls',
        year=datetime.now().year,
        polls=repository.get_polls(),
    )

@route('/contact')
@view('contact')
def contact():
    """Renders the contact page."""
    return dict(
        title='Contact',
        year=datetime.now().year
    )

@route('/about')
@view('about')
def about():
    """Renders the about page."""
    return dict(
        title='About',
        year=datetime.now().year,
        repository_name=repository.name,
    )

@route('/seed', method='POST')
def seed():
    """Seeds the database with sample polls."""
    repository.add_sample_polls()
    return redirect('/')

@route('/results/<key>')
@view('results')
def results(key):
    """Renders the results page."""
    try:
        poll = repository.get_poll(key)
        poll.calculate_stats()
        return dict(
            title='Results',
            year=datetime.now().year,
            poll=poll,
        )
    except PollNotFound:
        raise HTTPError(404, "Poll does not exist.")

@route('/poll/<key>', method='GET')
def details_get(key):
    """Renders the poll details page."""
    try:
        return details(key, '')
    except PollNotFound:
        raise HTTPError(404, "Poll does not exist.")

@route('/poll/<key>', method='POST')
def details_post(key):
    """Handles voting. Validates input and updates the repository."""
    try:
        choice_key = request.forms.get('choice', '')
        if choice_key:
            repository.increment_vote(key, choice_key)
            return redirect('/results/{0}'.format(key))
        else:
            return details(key, 'Please make a selection.')
    except PollNotFound:
        raise HTTPError(404, "Poll does not exist.")

def details(key, msg):
    """Renders the poll details page with the specified error message."""
    return template(
        'details',
        title='Poll',
        year=datetime.now().year,
        poll=repository.get_poll(key),
        error_message=msg,
    )
