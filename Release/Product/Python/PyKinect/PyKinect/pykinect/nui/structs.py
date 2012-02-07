 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # Available under the Microsoft PyKinect 1.0 Alpha license.  See LICENSE.txt
 # for more information.
 #
 # ###########################################################################/

"""defines the core data structures used for communicating w/ the Kinect APIs"""

import ctypes

NUI_SKELETON_COUNT = 6

class _EnumerationType(type(ctypes.c_int)):
    """metaclass for an enumeration like type for ctypes"""

    def __new__(metacls, name, bases, dict):
        cls = type(ctypes.c_int).__new__(metacls, name, bases, dict)
        for key, value in cls.__dict__.items():                        
            if key.startswith('_') and key.endswith('_'): continue
            
            setattr(cls, key, cls(key, value))
        
        return cls


class _Enumeration(ctypes.c_int):
    """base class for enumerations"""

    __metaclass__ = _EnumerationType
    def __init__(self, name, value):
        self.name = name        
        ctypes.c_int.__init__(self, value)
      
    def __hash__(self):
        return self.value

    def __int__(self):
        return self.value
    
    def __index__(self):
        return self.value
      
    def __repr__(self):
        if hasattr(self, 'name'):
            return "<%s.%s (%r)>" % (self.__class__.__name__, self.name, self.value)

        name = '??'
        for x in type(self).__dict__:
            if x.startswith('_') and x.endswith('_'): continue

            if getattr(self, x, None).value == self.value:
                name = x
                break

        return "<%s.%s (%r)>" % (self.__class__.__name__, name, self.value)

    def __eq__(self, other):
        if type(self) is not type(other):
            return self.value == other
            
        return self.value == other.value

    def __ne__(self, other):
        if type(self) is not type(other):
            return self.value != other

        return self.value != other.value


class Vector(ctypes.Structure):
    """Represents vector data."""
    _fields_ = [('x', ctypes.c_float),
            ('y', ctypes.c_float),
            ('z', ctypes.c_float),
            ('w', ctypes.c_float)
            ]

    def __repr__(self):
        return '<x=%r, y=%r, z=%r, w=%r>' % (self.x, self.y, self.z, self.w)

class _NuiLockedRect(ctypes.Structure):
    _fields_ = [('pitch', ctypes.c_int32), 
                ('size', ctypes.c_int32),
                ('bits', ctypes.c_voidp)]

     
class _NuiSurfaceDesc(ctypes.Structure):
    _fields_ = [('width', ctypes.c_uint32),
                ('height', ctypes.c_uint32)
               ]

class PlanarImage(ctypes.c_voidp):
    """Represents a video image."""
    _BufferLen = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_int32)(3, 'BufferLen')
    _Pitch = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_int32)(4, 'Pitch')
    _LockRect = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_uint, ctypes.POINTER(_NuiLockedRect), ctypes.c_voidp, ctypes.c_uint32)(5, '_LockRect')
    _GetLevelDesc = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_uint32, ctypes.POINTER(_NuiSurfaceDesc))(6, '_GetLevelDesc')
    _UnlockRect = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_uint32)(7, '_UnlockRect')

    @property
    def width(self):
        desc = _NuiSurfaceDesc()
        PlanarImage._GetLevelDesc(self, 0, ctypes.byref(desc))
        return desc.width
    
    @property
    def height(self):
        desc = _NuiSurfaceDesc()
        PlanarImage._GetLevelDesc(self, 0, ctypes.byref(desc))
        return desc.height.value
        
    @property
    def bytes_per_pixel(self):
        return self.pitch / self.width

    @property
    def bits(self):
        buffer = (ctypes.c_byte * self.buffer_length)()
        self.copy_bits(buffer)
        return buffer

    def copy_bits(self, dest):
        """copies the bits of the image to the provided destination address"""
        desc = _NuiSurfaceDesc()
        
        PlanarImage._GetLevelDesc(self, 0, ctypes.byref(desc))

        rect = _NuiLockedRect()
        PlanarImage._LockRect(self, 0, ctypes.byref(rect), None, 0)
        ctypes.memmove(dest, rect.bits, desc.height * rect.pitch)
        PlanarImage._UnlockRect(self, 0)

    @property
    def buffer_length(self):
        return self.width * self.height * self.bytes_per_pixel

    @property
    def pitch(self):
        rect = _NuiLockedRect()
        PlanarImage._LockRect(self, 0, ctypes.byref(rect), None, 0)
        res = rect.pitch
        PlanarImage._UnlockRect(self, 0)
        return res


class ImageType(_Enumeration):
    """Specifies an image type. """
    DepthAndPlayerIndex = 0                 # USHORT
    Color = 1                               # RGB32 data
    ColorYuv = 2                            # YUY2 stream from camera h/w, but converted to RGB32 before user getting it.
    ColorYuvRaw = 3                         # YUY2 stream from camera h/w.
    Depth = 4                               # USHORT


