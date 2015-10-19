def handler(environment, start_response):
    start_response('200', '')
    return [b'myapp2 world!']
