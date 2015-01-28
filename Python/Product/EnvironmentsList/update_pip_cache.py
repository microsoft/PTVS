import sys
from datetime import datetime
try:
    from cPickle import load, dump
except ImportError:
    from pickle import load, dump
try:
    from xmlrpc.client import ServerProxy
except ImportError:
    from xmlrpclib import Server as ServerProxy

if __debug__:
    def log(t, *p):
        print(t % p)
else:
    def log(*p):
        pass

def is_new_release(change):
    return change == 'new release'

def is_description_change(change):
    return change.startswith('update ') and 'description' in change

def is_relevant_change(change):
    return is_new_release(change) or is_description_change(change)

if __name__ == '__main__':
    cache = sys.argv[1]
    try:
        classifier = sys.argv[2]
    except IndexError:
        classifier = None
    
    try:
        with open(cache, 'rb') as f:
            data = load(f)
    except:
        log("Cache not found")
        data = {}
    
    data['LastUpdate'] = str(datetime.now())
    
    server = ServerProxy("http://pypi.python.org/pypi")
    
    all_packages = data.get('Packages')
    last_serial = data.get('LastSerial', 0)
    log("Last change serial in cache = %s", last_serial)
    
    if not last_serial or not all_packages:
        log("Getting all packages")
        data['LastSerial'] = last_serial = server.changelog_last_serial()
        log("Last change serial = %s", last_serial)
        data['Packages'] = all_packages = dict((
            p.get('name'),
            (p.get('version'), p.get('summary'))
        ) for p in server.search({}))
        log("Got %s packages", len(all_packages))
    
    for change_data in server.changelog_since_serial(last_serial):
        package, version, timestamp, change = change_data[:4]
        if is_new_release(change):
            log("New release for %s %s", package, version)
            all_packages[package] = (version, all_packages.get(package, ('', ''))[1])
        elif is_description_change(change):
            log("New description for %s %s", package, version)
            all_packages[package] = (version, (server.release_data(package, version) or {}).get('summary', ''))
    
    if classifier:
        data['Preferred'] = [package for package, version in server.browse([classifier])]
    elif 'Preferred' in data:
        del data['Preferred']
    
    with open(cache, 'wb') as f:
        data = dump(data, f, protocol=2)
