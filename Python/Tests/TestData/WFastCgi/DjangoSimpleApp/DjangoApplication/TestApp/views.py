# Create your views here.
# Create your views here.
from django.http import HttpResponse, HttpRequest
def home(request):
    assert isinstance(request, HttpRequest)
    result = '''path: {0}
path_info: {1}
GET: {2}
POST: {3}'''.format(request.path, request.path_info, request.GET.urlencode(), request.POST.urlencode())
    return HttpResponse(result)