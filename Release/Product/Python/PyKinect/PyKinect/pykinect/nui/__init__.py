 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # Available under the Microsoft PyKinect 1.0 Alpha license.  See LICENSE.txt
 # for more information.
 #
 # ###########################################################################/

import ctypes
import os
import thread

# basic initialization, Python specific infrastructure
_nuidll_path = os.path.join(os.environ['WINDIR'], 'System32', 'Kinect10.dll')
_NUIDLL = ctypes.WinDLL(_nuidll_path)

class KinectError(WindowsError):
    """Represents an error from a Kinect sensor"""
    pass

from pykinect.nui.structs import (ImageDigitalZoom, ImageFrame, ImageResolution, 
                                  ImageType, ImageViewArea, JointId, 
                                  JointTrackingState, PlanarImage, SkeletonData, 
                                  SkeletonFrame, SkeletonFrameQuality, 
                                  SkeletonQuality, SkeletonTrackingState, 
                                  TransformSmoothParameters, Vector, _Enumeration)

from _interop import (_CreateEvent, _CloseHandle, _WaitForSingleObject, 
                      _WaitForMultipleObjects, _WAIT_OBJECT_0, _INFINITE, 
                      _SysFreeString, _NuiInstance, _NuiCreateSensorByIndex, 
                      _NuiGetSensorCount)


_NUI_IMAGE_PLAYER_INDEX_SHIFT      =    3
_NUI_IMAGE_PLAYER_INDEX_MASK       =    ((1 << _NUI_IMAGE_PLAYER_INDEX_SHIFT)-1)
_NUI_IMAGE_DEPTH_MAXIMUM           =    ((4000 << _NUI_IMAGE_PLAYER_INDEX_SHIFT) | _NUI_IMAGE_PLAYER_INDEX_MASK)
_NUI_IMAGE_DEPTH_MINIMUM           =    (800 << _NUI_IMAGE_PLAYER_INDEX_SHIFT)
_NUI_IMAGE_DEPTH_NO_VALUE          =    0

_NUI_CAMERA_DEPTH_NOMINAL_FOCAL_LENGTH_IN_PIXELS         = 285.63   # Based on 320x240 pixel size.
_NUI_CAMERA_DEPTH_NOMINAL_INVERSE_FOCAL_LENGTH_IN_PIXELS = 3.501e-3 # (1/NUI_CAMERA_DEPTH_NOMINAL_FOCAL_LENGTH_IN_PIXELS)
_NUI_CAMERA_DEPTH_NOMINAL_DIAGONAL_FOV                   = 70.0
_NUI_CAMERA_DEPTH_NOMINAL_HORIZONTAL_FOV                 = 58.5
_NUI_CAMERA_DEPTH_NOMINAL_VERTICAL_FOV                   = 45.6

_NUI_CAMERA_COLOR_NOMINAL_FOCAL_LENGTH_IN_PIXELS         = 531.15   # Based on 640x480 pixel size.
_NUI_CAMERA_COLOR_NOMINAL_INVERSE_FOCAL_LENGTH_IN_PIXELS = 1.83e-3  # (1/NUI_CAMERA_COLOR_NOMINAL_FOCAL_LENGTH_IN_PIXELS)
_NUI_CAMERA_COLOR_NOMINAL_DIAGONAL_FOV                   = 73.9
_NUI_CAMERA_COLOR_NOMINAL_HORIZONTAL_FOV                 = 62.0
_NUI_CAMERA_COLOR_NOMINAL_VERTICAL_FOV                   = 48.6

# the max # of NUI output frames you can hold w/o releasing
_NUI_IMAGE_STREAM_FRAME_LIMIT_MAXIMUM = 4

# return S_FALSE instead of E_NUI_FRAME_NO_DATA if NuiImageStreamGetNextFrame( ) doesn't have a frame ready and a timeout != INFINITE is used
_NUI_IMAGE_STREAM_FLAG_SUPPRESS_NO_FRAME_DATA  = 0x00010000

#######################################################

_NUI_SKELETON_MAX_TRACKED_COUNT = 2
_NUI_SKELETON_INVALID_TRACKING_ID = 0

# Assuming a pixel resolution of 320x240
# x_meters = (x_pixelcoord - 160) * NUI_CAMERA_DEPTH_IMAGE_TO_SKELETON_MULTIPLIER_320x240 * z_meters;
# y_meters = (y_pixelcoord - 120) * NUI_CAMERA_DEPTH_IMAGE_TO_SKELETON_MULTIPLIER_320x240 * z_meters;
_NUI_CAMERA_DEPTH_IMAGE_TO_SKELETON_MULTIPLIER_320x240 = _NUI_CAMERA_DEPTH_NOMINAL_INVERSE_FOCAL_LENGTH_IN_PIXELS
 
