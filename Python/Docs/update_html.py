#! python3
import io
import os
import pickle
import re
import sys
import urllib
import urllib.request

try:
    import markdown2
except ImportError:
    print("markdown2 is required. Run `pip install markdown2`")
    sys.exit(1)

try:
    import PIL.Image
except ImportError:
    print("Pillow is required. Run `pip install --use-wheel Pillow`")
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

HEADER = ''''''

FOOTER = '''
'''

def get_build_root(start):
    while start and not os.path.exists(os.path.join(start, 'build.root')):
        start = os.path.split(start)[0]
    return start

class LinkMapper:
    def __init__(self, source_root):
        self.source_root = source_root

        try:
            with open('maps.cache', 'rb') as f:
                self.file_map, self.type_map = pickle.load(f)
            return
        except:
            pass

        print('Creating file maps')
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
        self.file_map = file_map
        self.type_map = type_map
    
    def replace_source_links(self, matchobj):
        typename = matchobj.group(1)
        
        url = self.type_map.get(typename, None)
        if url:
            return '[{}]({} "{}")'.format(typename.rpartition('.')[-1], SOURCE_URL_BASE + url, typename)
        else:
            return '`{}`'.format(typename)

    def replace_file_links(self, matchobj):
        filename = matchobj.group(1)

        url = self.file_map.get(filename) or filename.replace('\\', '/')
        return '[{}]({} "{}")'.format(os.path.split(filename)[-1], SOURCE_URL_BASE + url, filename)

    def replace_wiki_links(self, matchobj):
        path = matchobj.group(3)
        title = matchobj.group(2) or matchobj.group(3)

        p1, p2, p3 = path.partition('#')
        return '[{}]({})'.format(title, WIKI_URL_BASE + urllib.parse.quote(p1) + p2 + p3)

    def replace_issue_links(self, matchobj):
        number = matchobj.group(3)
        title = matchobj.group(2) or matchobj.group(3)

        return '[{}]({})'.format(title, ISSUE_URL_BASE + number)

    def replace_video_links(self, matchobj):
        video_id = matchobj.group(3)
        title = matchobj.group(2) or ("YouTube video " + matchobj.group(3))

        print('Generating video thumbnail for {}'.format(video_id))

        with urllib.request.urlopen("http://img.youtube.com/vi/{}/hqdefault.jpg".format(video_id)) as f:
            thumbnail_data = f.read()
        thumbnail = PIL.Image.open(io.BytesIO(thumbnail_data)).convert('RGBA')
        overlay = PIL.Image.open(os.path.join(self.source_root, DOC_ROOT, 'Images/Play.png'))

        thumbnail_filename = 'VideoThumbnails/{}.png'.format(video_id)
        PIL.Image.alpha_composite(thumbnail, overlay).save(thumbnail_filename)

        return """
<p style="text-align: center">
<a href="http://www.youtube.com/watch?v={0}" target="_blank" style="display: inline-block">
<img src="{1}" alt="{2}" border="0" width="480" height="360" />
</a></p>
               """.format(video_id, thumbnail_filename, title)

    @property
    def patterns(self):
        return [
            (r'(?<!`)\[src\:([^\]]+)\]', self.replace_source_links),
            (r'(?<!`)\[file\:([^\]]+)\]', self.replace_file_links),
            (r'(?<!`)\[wiki\:("([^"]+)"\s*)?([^\]]+)\]', self.replace_wiki_links),
            (r'(?<!`)\[issue\:("([^"]+)"\s*)?([0-9]+)\]', self.replace_issue_links),
            (r'(?<!`)\[video\:("([^"]+)"\s*)?([^\]]+)\]', self.replace_video_links),
        ]


