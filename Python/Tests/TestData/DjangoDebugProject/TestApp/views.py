# Create your views here.
from django.http import HttpResponse

def home(request):
    return HttpResponse('<html><body>Hello world!</body></html>')