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

if ctypes.sizeof(ctypes.c_voidp) == 4:
    # assembly thunks (x86 only), go from stdcall to cdecl

    # 58              pop     eax                               // pop return address
    # 59              pop     ecx                               // pop this pointer
    # 5a              pop     edx                               // pop the vtable index
    # 50              push    eax                               // push the return address
    # 8b01            mov     eax,dword ptr [ecx]               // load the vtable address
    # 8b1490          mov     edx,dword ptr [eax+edx*4]         // load the final function address
    # ffe2            jmp     edx                               // jmp

    _stub_template = b'\x58\x59\x5a\x50\x8b\x01\x8b\x14\x90\xff\xe2'

    _VirtualAlloc = _kernel32.VirtualAlloc
    _VirtualAlloc.argtypes = [ctypes.c_voidp, ctypes.c_uint32, ctypes.c_uint32, ctypes.c_uint32]
    _VirtualAlloc.restype = ctypes.c_voidp

    _VirtualProtect = _kernel32.VirtualProtect
    _VirtualProtect.argtypes = [ctypes.c_voidp, ctypes.c_uint32, ctypes.c_uint32, ctypes.POINTER(ctypes.c_uint32)]
    _VirtualProtect.restype = ctypes.c_bool

    _MEM_COMMIT = 0x1000
    _MEM_RESERVE = 0x2000

    _PAGE_EXECUTE_READWRITE = 0x40
    _PAGE_EXECUTE_READ = 0x20

    _stub_addr = _VirtualAlloc(None, 4096, _MEM_COMMIT | _MEM_RESERVE, _PAGE_EXECUTE_READWRITE)
    if not _stub_addr:
        raise MemoryError('failed to allocate memory for thunks')

    ctypes.memmove(_stub_addr, _stub_template, len(_stub_template))

    if not _VirtualProtect(_stub_addr, 4096, _PAGE_EXECUTE_READ, ctypes.byref(ctypes.c_uint32())):
        raise WindowsError('failed to protect memory')

    def _THISCALLFUNCTYPE(restype, *argtypes):
        def maker(index, name):
            func = ctypes.WINFUNCTYPE(restype, *(ctypes.c_voidp, ctypes.c_uint32) + argtypes)(_stub_addr)
            def invoker(self, *args):
                return func(self, index, *args)
            return invoker
        return maker

else:
    _THISCALLFUNCTYPE = ctypes.CFUNCTYPE


class _NuiInstance(ctypes.c_voidp):
    """this interface duplicates exactly the public DLL NUI**** methods that
       work on just device #0. If you want to work with multiple devices,
       use these methods off the INuiInstance, after getting a INuiInstance * from
       the multiple-device methods below"""

    # vtable
    _InstanceIndex = _THISCALLFUNCTYPE(ctypes.c_int)(0, 'InstanceIndex')
    _NuiInitialize = _THISCALLFUNCTYPE(ctypes.HRESULT, ctypes.c_uint32)(1, 'NuiInitialize')
    _NuiShutdown = _THISCALLFUNCTYPE(None)(2, 'NuiShutdown')
    _NuiImageStreamOpen = _THISCALLFUNCTYPE(_KinectHRESULT, ImageType, ImageResolution, ctypes.c_uint32, ctypes.c_uint32, ctypes.c_voidp, ctypes.POINTER(ctypes.c_voidp))(3, 'NuiImageStreamOpen')
    _NuiImageStreamGetNextFrame = _THISCALLFUNCTYPE(ctypes.HRESULT, ctypes.c_voidp, ctypes.c_uint32, ctypes.POINTER(ctypes.POINTER(ImageFrame)))(4, 'NuiImageStreamGetNextFrame')
    _NuiImageStreamReleaseFrame = _THISCALLFUNCTYPE(ctypes.HRESULT, ctypes.c_voidp, ctypes.POINTER(ImageFrame))(5, 'NuiImageStreamReleaseFrame')
    _NuiImageGetColorPixelCoordinatesFromDepthPixel = _THISCALLFUNCTYPE(ctypes.HRESULT, ImageResolution, ctypes.POINTER(ImageViewArea), ctypes.c_long, ctypes.c_long, ctypes.c_uint16, ctypes.POINTER(ctypes.c_long), ctypes.POINTER(ctypes.c_long))(6, 'NuiImageGetColorPixelCoordinatesFromDepthPixel')
    _NuiCameraElevationSetAngle = _THISCALLFUNCTYPE(ctypes.HRESULT, ctypes.c_long)(7, 'NuiCameraElevationSetAngle')
    _NuiCameraElevationGetAngle = _THISCALLFUNCTYPE(ctypes.HRESULT, ctypes.POINTER(ctypes.c_long))(8, 'NuiCameraElevationGetAngle')
    _NuiSkeletonTrackingEnable = _THISCALLFUNCTYPE(ctypes.HRESULT, ctypes.c_voidp, ctypes.c_uint32)(9, 'NuiSkeletonTrackingEnable')
    _NuiSkeletonTrackingDisable = _THISCALLFUNCTYPE(ctypes.HRESULT)(10, 'NuiSkeletonTrackingDisable')
    _NuiSkeletonGetNextFrame = _THISCALLFUNCTYPE(_KinectHRESULT, ctypes.c_uint32, ctypes.POINTER(SkeletonFrame))(11, 'NuiSkeletonGetNextFrame')
    _NuiTransformSmooth = _THISCALLFUNCTYPE(ctypes.HRESULT, ctypes.POINTER(SkeletonFrame), ctypes.POINTER(TransformSmoothParameters))(12, 'NuiTransformSmooth')
    _MSR_NuiGetPropsBlob = _THISCALLFUNCTYPE(ctypes.c_bool, _PropsIndex, ctypes.c_voidp, ctypes.POINTER(ctypes.c_uint32))(13, 'MSR_NuiGetPropsBlob')
    _MSR_NuiGetPropsType = _THISCALLFUNCTYPE(_PropType, _PropsIndex)(14, 'MSR_NuiGetPropsType')

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
        res = ctypes.POINTER(ImageFrame)()
        _NuiInstance._NuiImageStreamGetNextFrame(self, hStream, dwMillisecondsToWait, res)
        return res.contents

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

_MSR_NUIGetDeviceCount = _NUIDLL.MSR_NUIGetDeviceCount
_MSR_NUIGetDeviceCount.argtypes = [ctypes.POINTER(ctypes.c_int)]
_MSR_NUIGetDeviceCount.restype = ctypes.HRESULT

def _NuiGetDeviceCount():
    count = ctypes.c_int()
    _MSR_NUIGetDeviceCount(ctypes.byref(count))
    return count.value
    
_MSR_NuiCreateInstanceByIndex = _NUIDLL.MSR_NuiCreateInstanceByIndex
_MSR_NuiCreateInstanceByIndex.argtypes = [ctypes.c_int, ctypes.POINTER(_NuiInstance)]
_MSR_NuiCreateInstanceByIndex.restype = ctypes.HRESULT

def _NuiCreateInstanceByIndex(index):
    inst = _NuiInstance()
    _MSR_NuiCreateInstanceByIndex(index, ctypes.byref(inst))
    return inst

_MSR_NuiDestroyInstance = _NUIDLL.MSR_NuiDestroyInstance
_MSR_NuiDestroyInstance.argtypes = [_NuiInstance]
_MSR_NuiDestroyInstance.restype = None