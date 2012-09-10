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
from ctypes import Array
from pykinect.nui import _NUIDLL

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

    def __init__(self, x = 0.0, y = 0.0, z = 0.0, w = 0.0):
        self.x = x
        self.y = y
        self.z = z
        self.w = w

    def __eq__(self, other):
        return (self.x == other.x and
               self.y == other.y and
               self.z == other.z and
               self.w == other.w)
 
    def __ne__(self, other):
        return not self.__eq__(other)

    def __repr__(self):
        return '<x=%r, y=%r, z=%r, w=%r>' % (self.x, self.y, self.z, self.w)


class Matrix4(Array):
    """4x4 matrix.  Can be accessed using matrix[0,0] ... matrix[3,3] or can be accessed using
       matrix.M11 ... matrix.M44 for similarity to .NET and the C data structures.  matrix[0,1] is
       the same as matrix.M12.
    
        Used to provide bone rotation information.   
    """

    _length_ = 16
    _type_ = ctypes.c_float

    def __getitem__(self, index):
        return Array.__getitem__(self, index[1] + index[0] * 4)

    def __setitem__(self, index, value):
        return Array.__setitem__(self, index[1] + index[0] * 4, value)

    def get_M11(self): return Array.__getitem__(0)
    def set_M11(self, value): Array.__setitem__(0, value)
    M11 = property(get_M11, set_M11)

    def get_M12(self): return Array.__getitem__(1)
    def set_M12(self, value): Array.__setitem__(1, value)
    M12 = property(get_M12, set_M12)

    def get_M13(self): return Array.__getitem__(2)
    def set_M13(self, value): Array.__setitem__(2, value)
    M13 = property(get_M13, set_M13)

    def get_M14(self): return Array.__getitem__(3)
    def set_M14(self, value): Array.__setitem__(3, value)
    M14 = property(get_M14, set_M14)

    def get_M21(self): return Array.__getitem__(4)
    def set_M21(self, value): Array.__setitem__(4, value)
    M21 = property(get_M21, set_M21)

    def get_M22(self): return Array.__getitem__(5)
    def set_M22(self, value): Array.__setitem__(5, value)
    M22 = property(get_M22, set_M22)

    def get_M23(self): return Array.__getitem__(6)
    def set_M23(self, value): Array.__setitem__(6, value)
    M23 = property(get_M23, set_M23)

    def get_M24(self): return Array.__getitem__(7)
    def set_M24(self, value): Array.__setitem__(7, value)
    M24 = property(get_M24, set_M24)

    def get_M31(self): return Array.__getitem__(8)
    def set_M31(self, value): Array.__setitem__(8, value)
    M31 = property(get_M31, set_M31)

    def get_M32(self): return Array.__getitem__(9)
    def set_M32(self, value): Array.__setitem__(9, value)
    M32 = property(get_M32, set_M32)

    def get_M33(self): return Array.__getitem__(10)
    def set_M33(self, value): Array.__setitem__(10, value)
    M33 = property(get_M33, set_M33)

    def get_M34(self): return Array.__getitem__(11)
    def set_M34(self, value): Array.__setitem__(11, value)
    M34 = property(get_M34, set_M34)

    def get_M41(self): return Array.__getitem__(12)
    def set_M41(self, value): Array.__setitem__(12, value)
    M41 = property(get_M41, set_M41)

    def get_M42(self): return Array.__getitem__(13)
    def set_M42(self, value): Array.__setitem__(13, value)
    M42 = property(get_M42, set_M42)

    def get_M43(self): return Array.__getitem__(14)
    def set_M43(self, value): Array.__setitem__(14, value)
    M43 = property(get_M43, set_M43)

    def get_M44(self): return Array.__getitem__(15)
    def set_M44(self, value): Array.__setitem__(15, value)
    M44 = property(get_M44, set_M44)


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
    depth_and_player_index = DepthAndPlayerIndex = 0                 # USHORT
    color = Color = 1                               # RGB32 data
    color_yuv = ColorYuv = 2                            # YUY2 stream from camera h/w, but converted to RGB32 before user getting it.
    color_yuv_raw = ColorYuvRaw = 3                         # YUY2 stream from camera h/w.
    depth = Depth = 4                               # USHORT


