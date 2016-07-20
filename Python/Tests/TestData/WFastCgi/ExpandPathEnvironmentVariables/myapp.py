import othermod
def handler(environment, start_response):
    start_response('200', '')
    return [othermod.x]