# Assuming a pixel resolution of 320x240
# x_pixelcoord = (x_meters) * NUI_CAMERA_SKELETON_TO_DEPTH_IMAGE_MULTIPLIER_320x240 / z_meters + 160;
# y_pixelcoord = (y_meters) * NUI_CAMERA_SKELETON_TO_DEPTH_IMAGE_MULTIPLIER_320x240 / z_meters + 120;
_NUI_CAMERA_SKELETON_TO_DEPTH_IMAGE_MULTIPLIER_320x240 = _NUI_CAMERA_DEPTH_NOMINAL_FOCAL_LENGTH_IN_PIXELS

_FLT_EPSILON     = 1.192092896e-07

class RuntimeOptions(object):
    """Specifies the runtime options for a Kinect sensor. """
    UseColor = 2
    UseDepth = 0x20
    UseDepthAndPlayerIndex = 1
    UseSkeletalTracking = 8


class Device(object):
    """Represents a system's Kinect sensors."""
    _device_inst = None

    def __new__(cls):
        if Device._device_inst is None:
            Device._device_inst = object.__new__(Device)
        return Device._device_inst
        
    @property
    def count(self):
        """The number of active Kinect sensors that are attached to the system."""
        return _NuiGetSensorCount()


class Runtime(object):
    """Represents a Kinect sensor."""

    def __init__(self, 
                 nui_init_flags = RuntimeOptions.UseColor | RuntimeOptions.UseDepth | RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseSkeletalTracking,                 
                 index = 0):
        """Creates a new runtime.  By default initialized to the 1st installed kinect device and tracking all events"""
        self._nui = self._skeleton_event = self._image_event = self._depth_event = None
        self._nui = _NuiCreateSensorByIndex(index)
        try:            
            self._nui.NuiInitialize(nui_init_flags)
        except:
            self._nui.NuiShutdown()
            import traceback
            
            raise KinectError('Unable to create Kinect runtime '+ traceback.format_exc()) 

        self.depth_frame_ready = _event()
        self.skeleton_frame_ready = _event()
        self.video_frame_ready = _event()

        self._skeleton_event = _CreateEvent(None, True, False, None)
        self._image_event = _CreateEvent(None, True, False, None)
        self._depth_event = _CreateEvent(None, True, False, None)
        
        self.camera = Camera(self)
        self.skeleton_engine = SkeletonEngine(self)
        self.depth_stream = ImageStream(self)
        self.video_stream = ImageStream(self)
        
        thread.start_new_thread(self._event_thread, ())

    def close(self):
        """closes the current runtime"""
        if self._nui is not None:
            self._nui.NuiShutdown()
            self._nui = None
        
        if self._skeleton_event is not None:
            _CloseHandle(self._skeleton_event)
            self._skeleton_event = None
        
        if self._image_event is not None:
            _CloseHandle(self._image_event)
            self._image_event = None
        
        if self._depth_event is not None:
            _CloseHandle(self._depth_event)
            self._depth_event = None

    def _check_closed(self):
        if self._nui is None:
            raise KinectError('Device closed')

    def __del__(self):
        self.close()
    
    def __enter__(self):
        return self

    def __exit__(self, *args):
        self.close()

    @property
    def instance_index(self):
        self._check_closed()

        """Gets the index for this instance of Runtime."""
        return self._nui.InstanceIndex()


    def _event_thread(self):
        handles = (ctypes.c_voidp * 3)()
        handles[0] = self._skeleton_event
        handles[1] = self._depth_event
        handles[2] = self._image_event
        while 1:    
            wait = _WaitForMultipleObjects(3, handles, False, _INFINITE)
            if wait == 0:
                # skeleton data                
                try:
                    frame = self._nui.NuiSkeletonGetNextFrame(0)
                except KinectError:
                    continue
        
                for curSkeleton in frame.SkeletonData:
                    if curSkeleton.eTrackingState != SkeletonTrackingState.NOT_TRACKED:
                        self.skeleton_frame_ready.fire(frame)
                        break
            elif wait == 1:
                # depth event
                depth_frame = self._nui.NuiImageStreamGetNextFrame(self.depth_stream._stream, 0)
                self.depth_frame_ready.fire(depth_frame)                
                self._nui.NuiImageStreamReleaseFrame(self.depth_stream._stream, depth_frame)
            elif wait == 2:
                # image event
                depth_frame = self._nui.NuiImageStreamGetNextFrame(self.video_stream._stream, 0)
                self.video_frame_ready.fire(depth_frame)     
                self._nui.NuiImageStreamReleaseFrame(self.video_stream._stream, depth_frame)
                pass
            else:
                # wait failed in some form (abandoned, timeout, or failed), this ends our loop
                # when we close our events.
                break


