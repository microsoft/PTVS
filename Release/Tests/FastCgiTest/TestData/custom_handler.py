def handler(params, start_response):
    log_file = 'C:\\users\\dinov\\foo.txt'
    if log_file:
        with file(log_file, 'a+') as f:
            f.write('request started\r\n')
    start_response('200', [('Content-type', 'text/html'), ('Custom-Header', '42')])
    return ['<html>', '<body>', 'hello world', '</body>', '</html>']
    
def callable_handler():
    return handler


def error_handler(params, start_response):
    log_file = 'C:\\users\\dinov\\foo.txt'
    if log_file:
        with file(log_file, 'a+') as f:
            f.write('request started\r\n')
    start_response('404', [('Content-type', 'text/html')])
    return ['<html>', '<body>', "Sorry folks, we're closed for two weeks to clean and repair America's favorite family fun park", '</body>', '</html>']