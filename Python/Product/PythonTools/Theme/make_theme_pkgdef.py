#! python3.4 -b

THEME_NAME = 'PythonTools'
THEME_PKGDEF = 'Microsoft.PythonTools.theme.v{}.0.pkgdef'

import struct
import xml.etree.ElementTree
from pathlib import WindowsPath
from uuid import UUID

CT_VALUES = {
    'CT_RAW': 1,
    'CT_SYSCOLOR': 3,
}

for VS_VERSION in (15, 16, 17):
    print('Writing to {}'.format(THEME_PKGDEF.format(VS_VERSION)))
    output = open(THEME_PKGDEF.format(VS_VERSION), 'w', encoding='ascii')

    for src in WindowsPath(__file__).parent.glob("*.vstheme"):
        print(src)
        tree = xml.etree.ElementTree.parse(str(src))
        root = tree.getroot()
        
        data = []
        
        theme = root.find('Theme')
        theme_guid = UUID(theme.get('GUID'))
        try:
            theme_ver = int(theme.get('MinVSVersion'))
        except (ValueError, TypeError):
            pass
        else:
            if theme_ver > VS_VERSION:
                print('SKIPPED {}'.format(src))
                continue

        categories = root.findall('Theme/Category')
        data.append(struct.pack('I', len(categories)))
        
        for category in categories:
            category_guid = UUID(category.get('GUID'))
            data.append(category_guid.bytes_le)
            
            colors = category.findall('Color')
            data.append(struct.pack('I', len(colors)))
            for color in colors:
                namebytes = color.get('Name').encode('utf-8')
                data.append(struct.pack('I', len(namebytes)))
                data.append(namebytes)
                
                bg_e = color.find('./Background')
                if bg_e is None:
                    data.append(struct.pack('b', 0))
                else:
                    data.append(struct.pack('b', CT_VALUES[bg_e.get('Type')]))
                    data.append(struct.pack('I', int(bg_e.get('Source'), 16)))
                
                fg_e = color.find('./Foreground')
                if fg_e is None:
                    data.append(struct.pack('b', 0))
                else:
                    data.append(struct.pack('b', CT_VALUES[fg_e.get('Type')]))
                    data.append(struct.pack('I', int(fg_e.get('Source'), 16)))

        payload = b''.join(data)
        hdr = struct.pack('II', len(payload) + 8, 11)
        
        fulldata = hdr + payload
        
        print('[$RootKey$\\Themes\\{{{}}}\\{}]'.format(theme_guid, THEME_NAME), file=output)
        print('"Data"=hex:' + ','.join('{:02x}'.format(i) for i in fulldata), file=output)
        print(file=output)
    
    print()