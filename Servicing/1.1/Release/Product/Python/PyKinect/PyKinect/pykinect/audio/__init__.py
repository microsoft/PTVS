 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # Available under the Microsoft PyKinect 1.0 Alpha license.  See LICENSE.txt
 # for more information.
 #
 # ###########################################################################/

import os
import ctypes
from os import path

_audiodll_path = os.path.join(os.environ['WINDIR'], 'System32', 'KinectAudio10.dll')

_MAX_STR_LEN = 512
_AUDIODLL = ctypes.WinDLL(_audiodll_path)

_audio_path = path.join(path.dirname(__file__), 'PyKinectAudio.dll')
if not os.path.exists(_audio_path):
    _audio_path = path.join(path.dirname(__file__), '..', '..', '..', 'Debug', 'PyKinectAudio.dll')
    if not path.exists(_audio_path):
        raise Exception('Cannot find PyKinectAudio.dll')

_PYAUDIODLL = ctypes.CDLL(_audio_path)
_OpenKinectAudio = _PYAUDIODLL.OpenKinectAudio
_OpenKinectAudio.argtypes = [ctypes.POINTER(ctypes.c_voidp)]
_OpenKinectAudio.restype = ctypes.HRESULT

_OpenAudioStream = _PYAUDIODLL.OpenAudioStream
_OpenAudioStream.argtypes = [ctypes.c_voidp, ctypes.POINTER(ctypes.c_voidp), ctypes.c_uint32]
_OpenAudioStream.restype = ctypes.HRESULT

_SetDeviceProperty_Bool = _PYAUDIODLL.SetDeviceProperty_Bool
_SetDeviceProperty_Bool.argtypes = [ctypes.c_voidp, ctypes.c_uint32, ctypes.c_bool]
_SetDeviceProperty_Bool.restype = ctypes.HRESULT

_SetDeviceProperty_Int = _PYAUDIODLL.SetDeviceProperty_Int
_SetDeviceProperty_Int.argtypes = [ctypes.c_voidp, ctypes.c_uint32, ctypes.c_int]
_SetDeviceProperty_Int.restype = ctypes.HRESULT

_GetDeviceProperty_Bool = _PYAUDIODLL.GetDeviceProperty_Bool
_GetDeviceProperty_Bool.argtypes = [ctypes.c_voidp, ctypes.c_uint32, ctypes.POINTER(ctypes.c_bool)]
_GetDeviceProperty_Bool.restype = ctypes.HRESULT

_GetDeviceProperty_Int = _PYAUDIODLL.GetDeviceProperty_Int
_GetDeviceProperty_Int.argtypes = [ctypes.c_voidp, ctypes.c_uint32, ctypes.POINTER(ctypes.c_int)]
_GetDeviceProperty_Int.restype = ctypes.HRESULT


#class AecQualityMetrics(ctypes.Structure):
#    _fields_ = [
#                ('timestamp', ctypes.c_longlong),
#                ('convergence_flag', ctypes.c_byte),
#                ('mic_clipped_flag', ctypes.c_byte),
#                ('mic_silence_flag', ctypes.c_byte),
#                ('pstv_feadback_flag', ctypes.c_byte),
#                ('spk_clipped_flag', ctypes.c_byte),
#                ('spk_mute_flag', ctypes.c_byte),
#                ('glitch_flag', ctypes.c_byte),
#                ('double_talk_flag', ctypes.c_byte),
#                ('glitch_count', ctypes.c_ulong),
#                ('mic_clip_count', ctypes.c_ulong),
#                ('duration', ctypes.c_float),
#                ('ts_variance', ctypes.c_float),
#                ('ts_drift_rate', ctypes.c_float),
#                ('voice_level', ctypes.c_float),
#                ('noise_Level', ctypes.c_float),
#                ('echo_return_loss_enhancement', ctypes.c_float),
#                ('avg_echo_return_loss_enhancement', ctypes.c_float),
#                ('reserved', ctypes.c_uint32),
#               ]

#_GetDeviceProperty_QualityMetrics = _PYAUDIODLL.GetDeviceProperty_QualityMetrics
#_GetDeviceProperty_QualityMetrics.argtypes = [ctypes.c_voidp, ctypes.c_uint32, ctypes.POINTER(AecQualityMetrics)]
#_GetDeviceProperty_QualityMetrics.restype = ctypes.HRESULT

_ReadAudioStream = _PYAUDIODLL.ReadAudioStream
_ReadAudioStream.argtypes = [ctypes.c_voidp, ctypes.c_voidp, ctypes.c_uint32, ctypes.POINTER(ctypes.c_uint32)]
_ReadAudioStream.restype = ctypes.HRESULT

_IUnknownRelease = _PYAUDIODLL.IUnknownRelease
_IUnknownRelease.argtypes = [ctypes.c_voidp]
_IUnknownRelease.restype = None

