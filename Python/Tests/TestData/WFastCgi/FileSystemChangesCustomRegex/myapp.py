result = file('myapp.txt', 'rb').readlines()

def handler(environment, start_response):
    start_response('200', '')
    return result