class ImageResolution(_Enumeration):
    """Specifies image resolution."""
    invalid = Invalid = -1
    resolution_80x60 = Resolution80x60 = 0 
    resolution_320x240 = Resolution320x240 = 1
    resolution_640x480 = Resolution640x480 = 2
    resolution_1280x1024 = Resolution1280x1024 = 3                      # for hires color only


class SkeletonTracking(_Enumeration):
    suppress_no_frame_data       = 0x00000001   # Prevents NuiSkeletonGetNextFrame from returning E_NUI_FRAME_NO_DATA errors. Instead, calls to NuiSkeletonGetNextFrame block until data is available or the timeout period passes.
    title_sets_tracked_skeletons = 0x00000002   # Disables the default player selection mode and enables the title to manage which players have tracked skeletons.
    enable_seated_support        = 0x00000004   # Uses seated skeleton tracking mode. The 10 lower-body joints of each skeleton will not be tracked.
    enable_in_near_range         = 0x00000008

class ImageDigitalZoom(_Enumeration):
    """Specifies the zoom factor."""

    zoom_1x = Zoom1x = 0  # A zoom factor of 1.0.
    zoom_2x = Zoom2x = 1  # A zoom factor of 2.0.


class ImageViewArea(ctypes.Structure):
    """Specifies the image view area. """
    _fields_ = [('Zoom', ctypes.c_int),     # An ImageDigitalZoom value that specifies the zoom factor. 
                ('CenterX', ctypes.c_long), # The horizontal offset from center, for panning. 
                ('CenterY', ctypes.c_long)  # The vertical offset from center, for panning. 
               ]

    def get_zoom(self):
        return self.Zoom

    def set_zoom(self, value):
        self.Zoom = value

    zoom = property(get_zoom, set_zoom)

    def get_center_x(self):
        return self.CenterX

    def set_center_x(self, value):
        self.CenterX = value

    def get_center_y(self):
        return self.CenterY

    center_x = property(get_center_x, set_center_x)

    def set_center_y(self, value):
        self.CenterY = value

    center_y = property(get_center_y, set_center_y)


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
    hip_center = HipCenter = 0
    spine = Spine = 1
    shoulder_center = ShoulderCenter = 2
    head = Head = 3
    shoulder_left = ShoulderLeft = 4
    elbow_left = ElbowLeft = 5
    wrist_left = WristLeft = 6
    hand_left = HandLeft = 7
    shoulder_right = ShoulderRight = 8
    elbow_right = ElbowRight = 9 
    wrist_right = WristRight = 10
    hand_right = HandRight = 11
    hip_left = HipLeft = 12
    knee_left = KneeLeft = 13
    ankle_left = AnkleLeft = 14
    foot_left = FootLeft = 15 
    hip_right = HipRight = 16
    knee_right = KneeRight = 17
    ankle_right = AnkleRight = 18
    foot_right = FootRight = 19
    count = Count = 20

class SkeletonBoneRotation(ctypes.Structure):
    _fields_ = [('rotation_matrix', Matrix4), 
                ('rotation_quaternion', Vector)]

    def __repr__(self):
        return '<SkeletonBoneRotation(%r, %r)>' % (self.rotation_matrix, self.rotation_quaternion)

class SkeletonBoneOrientation(ctypes.Structure):
    _fields_ = [('end_joint', JointId), 
                ('start_joint', JointId),
                ('hierarchical_rotation', SkeletonBoneRotation),
                ('absolute_rotation', SkeletonBoneRotation),
                ]

    def __repr__(self):
        return '<SkeletonBoneOrientation(%r, %r, %r, %r)>' % (self.end_joint, self.start_joint, self.hierarchical_rotation, self.absolute_rotation)


