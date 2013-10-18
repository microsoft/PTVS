def handler(environment, start_response):
    start_response('200', '')
    return [
      b'QUERY_STRING: ' + environment['QUERY_STRING'] + b'\n',
      b'PATH_INFO: ' + environment['PATH_INFO'] + b'\n',
      b'SCRIPT_NAME: ' + environment['SCRIPT_NAME'] + b'\n',
    ]
