from __future__ import with_statement
import sys
from datetime import datetime
from threading import Thread

try:
    from queue import Queue
except ImportError:
    from Queue import Queue
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

# These workers will be network-bound, so CPU count doesn't really matter
WORKER_THREADS = 16
QUEUE = None

def process_changelog_entry(thread_data):
    while QUEUE:
        item = QUEUE.get(block=True)
        try:
            i, change_data = item[:2]
            package, version, timestamp, change = change_data[:4]
            if is_new_release(change):
                log("New release for %s %s", package, version)
                thread_data.append((i, package, version, None))
            elif is_description_change(change):
                log("New description for %s %s", package, version)
                summary = None
                for _ in range(5):
                    try:
                        summary = server.release_data(package, version).get('summary', '')
                        break
                    except Exception:
                        pass
                thread_data.append((i, package, version, summary))
        finally:
            QUEUE.task_done()


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
    
    threads, thread_data = [], []
    any_items = False
    for change_data in enumerate(server.changelog_since_serial(last_serial)):
        if not any_items:
            any_items = True
            QUEUE = Queue()
        if len(threads) < WORKER_THREADS:
            t = Thread(target=process_changelog_entry, args=(thread_data,))
            t.daemon = True
            t.start()
            threads.append(t)

        QUEUE.put_nowait(change_data)
    
    if QUEUE:
        QUEUE.join()
        QUEUE = None

    for entry in sorted(thread_data):
        _, package, version, summary = entry
        if summary is None:
            summary = all_packages.get(package, ('', ''))[1]
        all_packages[package] = version, summary

    if classifier:
        data['Preferred'] = [package for package, version in server.browse([classifier])]
    elif 'Preferred' in data:
        del data['Preferred']
    
    with open(cache, 'wb') as f:
        data = dump(data, f, protocol=2)

    sys.exit(0)