 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # Available under the Microsoft PyKinect 1.0 Alpha license.  See LICENSE.txt
 # for more information.
 #
 # ###########################################################################/

"""Contains low-level implementation details which aren't consumed directly by users of the Kinect API"""

import ctypes
from pykinect.nui import KinectError, _NUIDLL
from pykinect.nui.structs import (ImageFrame, ImageResolution, ImageType, 
                                  ImageViewArea,  SkeletonFrame, 
                                  TransformSmoothParameters, _Enumeration)

_SEVERITY_ERROR = 1

class _KinectHRESULT(ctypes._SimpleCData):
    """performs error checking and returns a custom error message for kinect HRESULTs"""
    _type_ = "l"
    @staticmethod
    def _check_retval_(error):
        if error < 0:
            err = KinectError(error, 22)
            error = error + 0xffffffff
            error_msg = _KINECT_ERRORS.get(error, '')
            if error_msg is not None:
                err.strerror = err.message = error_msg
        
            raise err

def _HRESULT_FROM_WIN32(error):
    return 0x80070000 | error

def _MAKE_HRESULT(sev, fac, code):
    return (sev << 31) | (fac << 16) | code


class _PropsIndex(_Enumeration):
    INDEX_UNIQUE_DEVICE_NAME = 0
    INDEX_LAST  = 1                     # don't use!


class _PropType(_Enumeration):
    UNKNOWN = 0   # don't use
    UINT = 1      # no need to return anything smaller than an int
    FLOAT = 2
    BSTR = 3      # returns new BSTR. Use SysFreeString( BSTR ) when you're done
    BLOB = 4


##############################################################################
##
## Define NUI error codes derived from win32 errors
##

_E_NUI_DEVICE_NOT_CONNECTED  = _HRESULT_FROM_WIN32(1167)
_E_NUI_DEVICE_NOT_READY      = _HRESULT_FROM_WIN32(21)
_E_NUI_ALREADY_INITIALIZED   = _HRESULT_FROM_WIN32(1247)
_E_NUI_NO_MORE_ITEMS         = _HRESULT_FROM_WIN32(259)

##
## Define NUI specific error codes
##

_FACILITY_NUI = 0x301
_E_NUI_FRAME_NO_DATA                     = _MAKE_HRESULT(_SEVERITY_ERROR, _FACILITY_NUI, 1)
_E_NUI_STREAM_NOT_ENABLED                = _MAKE_HRESULT(_SEVERITY_ERROR, _FACILITY_NUI, 2)
_E_NUI_IMAGE_STREAM_IN_USE               = _MAKE_HRESULT(_SEVERITY_ERROR, _FACILITY_NUI, 3)
_E_NUI_FRAME_LIMIT_EXCEEDED              = _MAKE_HRESULT(_SEVERITY_ERROR, _FACILITY_NUI, 4)
_E_NUI_FEATURE_NOT_INITIALIZED           = _MAKE_HRESULT(_SEVERITY_ERROR, _FACILITY_NUI, 5)
_E_NUI_DATABASE_NOT_FOUND                = _MAKE_HRESULT(_SEVERITY_ERROR, _FACILITY_NUI, 13)
_E_NUI_DATABASE_VERSION_MISMATCH         = _MAKE_HRESULT(_SEVERITY_ERROR, _FACILITY_NUI, 14)

_KINECT_ERRORS = {
    _E_NUI_DEVICE_NOT_CONNECTED : 'Device not connected',
    _E_NUI_DEVICE_NOT_READY : 'Device not ready',
    _E_NUI_ALREADY_INITIALIZED : 'Device already initialized',
    _E_NUI_NO_MORE_ITEMS : 'No more items',
    _E_NUI_FRAME_NO_DATA : 'Frame has no data',
    _E_NUI_STREAM_NOT_ENABLED : 'Stream is not enabled',
    _E_NUI_IMAGE_STREAM_IN_USE : 'Image stream is already in use',
    _E_NUI_FRAME_LIMIT_EXCEEDED : 'Frame limit exceeded',
    _E_NUI_FEATURE_NOT_INITIALIZED : 'Feature not initialized',
    _E_NUI_DATABASE_NOT_FOUND : 'Database not found',
    _E_NUI_DATABASE_VERSION_MISMATCH : 'Database version mismatch',
}

_kernel32 = ctypes.WinDLL('kernel32')
_CreateEvent = _kernel32.CreateEventW
_CreateEvent.argtypes = [ctypes.c_voidp, ctypes.c_bool, ctypes.c_bool, ctypes.c_wchar_p]
_CreateEvent.restype = ctypes.c_voidp