SPAN_STYLES = {
    'c': 'color: #008000', 
    'cp': 'color: #0000ff',
    'err': 'border: 1px solid #FF0000', 
    'g': 'font-weight: bold',
    'ge': 'font-style: italic', 
    'hll': 'background-color: #ffffcc',
    'k': 'color: #0000ff',
    'kt': 'color: #2b91af', 
    'nc': 'color: #2b91af', 
    'ow': 'color: #0000ff', 
    's': 'color: #a31515', 
    'menu': 'font: menu; color: MenuText; background-color: ThreeDFace; padding: 0em 0.5em 0em 0.5em;'
}

def replace_span_style(matchobj):
    key = matchobj.group(1)
    style = None
    while key and not style:
        style = SPAN_STYLES.get(key)
        key = key[:-1]
    
    return '<span style="{}">'.format(style) if style else '<span>'

HTML_PATTERNS = [
    # Provide --( --) syntax for divs that keep floats and text together
    (r'\<p\>--\(\<\/p\>', '<div style="overflow: auto">'),
    (r'\<p\>--\)\<\/p\>', '</div>'),
    
    # Provide >> syntax for a div floated to the right
    (r' *<blockquote>\s+<blockquote>', '<div style="float: right; padding: 0.5em">'),
    (r' *</blockquote>\s+</blockquote>', '</div>'),
    
    # Remove p tags when the entire contents is an img, an empty anchor, or an empty p tag
    (r'\<p\>(?P<img>\<img.+?\/\s*\>)\<\/p\>', r'\g<img>'),
    (r'\<p\>(?P<tag>\<[ap][^>]+?\/\s*\>)\<\/p\>', r'\g<tag>'),
    
    # Add inline styles for some elements
    (r'\<pre\>', '<pre style="border: 2px solid #a0a0a0; padding: 1em;">'),
    (r'\<table\>', '<table style="border-spacing: 0; border-collapse: collapse;">'),
    (r'\<td\>', '<td style="padding: 0.2em 0.5em; border: 1px solid #a0a0a0;">'),
    (r'\<kbd\>', '<span style="font-family: sans-serif; font-size: 0.8em; padding: 0.1em 0.6em; border: 1px solid #cccccc; background-color:#f7f7f7; color:#333333; box-shadow: 0 1px 0px rgba(0, 0, 0, 0.2), 0 0 0 2px #ffffff inset; border-radius: 3px; display: inline-block; margin: 0 0.1em; text-shadow: 0 1px 0 #ffffff; line-height: 1.4; white-space:nowrap">'),
    (r'\</kbd\>', '</span>'),
    (r'\<span class="(.+?)"\>', replace_span_style),
    
    # Remove unnecessary spans
    (r'\<span\>([^<]+)\<\/span\>', r'\g<1>'),
]

markdown = markdown2.Markdown(
    tab_width = 4,
    extras = EXTRAS,
)

# Patch out old-style code blocks. We only support fenced (```...```),
# and the indented ones cause formatting conflicts.
markdown._do_code_blocks = lambda t: t

def main(start_dir):
    BUILD_ROOT = get_build_root(start_dir)
    link_mapper = LinkMapper(BUILD_ROOT)
    
    sources = os.path.join(BUILD_ROOT, DOC_ROOT)
    print('Reading from ' + sources)
    
    for dirname, _, filenames in os.walk(sources):
        for filename in (f for f in filenames if f.upper().endswith('.MD')):
            print('Converting {}'.format(filename))
            cwd = os.getcwd()
            os.chdir(dirname)
            try:
                with open(filename, 'r', encoding='utf-8-sig') as src:
                    text = src.read()
            
                # Do our own link replacements, rather than markdown2's
                for pattern, repl in link_mapper.patterns:
                    text = re.sub(pattern, repl, text)
            
                html = markdown.convert(text)
            
                # Do any post-conversion replacements
                for pattern, repl in HTML_PATTERNS:
                    html = re.sub(pattern, repl, html)
            
                with open(filename[:-3] + '.html', 'w', encoding='utf-8') as dest:
                    dest.write(HEADER)
                    dest.write(html)
                    dest.write(FOOTER)
            finally:
                os.chdir(cwd)

if __name__ == '__main__':
    sys.exit(main(os.getcwd()) or 0)
