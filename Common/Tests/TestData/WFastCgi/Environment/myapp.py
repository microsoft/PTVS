def handler(environment, start_response):
    start_response('200', '')
    return [
      'QUERY_STRING: ' + environment['QUERY_STRING'] + '\n',
      'PATH_INFO: ' + environment['PATH_INFO'] + '\n',
      'SCRIPT_NAME: ' + environment['SCRIPT_NAME'] + '\n',
    ]