_Recognize_Callback = ctypes.WINFUNCTYPE(None, ctypes.c_wchar_p)

_ReadCallback = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_uint32, ctypes.c_voidp, ctypes.POINTER(ctypes.c_uint32))

_OLE32 = ctypes.WinDLL('ole32.dll')
_CoInitialize = _OLE32.CoInitialize
_CoInitialize.argtypes = [ctypes.c_voidp]
_CoInitialize.restype = ctypes.HRESULT

_CoInitialize(None)

class AudioDeviceInfo(ctypes.Structure):
    """Describes a systems Kinect sensors. """
    _fields_ = [('device_name', ctypes.c_wchar * _MAX_STR_LEN),
                ('device_id', ctypes.c_wchar * _MAX_STR_LEN),
                ('device_index', ctypes.c_int),
                ]

_NuiGetMicrophoneArrayDevices = _AUDIODLL.NuiGetMicrophoneArrayDevices
_NuiGetMicrophoneArrayDevices.argtypes = [ctypes.POINTER(AudioDeviceInfo), ctypes.POINTER(ctypes.c_int)]
_NuiGetMicrophoneArrayDevices.restype = ctypes.HRESULT

def GetKinectDeviceInfo():
    '''returns a sequence of AudioDeviceInfo objects describing the available Kinect audio devices'''
    count = ctypes.c_int()
    _NuiGetMicrophoneArrayDevices(None, 0, ctypes.byref(count))
    deviceArray = (AudioDeviceInfo * count.value)()

    _NuiGetMicrophoneArrayDevices(deviceArray, count.value, ctypes.byref(count))

    return [arrayObj for arrayObj in deviceArray]


_MFPKEY_WMAAECMA_SYSTEM_MODE = 2
_MFPKEY_WMAAECMA_DMO_SOURCE_MODE = 3
_MFPKEY_WMAAECMA_DEVICE_INDEXES = 4
_MFPKEY_WMAAECMA_FEATURE_MODE = 5
_MFPKEY_WMAAECMA_FEATR_FRAME_SIZE = 6
_MFPKEY_WMAAECMA_FEATR_ECHO_LENGTH = 7
_MFPKEY_WMAAECMA_FEATR_NS = 8
_MFPKEY_WMAAECMA_FEATR_AGC = 9
_MFPKEY_WMAAECMA_FEATR_AES = 10
_MFPKEY_WMAAECMA_FEATR_VAD = 11
_MFPKEY_WMAAECMA_FEATR_CENTER_CLIP = 12
_MFPKEY_WMAAECMA_FEATR_NOISE_FILL = 13
_MFPKEY_WMAAECMA_RETRIEVE_TS_STATS = 14
_MFPKEY_WMAAECMA_QUALITY_METRICS = 15
_MFPKEY_WMAAECMA_DEVICEPAIR_GUID = 0x11
_MFPKEY_WMAAECMA_FEATR_MICARR_MODE = 0x12
_MFPKEY_WMAAECMA_FEATR_MICARR_BEAM = 0x13
_MFPKEY_WMAAECMA_MIC_GAIN_BOUNDER = 0x15
_MFPKEY_WMAAECMA_FEATR_MICARR_PREPROC = 20


_AudioPropertySetters = {
        bool : _SetDeviceProperty_Bool,
        int : _SetDeviceProperty_Int,
}

_AudioPropertyGetters = {
        bool : (_GetDeviceProperty_Bool, ctypes.c_bool),
        int : (_GetDeviceProperty_Int, ctypes.c_int),
        #AecQualityMetrics: (_GetDeviceProperty_QualityMetrics,  AecQualityMetrics),
 }

class _AudioSourceProperty(object):
    """internal descriptor used for all of our properties"""
    def __init__(self, index, prop_type, doc = None):
        self.index = index
        self.prop_type = prop_type
        self.__doc__ = doc

    def __get__(self, inst, context):
        getter_func, getter_type = _AudioPropertyGetters[self.prop_type]
        value = getter_type()
        getter_func(inst._dmo, self.index, ctypes.byref(value))
        return value.value


class _SettableAudioSourceProperty(_AudioSourceProperty):
    def __set__(self, inst, value):
        _AudioPropertySetters[self.prop_type](inst._dmo, self.index, value)


class MicArrayMode(object):
    MicArrayAdaptiveBeam = 0x1100,
    MicArrayExternBeam = 0x800,
    MicArrayFixedBeam = 0x400,
    MicArraySimpleSum = 0x100,
    MicArraySingleBeam = 0x200,
    MicArraySingleChan = 0

 
