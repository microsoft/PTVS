# Create your views here.
from django.http import HttpResponse

def home(request):
    return HttpResponse('<html><body>Hello World!</body></html>')

def config(request):
    import os    
    return HttpResponse('<html><body>' + os.environ['CONFIG_VALUE'] + '</body></html>')


def large_response(request):
    res = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghjklmnopqrstuvwxyz0123456789' * 3000
    return HttpResponse(res)
