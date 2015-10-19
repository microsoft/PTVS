class handler(object):
    def __call__(self, environment, start_response):
        start_response('200', '')
        return [b'hello world!']
