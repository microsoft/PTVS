result = open('myapp.data', 'rb').readlines()

def handler(environment, start_response):
    start_response('200', '')
    return result