class ImageStreamType(object):
    """Specifies an image stream type. """
    Depth = 0
    Video = 1
    Invalid = -1


class ImageStream(object):
    """Represents an image stream."""

    def __init__(self, runtime):
        self.runtime = runtime
        self.resolution = ImageResolution.Invalid
        self.height = self.width = 0
        self.stream_type = ImageStreamType.Invalid
        self._stream = None

    def open(self, image_stream_type = 0, frame_limit = 2, resolution = ImageResolution.Resolution320x240, image_type = ImageType.Color):
        if image_stream_type == ImageStreamType.Depth:
            event_handle = self.runtime._depth_event
        elif image_stream_type == ImageStreamType.Video:
            event_handle = self.runtime._image_event
        else:
            raise ValueError("Unexpected image stream type: %r" % (image_stream_type, ))

        if resolution == ImageResolution.Resolution1280x1024:
            self.width = 1280
            self.height = 1024
        elif resolution == ImageResolution.Resolution640x480:
            self.width = 640
            self.height = 480
        elif resolution == ImageResolution.Resolution320x240:
            self.width = 320
            self.height = 240
        elif resolution == ImageResolution.Resolution80x60:
            self.width = 80
            self.height = 60
        else:
            raise ValueError("Unexpected resolution: %r" % (resolution, ))

        self._stream = self.runtime._nui.NuiImageStreamOpen(image_type, resolution, 0, frame_limit, event_handle)
        self.stream_type = image_stream_type
        self.resolution = resolution        
        self.type = image_type

    def get_next_frame(self, milliseconds_to_wait = 0):
        # TODO: Allow user to provide a NUI_IMAGE_FRAME ?
        return self.runtime._nui.NuiImageStreamGetNextFrame(self._stream, milliseconds_to_wait)
    
    @staticmethod
    def get_valid_resolutions(image_type):
        if image_type == ImageType.Color:
            return (ImageResolution.Resolution1280x1024, ImageResolution.Resolution640x480)
        elif image_type == ImageType.Depth:
            return (ImageResolution.Resolution640x480, )
        elif image_type == ImageType.DepthAndPlayerIndex:
            return (ImageResolution.Resolution320x240, )
        elif image_type == ImageType.ColorYuv:
            return (ImageResolution.Resolution640x480, )
        elif image_type == ImageType.ColorYuvRaw:
            return (ImageResolution.Resolution640x480, )
        else:
            raise KinectError("Unknown image_type: %r" % (image_type, ))


class SkeletonEngine(object):    
    """Represents the skeleton tracking engine. """

    def __init__(self, runtime):
        self.runtime = runtime
        self._enabled = False

    @property
    def enabled(self):
        return self._enabled

    @enabled.setter
    def enabled(self, value):
        if value:
            self.runtime._nui.NuiSkeletonTrackingEnable(self.runtime._skeleton_event)
            self._enabled = True
        else:
            self.runtime._nui.NuiSkeletonTrackingDisable(self.runtime._skeleton_event)
            self._enabled = False

    def get_next_frame(self, timeout = -1):
        res = self.runtime._nui.NuiSkeletonGetNextFrame(timeout)
        assert isinstance(res, SkeletonFrame)
        return res

    @staticmethod
    def depth_image_to_skeleton(fDepthX, fDepthY, usDepthValue):
        """returns Vector4"""

        ##
        ##  Depth is in meters in skeleton space.
        ##  The depth image pixel format has depth in millimeters shifted left by 3.
        ##
    
        fSkeletonZ = (usDepthValue >> 3) / 1000.0
    
        ##
        ## Center of depth sensor is at (0,0,0) in skeleton space, and
        ## and (160,120) in depth image coordinates.  Note that positive Y
        ## is up in skeleton space and down in image coordinates.
        ##
    
        fSkeletonX = (fDepthX - 0.5) * (_NUI_CAMERA_DEPTH_IMAGE_TO_SKELETON_MULTIPLIER_320x240 * fSkeletonZ) * 320.0
        fSkeletonY = (0.5 - fDepthY) * (_NUI_CAMERA_DEPTH_IMAGE_TO_SKELETON_MULTIPLIER_320x240 * fSkeletonZ) * 240.0
    
        ##
        ## Return the result as a vector.
        ##
        
        v4 = Vector()    
        v4.x = fSkeletonX
        v4.y = fSkeletonY
        v4.z = fSkeletonZ
        v4.w = 1.0
        return v4

    @staticmethod
    def skeleton_to_depth_image(vPoint, scaleX = 1, scaleY = 1):
        """Given a Vector4 returns X and Y coordinates fo display on the screen.  Returns a tuple depthX, depthY"""

        if vPoint.z > _FLT_EPSILON: 
           ##
           ## Center of depth sensor is at (0,0,0) in skeleton space, and
           ## and (160,120) in depth image coordinates.  Note that positive Y
           ## is up in skeleton space and down in image coordinates.
           ##
       
           pfDepthX = 0.5 + vPoint.x * ( _NUI_CAMERA_SKELETON_TO_DEPTH_IMAGE_MULTIPLIER_320x240 / vPoint.z ) / 320.0
           pfDepthY = 0.5 - vPoint.y * ( _NUI_CAMERA_SKELETON_TO_DEPTH_IMAGE_MULTIPLIER_320x240 / vPoint.z ) / 240.0
       
           return pfDepthX * scaleX, pfDepthY * scaleY

        return 0.0, 0.0


