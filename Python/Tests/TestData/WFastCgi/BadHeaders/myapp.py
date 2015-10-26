import sys
import traceback

def test_1(environment, start_response):
    try:
        start_response('200', [])
        raise Exception
        yield b'200 OK'
    except:
        # We get to start again as long as no data has been yielded
        start_response('500', [], sys.exc_info())
        yield b'500 Error'

def test_2(environment, start_response):
    try:
        start_response('200', [])
        yield b'200 OK'
        raise Exception
    except:
        # We don't get to start again because of the yield.
        # This will result in a generic 500 error
        start_response('500', [], sys.exc_info())

def test_3(environment, start_response):
    start_response('200', [])
    try:
        start_response('200', [])
        yield b'Should have thrown when setting headers again'
    except:
        start_response('500', [], sys.exc_info())
        yield traceback.format_exc()

def test_4(environment, start_response):
    yield b'Should throw because we have not set headers'

def handler(environment, start_response):
    return globals()[environment['PATH_INFO'].strip('/')](environment, start_response)