class ImageResolution(_Enumeration):
    """Specifies image resolution."""
    Invalid = -1
    Resolution80x60 = 0 
    Resolution320x240 = 1
    Resolution640x480 = 2
    Resolution1280x1024 = 3                      # for hires color only


class ImageDigitalZoom(_Enumeration):
    """Specifies the zoom factor."""

    Zoom1x = 0  # A zoom factor of 1.0.
    Zoom2x = 1  # A zoom factor of 2.0.


class ImageViewArea(ctypes.Structure):
    """Specifies the image view area. """
    _fields_ = [('Zoom', ctypes.c_int),     # An ImageDigitalZoom value that specifies the zoom factor. 
                ('CenterX', ctypes.c_long), # The horizontal offset from center, for panning. 
                ('CenterY', ctypes.c_long)  # The vertical offset from center, for panning. 
               ]


class ImageFrame(ctypes.Structure):
    _fields_ = [('timestamp', ctypes.c_longlong),               # The timestamp (in milliseconds) of the most recent frame. The clock starts when you call Initialize.
               ('frame_number', ctypes.c_uint32),              # Returns the frame number
               ('type', ImageType),                         # An ImageType value that specifies the image type.
               ('resolution', ImageResolution),                # An ImageResolution value that specifies the image resolution.
               ('image', PlanarImage),                         # A PlanarImage object that represents the image.
               ('flags', ctypes.c_uint32),                     # flags, not used
               ('view_area', ImageViewArea),                   # An ImageViewArea value that specifies the view area.
              ]


class JointId(_Enumeration):
    """Specifies the various skeleton joints. """
    HipCenter = 0
    Spine = 1
    ShoulderCenter = 2
    Head = 3
    ShoulderLeft = 4
    ElbowLeft = 5
    WristLeft = 6
    HandLeft = 7
    ShoulderRight = 8
    ElbowRight = 9 
    WristRight = 10
    HandRight = 11
    HipLeft = 12
    KneeLeft = 13
    AnkleLeft = 14
    FootLeft = 15 
    HipRight = 16
    KneeRight = 17
    AnkleRight = 18
    FootRight = 19
    Count = 20


class JointTrackingState(_Enumeration):
    """Specifies the joint tracking state. """
    NOT_TRACKED = 0
    INFERRED = 1
    TRACKED = 2


class SkeletonTrackingState(_Enumeration):
    """Specifies a skeleton's tracking state."""
    NOT_TRACKED = 0
    POSITION_ONLY = 1
    TRACKED = 2

class SkeletonFrameQuality(_Enumeration):
    """Specifies skeleton frame quality. """
    CameraMotion = 0x01
    ExtrapolatedFloor 	 = 0x02
    UpperBodySkeleton = 0x04


class SkeletonQuality(_Enumeration):
    """Specifies how much of the skeleton is visible. """
    ClippedRight  = 0x00000001
    ClippedLeft   = 0x00000002
    ClippedTop    = 0x00000004
    ClippedBottom = 0x00000008


NUI_SKELETON_POSITION_COUNT = JointId.Count.value


class SkeletonData(ctypes.Structure):
    """Contains data that characterizes a skeleton."""
    _fields_ = [('eTrackingState', SkeletonTrackingState),
                ('dwTrackingID', ctypes.c_uint32),
                ('dwEnrollmentIndex', ctypes.c_uint32),
                ('dwUserIndex', ctypes.c_uint32),
                ('Position', Vector),
                ('SkeletonPositions', ctypes.ARRAY(Vector, NUI_SKELETON_POSITION_COUNT)),
                ('eSkeletonPositionTrackingState', ctypes.ARRAY(JointTrackingState, NUI_SKELETON_POSITION_COUNT)),
                ('Quality', SkeletonQuality),
                ]

    def __repr__(self):
        return '<Tracking: {0}, ID: {1}, Position: {2}>'.format(self.eTrackingState, self.dwTrackingID, self.Position)


class SkeletonFrame(ctypes.Structure):
    _pack_ = 16
    _fields_ = [('liTimeStamp', ctypes.c_longlong),
                ('dwFrameNumber', ctypes.c_uint32),
                ('Quality', SkeletonFrameQuality),
                ('vFloorClipPlane', Vector),
                ('vNormalToGravity', Vector),
                ('SkeletonData', ctypes.ARRAY(SkeletonData, NUI_SKELETON_COUNT)),
                ]


class TransformSmoothParameters(ctypes.Structure):
    """Contains transform smoothing parameters. """
    _fields_ = [('fSmoothing', ctypes.c_float),
                ('fCorrection', ctypes.c_float),
                ('fPrediction', ctypes.c_float),
                ('fJitterRadius', ctypes.c_float),
                ('fMaxDeviationRadius', ctypes.c_float)
                ]

