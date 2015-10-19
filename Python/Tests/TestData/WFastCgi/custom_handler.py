def handler(params, start_response):
    start_response('200', [('Content-type', 'text/html'), ('Custom-Header', '42')])
    return [b'<html>', b'<body>', b'hello world', b'</body>', b'</html>']
    
def callable_handler():
    return handler


def error_handler(params, start_response):
    start_response('404', [('Content-type', 'text/html')])
    return [b'<html>', b'<body>', b"Sorry folks, we're closed for two weeks to clean and repair America's favorite family fun park", b'</body>', b'</html>']