class _AudioFile(object):
    """provides a file-like object for reading from the kinect audio stream"""

    def __init__(self, stream):
        self.closed = False
        self.__ISpStreamFormat__ = stream
        self._buffer = (ctypes.c_byte * 4096)()
        self._buffered_bytes = 0
        self._buffer_start = 0

    def __del__(self):
        if self.__ISpStreamFormat__ is not None:
            _IUnknownRelease(self.__ISpStreamFormat__)

    def close(self):
        self.closed = True
        _IUnknownRelease(self.__ISpStreamFormat__)
        self.__ISpStreamFormat__ = None

    def flush(self):
        pass

    def next(self):
        raise NotImplementedError()

    def __iter__(self):
        raise NotImplementedError()

    def read(self, size = 4096):
        if self.closed:
            raise IOError('Kinect audio source has been closed')

        to_read = size
        res = []
        bytes_read = ctypes.c_uint32()
        while to_read != 0:            
            if self._buffer_start != self._buffered_bytes:
                to_append = min(to_read, self._buffered_bytes - self._buffer_start)

                res.append(ctypes.string_at(ctypes.addressof(self._buffer) + self._buffer_start, to_append))
                
                self._buffer_start += to_append
                to_read -= to_append

                if not to_read:
                    break

            # read more data...
            _ReadAudioStream(self.__ISpStreamFormat__, self._buffer, len(self._buffer), ctypes.byref(bytes_read))
            self._buffer_start = 0
            self._buffered_bytes = bytes_read.value
        
        return ''.join(res)

    def readline(self, size = None):
        raise NotImplementedError()

    def readlines(self, sizehint = None):
        raise NotImplementedError()

    def xreadlines(self):
        raise NotImplementedError()

    def seek(self, offset, whence):
        raise NotImplementedError()

    def tell(self):
        raise NotImplementedError()

    def truncate(self, size = None):
        raise IOError('KinectAudio file not open for writing')

    def write(self, str):
        raise IOError('KinectAudio file not open for writing')

    def writelines(self, sequence):
        raise IOError('KinectAudio file not open for writing')
    
    @property
    def name(self):
        return 'KinectAudio'

    @property
    def mode(self):
        return 'rb'

class KinectAudioSource(object):
    def __init__(self, device = None):
        self._dmo = None
        dmo = ctypes.c_voidp()
        _OpenKinectAudio(ctypes.byref(dmo))
        self._dmo = dmo
        self._file = None

    def __del__(self):
        if self._dmo is not None:
            _IUnknownRelease(self._dmo)

    def __enter__(self):
        return self

    def __exit__(self, type, value, traceback):
        _IUnknownRelease(self._dmo)
        self._dmo = None

    def start(self, readStaleThreshold = 500):
        """Starts capturing audio from the Kinect sensor's microphone array into a buffer.   Returns a file-like object that represents the audio stream, which is in 16khz, 16 bit PCM format. 
        
readStaleThreshold: Specifies how long to retain data in the buffer (in milliseconds). If you do not read from the stream for longer than this "stale data" threshold value the DMO discards any buffered audio.

        """
        if self._file is not None:
            raise Exception('Capture already started')

        audio_stream = ctypes.c_voidp()
        _OpenAudioStream(self._dmo, ctypes.byref(audio_stream), readStaleThreshold)

        self._file = _AudioFile(audio_stream)
        return self._file

    def stop(self):
        if self._file is not None:
            self._file.close()        
    
    acoustic_echo_suppression = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_FEATR_AES, int)
    automatic_gain_control = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_FEATR_AGC, bool)
    center_clip = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_FEATR_CENTER_CLIP, bool)
    echo_length = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_FEATR_ECHO_LENGTH, int)
    feature_mode = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_FEATURE_MODE, bool)
    frame_size = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_FEATR_FRAME_SIZE, int)
    gain_bounder = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_MIC_GAIN_BOUNDER, bool)
    mic_array_mode = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_FEATR_MICARR_MODE, int)
    mic_array_preprocess = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_FEATR_MICARR_PREPROC, bool)
    noise_fill = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_FEATR_NOISE_FILL, bool)
    noise_suppression = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_FEATR_NS, int)
    #quality_metrics = _AudioSourceProperty(_MFPKEY_WMAAECMA_QUALITY_METRICS, AecQualityMetrics) 
    retrieve_ts_stats = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_RETRIEVE_TS_STATS, bool)
    source_mode = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_DMO_SOURCE_MODE, bool)
    system_mode = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_SYSTEM_MODE, int)
    voice_activity_detector = _SettableAudioSourceProperty(_MFPKEY_WMAAECMA_FEATR_VAD, int)
    device_pair_guid = _AudioSourceProperty(_MFPKEY_WMAAECMA_DEVICEPAIR_GUID, object) # TODO: Read only, guid type

    # TODO: Properties
    # speaker_index
    # sound_source_position_confidence
    # sound_source_position
    # mic_array_beam_angle

    # TODO: Events
    # beam_changed event

    pass

