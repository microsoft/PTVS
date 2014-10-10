import {1}
from bottle import get, post, request

@get('/{0}_form')
@view('{0}_form')
def my_service_form():
    return dict(
        title='{3}',
        message='Input a score for {3}',
        year=datetime.now().year,
    )

@post('/{0}_form')
@view('{0}_dashboard')
def {0}_dashboad():
    score = {1}.get_{0}_score(
{2}
    )
    return dict(
        title='Score',
        message='Results of scoring {0}',
        year=datetime.now().year,
        score=score
    )
