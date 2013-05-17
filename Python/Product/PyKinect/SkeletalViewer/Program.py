import pykinect
from pykinect import nui


pykinect.nui.NuiInitialize(
                            nui.NUI_INITIALIZE_FLAG_USES_DEPTH_AND_PLAYER_INDEX | 
                            nui.NUI_INITIALIZE_FLAG_USES_SKELETON | 
                            nui.NUI_INITIALIZE_FLAG_USES_COLOR)

from  pykinect.nui import imagecamera


pykinect.nui.NuiShutdown()


print('goodbye')