_CloseHandle = _kernel32.CloseHandle
_CloseHandle.argtypes = [ctypes.c_voidp]
_CloseHandle.restype = ctypes.c_bool

_WaitForSingleObject = _kernel32.WaitForSingleObject 
_WaitForSingleObject.argtypes = [ctypes.c_voidp, ctypes.c_uint32]
_WaitForSingleObject.restype = ctypes.c_uint32

_WaitForMultipleObjects = _kernel32.WaitForMultipleObjects 
_WaitForMultipleObjects.argtypes = [ctypes.c_uint32, ctypes.POINTER(ctypes.c_voidp), ctypes.c_bool, ctypes.c_uint32]
_WaitForMultipleObjects.restype = ctypes.c_uint32

_WAIT_OBJECT_0 = 0
_INFINITE = 0xffffffff

_oleaut32 = ctypes.WinDLL('oleaut32')
_SysFreeString = _oleaut32.SysFreeString
_SysFreeString.argtypes = [ctypes.c_voidp]
_SysFreeString.restype = ctypes.HRESULT

class _NuiInstance(ctypes.c_voidp):
    """this interface duplicates exactly the public DLL NUI**** methods that
       work on just device #0. If you want to work with multiple devices,
       use these methods off the INuiInstance, after getting a INuiInstance * from
       the multiple-device methods below"""

    # vtable
    _NuiInitialize = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_uint32)(3, 'NuiInitialize')
    _NuiShutdown = ctypes.WINFUNCTYPE(None)(4, 'NuiShutdown')
    _NuiSetFrameAndEvent = ctypes.WINFUNCTYPE(_KinectHRESULT, ctypes.c_voidp, ctypes.c_uint32)(5, 'NuiSetFrameEndEvent')
    _NuiImageStreamOpen = ctypes.WINFUNCTYPE(_KinectHRESULT, ImageType, ImageResolution, ctypes.c_uint32, ctypes.c_uint32, ctypes.c_voidp, ctypes.POINTER(ctypes.c_voidp))(6, 'NuiImageStreamOpen')
    _NuiImageStreamSetImageFrameFlags = 7
    _NuiImageStreamGetImageFrameFlags = 8
    _NuiImageStreamGetNextFrame = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_voidp, ctypes.c_uint32, ctypes.POINTER(ImageFrame))(9, 'NuiImageStreamGetNextFrame')
    _NuiImageStreamReleaseFrame = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_voidp, ctypes.POINTER(ImageFrame))(10, 'NuiImageStreamReleaseFrame')
    _NuiImageGetColorPixelCoordinatesFromDepthPixel = ctypes.WINFUNCTYPE(ctypes.HRESULT, ImageResolution, ctypes.POINTER(ImageViewArea), ctypes.c_long, ctypes.c_long, ctypes.c_uint16, ctypes.POINTER(ctypes.c_long), ctypes.POINTER(ctypes.c_long))(11, 'NuiImageGetColorPixelCoordinatesFromDepthPixel')
    _NuiImageGetColorPixelCoordinatesFromDepthPixelAtResolution = 12
    _NuiImageGetColorPixelCoordinateFrameFromDepthPixelFrameAtResolution = 13
    _NuiCameraElevationSetAngle = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_long)(14, 'NuiCameraElevationSetAngle')
    _NuiCameraElevationGetAngle = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.POINTER(ctypes.c_long))(15, 'NuiCameraElevationGetAngle')
    _NuiSkeletonTrackingEnable = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_voidp, ctypes.c_uint32)(16, 'NuiSkeletonTrackingEnable')
    _NuiSkeletonTrackingDisable = ctypes.WINFUNCTYPE(ctypes.HRESULT)(17, 'NuiSkeletonTrackingDisable')
    _NuiSkeletonSetTrackedSkeletons = 18
    _NuiSkeletonGetNextFrame = ctypes.WINFUNCTYPE(_KinectHRESULT, ctypes.c_uint32, ctypes.POINTER(SkeletonFrame))(19, 'NuiSkeletonGetNextFrame')
    _NuiTransformSmooth = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.POINTER(SkeletonFrame), ctypes.POINTER(TransformSmoothParameters))(20, 'NuiTransformSmooth')
    _NuiGetAudioSource = 21
    _InstanceIndex = ctypes.WINFUNCTYPE(ctypes.c_int)(22, 'InstanceIndex')
    _NuiDeviceConnectionId = 23
    _NuiUniqueId = 24
    _NuiAudioArrayId = 25
    _NuiStatus = 26
    _NuiInitializationFlags = 27

    def InstanceIndex(self):
        """which instance # was it created with, in MSR_NuiCreateInstanceByIndex( )/etc?"""
        print self.value
        return _NuiInstance._InstanceIndex(self)

    def NuiInitialize(self, dwFlags = 0):
        _NuiInstance._NuiInitialize(self, dwFlags)

    def NuiShutdown(self):
        return _NuiInstance._NuiShutdown(self)
        
    def NuiImageStreamOpen(self, eImageType, eResolution, dwImageFrameFlags_NotUsed, dwFrameLimit, hNextFrameEvent = 0):
        res = ctypes.c_voidp()
        _NuiInstance._NuiImageStreamOpen(self, eImageType, eResolution, dwImageFrameFlags_NotUsed, dwFrameLimit, hNextFrameEvent, ctypes.byref(res))
        return res

    def NuiImageStreamGetNextFrame(self, hStream, dwMillisecondsToWait):
        res = ImageFrame()
        _NuiInstance._NuiImageStreamGetNextFrame(self, hStream, dwMillisecondsToWait, ctypes.byref(res))
        return res

    def NuiImageStreamReleaseFrame(self, hStream, pImageFrame):
        _NuiInstance._NuiImageStreamReleaseFrame(self, hStream, pImageFrame)

    def NuiImageGetColorPixelCoordinatesFromDepthPixel(self, eColorResolution, pcViewArea, lDepthX, lDepthY, usDepthValue):
        x, y = ctypes.c_long(), ctypes.c_long()
        _NuiInstance._NuiImageGetColorPixelCoordinatesFromDepthPixel(eColorResolution, pcViewArea, lDepthX, lDepthY, usDepthValue, byref(x), byref(y))
        return x.value, y.value

    def NuiCameraElevationSetAngle(self, lAngleDegrees):
        while 1:
            try:
                _NuiInstance._NuiCameraElevationSetAngle(self, lAngleDegrees)
                return
            except:
                pass

    def NuiCameraElevationGetAngle(self):
        res = ctypes.c_long()
        _NuiInstance._NuiCameraElevationGetAngle(self, ctypes.byref(res))
        return res.value

    def NuiSkeletonTrackingEnable(self, hNextFrameEvent = 0, dwFlags = 0):
        _NuiInstance._NuiSkeletonTrackingEnable(self, hNextFrameEvent, dwFlags)

    def NuiSkeletonTrackingDisable(self):
        _NuiInstance._NuiSkeletonTrackingDisable()

    def NuiSkeletonGetNextFrame(self, dwMillisecondsToWait):
        frame = SkeletonFrame()
        _NuiInstance._NuiSkeletonGetNextFrame(self, dwMillisecondsToWait, ctypes.byref(frame))
        return frame

    def NuiTransformSmooth(self, pSkeletonFrame, pSmoothingParams):
        _NuiInstance._NuiTransformSmooth(self, pSkeletonFrame, pSmoothingParams)

    def GetUniqueDeviceName(self):        
        mem = ctypes.c_voidp()
        # Size is currently not used, and when we get the unique device name we need to free the memory.

        _NuiInstance._MSR_NuiGetPropsBlob(self, _PropsIndex.INDEX_UNIQUE_DEVICE_NAME, ctypes.byref(mem), None)
        res = ctypes.cast(mem, ctypes.c_wchar_p).value
        _SysFreeString(mem)
        return res

    def MSR_NuiGetPropsType(index):
        return _NuiInstance._MSR_NuiGetPropsType(index)


##***********************
## NUI enumeration function
##***********************

__NuiGetSensorCount = _NUIDLL.NuiGetSensorCount
__NuiGetSensorCount.argtypes = [ctypes.POINTER(ctypes.c_int)]
__NuiGetSensorCount.restype = ctypes.HRESULT

def _NuiGetSensorCount():
    count = ctypes.c_int()
    __NuiGetSensorCount(ctypes.byref(count))
    return count.value
    
__NuiCreateSensorByIndex = _NUIDLL.NuiCreateSensorByIndex
__NuiCreateSensorByIndex.argtypes = [ctypes.c_int, ctypes.POINTER(_NuiInstance)]
__NuiCreateSensorByIndex.restype = ctypes.HRESULT

def _NuiCreateSensorByIndex(index):
    inst = _NuiInstance()
    __NuiCreateSensorByIndex(index, ctypes.byref(inst))
    return inst
