import sys
from django.http import HttpResponse, HttpRequest

if sys.version_info >= (3,):
    def encode(x):
        return x.encode('ascii')
else:
    def encode(x):
        return x

def home(request):
    assert isinstance(request, HttpRequest)
    result = '''path: {0}
path_info: {1}
GET: {2}
POST: {3}'''.format(request.path, request.path_info, request.GET.urlencode(), request.POST.urlencode())
    return HttpResponse(encode(result))