class JointTrackingState(_Enumeration):
    """Specifies the joint tracking state. """
    not_tracked = NOT_TRACKED = 0
    inferred = INFERRED = 1
    tracked = TRACKED = 2


class SkeletonTrackingState(_Enumeration):
    """Specifies a skeleton's tracking state."""
    not_tracked = NOT_TRACKED = 0
    position_only = POSITION_ONLY = 1
    tracked = TRACKED = 2

class SkeletonFrameQuality(_Enumeration):
    """Specifies skeleton frame quality. """
    camera_motion = CameraMotion = 0x01
    extrapolated_floor = ExtrapolatedFloor 	 = 0x02
    upper_body_skeleton = UpperBodySkeleton = 0x04
    seated_support_enabled = 0x08


class SkeletonQuality(_Enumeration):
    """Specifies how much of the skeleton is visible. """
    clipped_right = ClippedRight  = 0x00000001
    clipped_left = ClippedLeft   = 0x00000002
    clipped_top = ClippedTop    = 0x00000004
    clipped_bottom = ClippedBottom = 0x00000008

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

    def get_tracking_state(self):
        return self.eTrackingState

    def set_tracking_state(self, value):
        self.eTrackingState = value

    tracking_state = property(get_tracking_state, set_tracking_state)

    def get_tracking_id(self):
        return self.dwTrackingID

    def set_tracking_id(self, value):
        self.dwTrackingID = value

    tracking_id = property(get_tracking_id, set_tracking_id)

    def get_enrollment_index(self):
        return self.dwEnrollmentIndex

    def set_enrollment_index(self, value):
        self.dwEnrollmentIndex = value

    enrollment_index = property(get_enrollment_index, set_enrollment_index)

    def get_user_index(self):
        return self.dwUserIndex

    def set_user_index(self, value):
        self.dwUserIndex = value

    user_index = property(get_user_index, set_user_index)

    def get_position(self):
        return self.Position

    def set_position(self, value):
        self.Position = value

    position = property(get_position, set_position)

    def get_skeleton_positions(self):
        return self.SkeletonPositions

    def set_skeleton_positions(self, value):
        self.SkeletonPositions = value

    skeleton_positions = property(get_skeleton_positions, set_skeleton_positions)

    def get_skeleton_position_tracking_states(self):
        return self.eSkeletonPositionTrackingState

    def set_skeleton_position_tracking_states(self, value):
        self.eSkeletonPositionTrackingState = value

    skeleton_position_tracking_states = property(get_skeleton_position_tracking_states, 
                                                set_skeleton_position_tracking_states)
        
    def get_skeleton_quality(self):
        return self.Quality

    def set_skeleton_quality(self, value):
        self.Quality = value

    skeleton_quality = property(get_skeleton_quality, set_skeleton_quality)

    def calculate_bone_orientations(self):
        """Calculate bone orientations for a skeleton.

        The function calculates hierarchical and absolute joint angles for the skeleton, which can
        be used in animating an avatar (Avateering). The HipCenter joint is the root of the hierarchy,
        and describes an absolute rotation in the right-hand camera coordinate system. All other
        joints describe rotations relative to their parent joint orientation. The angles are returned
        in the same order as the joints are defined.

        Returns a sequence of SkeletonBoneOrientation objects."""
        arr = (SkeletonBoneOrientation*JointId.Count)()
        _NuiSkeletonCalculateBoneOrientations(self, arr)
        return tuple(arr)

    def __repr__(self):
        return '<Tracking: %r, ID: %r, Position: %r>' % (self.eTrackingState, 
                                                            self.dwTrackingID, 
                                                            self.Position)
    def __eq__(self, other):
        if (self.tracking_state == other.tracking_state and
                self.tracking_id == other.tracking_id and
                self.enrollment_index == other.enrollment_index and
                self.user_index == other.user_index and
                self.position == other.position and
                self.skeleton_quality == other.skeleton_quality):

            for i in range(len(self.skeleton_positions)):
                if (self.skeleton_positions[i] != other.skeleton_positions[i] or 
                    self.skeleton_position_tracking_states[i] != other.skeleton_position_tracking_states[i]):
                    return False                 

            return True

        return False

    def __ne__(self, other):
        return not self.__eq__(other)

    def __nonzero__(self):
        return self.tracking_state != SkeletonTrackingState.not_tracked

