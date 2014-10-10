import {1}

@route('/{0}')
@view('{0}_dashboard')
def {0}_dashboad():
    # TODO: Get the values to pass to the scoring algorithm
    score = {1}.get_{0}_score(
{2}
    )
    return dict(
        title='Score',
        message='Results of scoring {0}',
        year=datetime.now().year,
        score=score
    )
