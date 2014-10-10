try:
    from urllib.request import Request, urlopen
except ImportError:
    from urllib2 import Request, urlopen
import json 
from collections import namedtuple

{4}_score = namedtuple('{4}_score', [{5}])

def get_{4}_score({0}):
    data =  {{
            'Id': 'score00001',
            'Instance': {{
                'FeatureVector': {{
{1}
                }},
                'GlobalParameters': {{ }}
            }}
        }}
    
    body = str.encode(json.dumps(data))

    url = '{2}'
    api_key = '{3}'
    headers = {{'Content-Type':'application/json', 'Authorization':('Bearer '+ api_key)}}
    req = Request(url, body, headers) 
    response = urlopen(req)
    result = response.read()
    return {4}_score(*json.loads(result))
