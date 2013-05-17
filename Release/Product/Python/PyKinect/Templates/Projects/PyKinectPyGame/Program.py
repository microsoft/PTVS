# If this is your first PyKinect project, you may need to install PyKinect into
# your Python 2.7 library through the Tools->Python Tools->Samples->PyKinect
# menu.
from pykinect import nui

import pygame
from pygame.color import THECOLORS
from pygame.locals import *

KINECTEVENT = pygame.USEREVENT

def post_frame(frame):
    """Get skeleton events from the Kinect device and post them into the PyGame event queue"""
    try:
        pygame.event.post(pygame.event.Event(KINECTEVENT, skeletons = frame.SkeletonData))
    except:
        # event queue full
        pass

if __name__ == '__main__':
    WINSIZE = 640, 480
    pygame.init()

    # Initialize PyGame
    screen = pygame.display.set_mode(WINSIZE,0,16)    
    pygame.display.set_caption('Python Kinect Game')
    screen.fill(THECOLORS["black"])

    with nui.Runtime() as kinect:
        kinect.skeleton_engine.enabled = True
        kinect.skeleton_frame_ready += post_frame

        # Main game loop    
        while True:
            e = pygame.event.wait()
            
            if e.type == pygame.QUIT:
                break
            elif e.type == KINECTEVENT:
                # process e.skeletons here
                pass