class Camera(object):
    """Represents a Kinect sensor's camera."""

    def __init__(self, runtime):
        self.runtime = runtime
        self.elevation_angle

    ElevationMaximum = 27
    ElevationMinimum = -27

    @property
    def elevation_angle(self):
        """Gets or sets the camera elevation angle. """
        return self.runtime._nui.NuiCameraElevationGetAngle()

    @elevation_angle.setter
    def elevation_angle(self, degrees):
        """Gets or sets the camera elevation angle. """

        self.runtime._nui.NuiCameraElevationSetAngle(degrees)

    @property
    def unique_device_name(self):
        """Gets the camera's unique device name. """
        return self.runtime._nui.GetUniqueDeviceName()

    def get_color_pixel_coordinates_from_depth_pixel(self, color_resolution, view_area, depth_x, depth_y, depth_value):
        """Returns the pixel coordinates in color image space that correspond to the specified pixel coordinates in depth image space. 

color_resolution: An ImageResolution value specifying the color image resolution.
view_area: An ImageViewArea structure containing the pan and zoom settings. If you provide this argument, you should pass in the view area from the image frame against which you are registering pixels, rather than manually instantiating and populating the structure. This helps ensure that your settings are valid.
depth_x: The x coordinate in depth image space.
depth_y: The y coordinate in depth image space.
depth_value The depth value in depth image space.

Returns: color_x, color_y - the x and y coordinate in the color image space
"""
        return self.runtime._nui.NuiImageGetColorPixelCoordinatesFromDepthPixel(color_resolution, view_area, depth_x, depth_y, depth_value)
        

def TransformSmoothParameters(vPoint):
    """returns depthX (float), depthY (float), depthValue (int)"""

    if vPoint.vector.z > _FLT_EPSILON:

       # Center of depth sensor is at (0,0,0) in skeleton space, and
       # and (160,120) in depth image coordinates.  Note that positive Y
       # is up in skeleton space and down in image coordinates.
       #
       
       pfDepthX = 0.5 + vPoint.vector.x * _NUI_CAMERA_SKELETON_TO_DEPTH_IMAGE_MULTIPLIER_320x240 / ( vPoint.vector.z * 320.0 )
       pfDepthY = 0.5 - vPoint.vector.y * _NUI_CAMERA_SKELETON_TO_DEPTH_IMAGE_MULTIPLIER_320x240 / ( vPoint.vector.z * 240.0 )
       
       #
       #  Depth is in meters in skeleton space.
       #  The depth image pixel format has depth in millimeters shifted left by 3.
       #
       
       pusDepthValue = int(vPoint.vector.z * 1000) << 3
       return pfDepthX, pfDepthY, pusDepthValue

    return 0.0, 0.0, 0


class _event(object):
    """class used for adding/removing/invoking a set of listener functions"""
    __slots__ = ['handlers']
        
    def __init__(self):
        self.handlers = []
    
    def __iadd__(self, other):
        self.handlers.append(other)
        return self
        
    def __isub__(self, other):
        self.handlers.remove(other)
        return self

    def fire(self, *args):
        for handler in self.handlers:
            handler(*args)