_NuiSkeletonCalculateBoneOrientations = _NUIDLL.NuiSkeletonCalculateBoneOrientations
_NuiSkeletonCalculateBoneOrientations.argtypes = [ctypes.POINTER(SkeletonData), ctypes.POINTER(SkeletonBoneOrientation)]
_NuiSkeletonCalculateBoneOrientations.restype = ctypes.HRESULT


class SkeletonFrame(ctypes.Structure):
    _pack_ = 16
    _fields_ = [('liTimeStamp', ctypes.c_longlong),
                ('dwFrameNumber', ctypes.c_uint32),
                ('Quality', SkeletonFrameQuality),
                ('vFloorClipPlane', Vector),
                ('vNormalToGravity', Vector),
                ('SkeletonData', ctypes.ARRAY(SkeletonData, NUI_SKELETON_COUNT)),
                ]

    def get_timestamp(self):
        return self.liTimeStamp
    
    def set_timestamp(self, value):
        self.liTimeStamp = value
    
    timestamp = property(get_timestamp, set_timestamp)

    def get_frame_number(self):
        return self.dwFrameNumber
    
    def set_frame_number(self, value):
        self.dwFrameNumber = value
    
    frame_number = property(get_frame_number, set_frame_number)

    def get_quality(self):
        return self.Quality
    
    def set_quality(self, value):
        self.Quality = value
    
    quality = property(get_quality, set_quality)

    def get_floor_clip_plane(self):
        return self.vFloorClipPlane
    
    def set_floor_clip_plane(self, value):
        self.vFloorClipPlane = value
    
    floor_clip_plane = property(get_floor_clip_plane, set_floor_clip_plane)

    def get_normal_to_gravity(self):
        return self.vNormalToGravity
    
    def set_normal_to_gravity(self, value):
        self.vNormalToGravity = value
    
    normal_to_gravity = property(get_normal_to_gravity, set_normal_to_gravity)

    def get_skeleton_data(self):
        return self.SkeletonData
    
    def set_skeleton_data(self, value):
        self.SkeletonData = value
    
    skeleton_data = property(get_skeleton_data, set_skeleton_data)


class TransformSmoothParameters(ctypes.Structure):
    """Contains transform smoothing parameters. """
    _fields_ = [('fSmoothing', ctypes.c_float),
                ('fCorrection', ctypes.c_float),
                ('fPrediction', ctypes.c_float),
                ('fJitterRadius', ctypes.c_float),
                ('fMaxDeviationRadius', ctypes.c_float)
                ]

    def get_smoothing(self):
        return self.fSmoothing
    
    def set_smoothing(self, value):
        self.fSmoothing = value
    
    smoothing = property(get_smoothing, set_smoothing)

    def get_correction(self):
        return self.fCorrection
    
    def set_correction(self, value):
        self.fCorrection = value
    
    correction = property(get_correction, set_correction)

    def get_prediction(self):
        return self.fPrediction
    
    def set_prediction(self, value):
        self.fPrediction = value
    
    prediction = property(get_prediction, set_prediction)

    def get_jitter_radius(self):
        return self.fJitterRadius
    
    def set_jitter_radius(self, value):
        self.fJitterRadius = value
    
    jitter_radius = property(get_jitter_radius, set_jitter_radius)

    def get_max_deviation_radius(self):
        return self.fMaxDeviationRadius
    
    def set_max_deviation_radius(self, value):
        self.fMaxDeviationRadius = value
    
    max_deviation_radius = property(get_max_deviation_radius, set_max_deviation_radius)