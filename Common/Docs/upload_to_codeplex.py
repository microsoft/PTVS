import base64
import hashlib
import json
import os
import re
import sys
import traceback
import urllib.parse
import urllib.request
import xmlrpc.client

# Name of the file mapping local files to remote URLs. This is used to avoid
# having to upload every image each time the script is run, because the current
# API gives us no way to get a list of available images.
#
# The file should be checked 
MAP_FILENAME = 'codeplex.map'

MW_SOURCE = 'https://www.codeplex.com/site/metaweblog'

MEDIA_TYPES = {
    '.PNG': 'image/png',
    '.JPG': 'image/jpeg',
}

DOC_TYPES = {
    '.HTML': 'text/html',
}

def is_media_file(filename):
    return os.path.splitext(filename)[1].upper() in MEDIA_TYPES

def is_document(filename):
    return os.path.splitext(filename)[1].upper() in DOC_TYPES

def add_files(root, filemap=None):
    if filemap is None:
        filemap = {}
    
    for dirname, _, filenames in os.walk(root):
        for filename in filenames:
            if is_media_file(filename) or is_document(filename):
                fullpath = os.path.join(dirname, filename)
                relpath = os.path.relpath(fullpath, root)
                filemap.setdefault(relpath, None)
    return filemap

def get_posts(server, site, user, password):
    post_count = 50
    existing_posts = server.getRecentPosts(site, user, password, post_count)
    while len(existing_posts) == post_count:
        post_count += 50
        existing_posts = server.getRecentPosts(site, user, password, post_count)
    print('Found {} posts'.format(len(existing_posts)))
    return existing_posts


def remap_refs(filemap, content):
    def repl(match):
        local_name = match.group(2).replace('/', '\\')
        return match.group(1) + (filemap.get(local_name) or match.group(2)) + match.group(3)
    
    content = re.sub(r'''(\<img[^>]+src=['"])([^'"]+)(['"])''', repl, content)
    content = re.sub(r'''(\<a[^>]+href=['"])([^'"]+)(['"])''', repl, content)
    content = re.sub(r'''(\<[^>]+\bstyle=['"][^'"]*\burl\()([^)]*)(\))''', repl, content)
    
    return content

def main(root, site, user, password, dry_run):
    if not os.path.isabs(root):
        start_dir = os.path.join(os.getcwd(), root)

    if not dry_run:
        try:
            with open(os.path.join(root, MAP_FILENAME), 'r+b') as f:
                pass
        except FileNotFoundError:
            pass
        except PermissionError:
            print('The {} file is not writable. You should `tf edit` it before uploading.'.format(MAP_FILENAME))
            return 1

    filemap = add_files(root)
    
    server = xmlrpc.client.ServerProxy(MW_SOURCE)
    existing_posts = {
        p['title'] : (p['postid'], p['description'])
        for p in get_posts(server, site, user, password)
    }

    try:
        with open(os.path.join(root, MAP_FILENAME), 'r', encoding='utf-8-sig') as f:
            mapdata = json.load(f)
        
        for local_path in [k for k in filemap if is_media_file(k)]:
            filemap[local_path] = mapdata.get(local_path)
    except (FileNotFoundError, PermissionError):
        pass

    for local_path in [k for k in filemap if is_media_file(k)]:
        name, ext = os.path.splitext(os.path.basename(local_path))
        mediatype = MEDIA_TYPES[ext.upper()]
        
        with open(os.path.join(root, local_path), 'rb') as f:
            content = f.read()

        if filemap[local_path]:
            with urllib.request.urlopen(filemap[local_path]) as f:
                old_content = f.read()
            
            if old_content == content:
                print('Did not upload {} - contents are identical'.format(local_path))
                continue
        
        post_data = {
            'name': name + ext,
            'type': mediatype,
            'bits': content,
        }
        if dry_run:
            filemap[local_path] = 'http://fake/' + local_path
        else:
            result = server.newMediaObject(site, user, password, post_data)
            parsed_url = list(urllib.parse.urlparse(result['url']))
            # Uploaded files are hosted on a separate website with a different domain, and linking
            # to them directly does not work because the authentication cookie is not passed through.
            # To make it work, the URL has to be adjusted to go through codeplex.com - it will do
            # a redirect to the real URL and pass the cookie along.
            parsed_url[1] = 'www.codeplex.com'
            url = urllib.parse.urlunparse(parsed_url)
            filemap[local_path] = url
        print('Uploaded {} to {}'.format(local_path, filemap[local_path]))
    
    
    for local_path in [k for k in filemap if is_document(k)]:
        name, ext = os.path.splitext(os.path.basename(local_path))
        
        with open(os.path.join(root, local_path), 'r', encoding='utf-8-sig') as f:
            content = remap_refs(filemap, f.read())

        filemap[local_path] = content_hash = hashlib.sha256(bytes(content, 'utf-8-sig')).hexdigest()
        content += '<p style="visibility: hidden;">{}</p>'.format(content_hash)

        post_data = {
            'title': urllib.parse.unquote(name),
            'description': content,
            'flNotOnHomePage': True,
        }
        existing_post = existing_posts.get(urllib.parse.unquote(name))
        try:
            if existing_post:
                post_id, old_text = existing_post
                if content_hash not in old_text:
                    if dry_run:
                        success = True
                    else:
                        success = server.editPost(post_id, user, password, post_data, False)
                    print(('Uploaded {}' if success else 'Failed to upload {}').format(local_path))
                else:
                    print('Did not upload {} - contents are identical'.format(local_path))
            else:
                if dry_run:
                    success = True
                else:
                    success = server.newPost(site, user, password, post_data, False)
                print(('Uploaded {}' if success else 'Failed to upload {}').format(local_path))
        except:
            print('Potentially failed to upload {}: {}'.format(local_path, sys.exc_info()[1]))
            if __debug__:
                traceback.print_exc()
            print()
    
    if dry_run:
        print('** This was a dry run. No pages were updated. **')
    else:
        try:
            with open(os.path.join(root, MAP_FILENAME), 'w', encoding='utf-8-sig') as f:
                json.dump(filemap, f, indent = 4, sort_keys = True)
            print('Updated "codeplex.map" with {} remote files.'.format(len(filemap)))
        except PermissionError:
            pass
