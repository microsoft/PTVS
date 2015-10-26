def handler(environment, start_response):
    start_response('200', '')
    return [
      b'QUERY_STRING: ' + environment['wsgi.query_string'] + b'\n',
      b'PATH_INFO: ' + environment['wsgi.path_info'] + b'\n',
      b'SCRIPT_NAME: ' + environment['wsgi.script_name'] + b'\n',
    ]
