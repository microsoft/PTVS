import base64
import re
import xml.etree.ElementTree as ET
from pathlib import Path

NS = {"n": "http://www.w3.org/1999/xhtml"}
ET.register_namespace("", NS['n'])

BUILD_ROOT = Path(__file__).resolve().parent
while not (BUILD_ROOT / 'build.root').is_file():
    brp = BUILD_ROOT.parent
    if brp == BUILD_ROOT:
        raise RuntimeError("cannot find root of repository")
    BUILD_ROOT = brp

SOURCE_CSS = BUILD_ROOT / 'Python' / 'Product' / 'PythonTools' / 'ReadmeStyle.css'
HTML_FILES = [
    BUILD_ROOT / 'Python' / 'Product' / 'IronPython' / 'NoIronPython.htmlsrc',
    BUILD_ROOT / 'Python' / 'Product' / 'PythonTools' / 'NoInterpreters.htmlsrc',
    BUILD_ROOT / 'Python' / 'Templates' / 'ReadmeFiles' / 'StarterDjangoProject' / 'readme.htmlsrc',
]

def strip_comment(s):
    r = []
    in_comment = False
    for b in re.split(r'(/\*|\*/)', s):
        if in_comment:
            if b == '*/':
                in_comment = False
        elif b == '/*':
            in_comment = True
        else:
            r.append(b)
    return ''.join(b)

def process(path, style):
    xml = ET.parse(path)
    e = xml.getroot().find("n:head/n:style[@type='text/css']", NS)
    if e is not None:
        e.text="$$STYLE$$"
    else:
        print(f'WARNING: {path} has no style element')

    for e in xml.getroot().findall(".//n:img", NS):
        with open(path.parent / e.get('src'), 'rb') as f:
            img = base64.b64encode(f.read()).decode()
        e.set('src', 'data:image/png;base64,' + img)

    xml_str = ET.tostringlist(xml.getroot(), encoding='unicode')
    with open(path.with_suffix('.html'), 'w', encoding='utf-8') as f:
        f.write(''.join(style if p == '$$STYLE$$' else p for p in xml_str))

if __name__ == '__main__':
    with open(SOURCE_CSS, 'r', encoding='utf-8-sig') as f:
        css = strip_comment(f.read()).strip()

    min_css = re.sub(r'\s+', ' ', css)

    for html_file in HTML_FILES:
        process(html_file, min_css)
        print(f'- {html_file}')
