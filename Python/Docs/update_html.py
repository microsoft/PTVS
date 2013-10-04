#! python3
import sys
import os
import pickle
import re
import urllib

try:
    import markdown2
except ImportError:
    print("markdown2 is required. Run `pip install markdown2`")
    sys.exit(1)

# Relative path from location of build.root file
DOC_ROOT = r'Python\Docs'

# Base URL of source file location
URL_BASE = 'http://pytools.codeplex.com/'
SOURCE_URL_BASE = URL_BASE + 'SourceControl/latest#'
WIKI_URL_BASE = URL_BASE + 'wikipage?title='
ISSUE_URL_BASE = URL_BASE + 'workitem/'

EXTRAS = {
    'code-friendly': None,
    'header-ids': None,
    'pyshell': None,
    'fenced-code-blocks': None,
    'wiki-tables': None,
}
LINK_PATTERNS = None

HEADER = '''<style type="text/css">
    .hll { background-color: #ffffcc }
    .c { color: #008000 } 
    .err { border: 1px solid #FF0000 } 
    .cm, .c1, .cs { color: #008000 } 
    .ge { font-style: italic } 
    .gh, .gp, .gs, .gu { font-weight: bold } 
    .k, .cp, .kc, .kd, .kn, .kp, .kr, .ow { color: #0000ff } 
    .kt { color: #2b91af } 
    .s { color: #a31515 } 
    .nc { color: #2b91af } 
    .sb, .sc, .sd, .s2, .se, .sh, .si, .sx, .sr, .s1, .ss { color: #a31515 }

    pre { border: 2px solid #a0a0a0; padding: 1em; }
    table { border-spacing: 0; border-collapse: collapse; }
    td { padding: 0.2em 0.5em; border: 1px solid #a0a0a0; }
</style>
'''

FOOTER = '''
'''

def get_build_root(start):
    while start and not os.path.exists(os.path.join(start, 'build.root')):
        start = os.path.split(start)[0]
    return start

def get_file_maps(source_root):
    try:
        with open('maps.cache', 'rb') as f:
            result = pickle.load(f)
        return result
    except:
        pass

    file_map = {}
    type_map = {}
    for dirname, dirnames, filenames in os.walk(source_root):
        for filename in filenames:
            fullpath = os.path.join(source_root, dirname, filename)
            urlpath = fullpath[len(source_root):].lstrip('\\').replace('\\', '/')

            nameonly = os.path.split(fullpath)[1]
            if nameonly in file_map:
                file_map[nameonly] = None
            else:
                file_map[nameonly] = urlpath

            if not filename.upper().endswith(('.PY', '.CS')):
                continue

            try:
                with open(fullpath, 'r', encoding='utf-8-sig') as f:
                    content = f.read()
            except UnicodeDecodeError:
                #print('Cannot read {}'.format(filename))
                continue
            
            nsname = None
            if filename.upper().endswith('.PY'):
                nsname = os.path.splitext(filename)[0]

            for match in re.finditer(r'(namespace|class|struct|enum|interface) ([\w\.]+)', content):
                kind, name = match.groups()
                if kind == 'namespace':
                    nsname = name
                elif nsname:
                    type_map[nsname + '.' + name] = urlpath
    
    try:
        with open('maps.cache', 'wb') as f:
            pickle.dump((file_map, type_map), f, pickle.HIGHEST_PROTOCOL)
    except:
        pass
    return file_map, type_map

def replace_source_links(matchobj):
    typename = matchobj.group(1)
    
    url = NAMESPACE_FILE_MAP.get(typename, None)
    if url:
        return '[{}]({} "{}")'.format(typename.rpartition('.')[-1], SOURCE_URL_BASE + url, typename)
    else:
        return '`{}`'.format(typename)

def replace_file_links(matchobj):
    filename = matchobj.group(1)

    url = FILENAME_MAP.get(filename) or filename.replace('\\', '/')
    return '[{}]({} "{}")'.format(os.path.split(filename)[-1], SOURCE_URL_BASE + url, filename)

def replace_wiki_links(matchobj):
    path = matchobj.group(3)
    title = matchobj.group(2) or matchobj.group(3)

    return '[{}]({})'.format(title, WIKI_URL_BASE + urllib.parse.quote(path))

def replace_issue_links(matchobj):
    number = matchobj.group(3)
    title = matchobj.group(2) or matchobj.group(3)

    return '[{}]({})'.format(title, ISSUE_URL_BASE + number)

LINK_PATTERNS = [
    (r'(?<!`)\[src\:([^\]]+)\]', replace_source_links),
    (r'(?<!`)\[file\:([^\]]+)\]', replace_file_links),
    (r'(?<!`)\[wiki\:("([^"]+)"\s*)?([^\]]+)\]', replace_wiki_links),
    (r'(?<!`)\[issue\:("([^"]+)"\s*)?([0-9]+)\]', replace_issue_links),
]

HTML_PATTERNS = [
    (r'\<p\>--\(\<\/p\>', '<div style="overflow: auto">'),
    (r'\<p\>--\)\<\/p\>', '</div>')
]

markdown = markdown2.Markdown(
    tab_width = 4,
    extras = EXTRAS,
)

# Patch out old-style code blocks. We only support fenced (```...```),
# and the indented ones cause formatting conflicts.
markdown._do_code_blocks = lambda t: t

if __name__ == '__main__':
    BUILD_ROOT = get_build_root(os.getcwd())
    print('Creating/loading file maps')
    FILENAME_MAP, NAMESPACE_FILE_MAP = get_file_maps(BUILD_ROOT)
    
    sources = os.path.join(BUILD_ROOT, DOC_ROOT)
    print('Reading from ' + sources)
    
    for dirname, _, filenames in os.walk(sources):
        for filename in (f for f in filenames if f.upper().endswith('.MD')):
            print('Converting {}'.format(filename))
            with open(os.path.join(dirname, filename), 'r', encoding='utf-8-sig') as src:
                text = src.read()
            
            # Do our own link replacements, rather than markdown2's
            for pattern, repl in LINK_PATTERNS:
                text = re.sub(pattern, repl, text)
            
            html = markdown.convert(text)
            
            # Do any post-conversion replacements
            for pattern, repl in HTML_PATTERNS:
                html = re.sub(pattern, repl, html)
            
            with open(os.path.join(dirname, filename[:-3] + '.html'), 'w', encoding='utf-8') as dest:
                dest.write(HEADER)
                dest.write(html)
                dest.write(FOOTER)
