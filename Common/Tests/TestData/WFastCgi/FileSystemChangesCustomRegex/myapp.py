result = file('myapp.txt').readlines()

def handler(environment, start_response):
    start_response('200', '')
    return result
