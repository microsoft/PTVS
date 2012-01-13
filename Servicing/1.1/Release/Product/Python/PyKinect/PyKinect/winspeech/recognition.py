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

_audio_path = path.join(path.dirname(__file__), '..', 'pykinect', 'audio', 'PyKinectAudio.dll')
if not os.path.exists(_audio_path):
    _audio_path = path.join(path.dirname(__file__), '..', '..', 'Debug', 'PyKinectAudio.dll')
    if not path.exists(_audio_path):
        raise Exception('Cannot find PyKinectAudio.dll')


_PYAUDIODLL = ctypes.CDLL(_audio_path)

_CreateRecognizer = _PYAUDIODLL.CreateRecognizer
_CreateRecognizer.argtypes = [ctypes.c_voidp, ctypes.POINTER(ctypes.c_voidp)]
_CreateRecognizer.restype = ctypes.HRESULT

_SetInputFile = _PYAUDIODLL.SetInputFile
_SetInputFile.argtypes = [ctypes.c_voidp, ctypes.c_voidp]
_SetInputFile.restype = ctypes.HRESULT

_SetInputStream = _PYAUDIODLL.SetInputStream
_SetInputStream.argtypes = [ctypes.c_voidp, ctypes.c_voidp]
_SetInputStream.restype = ctypes.HRESULT

_IUnknownRelease = _PYAUDIODLL.IUnknownRelease
_IUnknownRelease.argtypes = [ctypes.c_voidp]
_IUnknownRelease.restype = None

_LoadGrammar = _PYAUDIODLL.LoadGrammar
_LoadGrammar.argtypes = [ctypes.c_wchar_p, ctypes.c_voidp, ctypes.POINTER(ctypes.c_voidp)]
_LoadGrammar.restype = ctypes.HRESULT

_EnumRecognizers  = _PYAUDIODLL.EnumRecognizers  

_ReadCallback = ctypes.WINFUNCTYPE(ctypes.HRESULT, ctypes.c_uint32, ctypes.c_voidp, ctypes.POINTER(ctypes.c_uint32))
_Recognize_Callback = ctypes.WINFUNCTYPE(None, ctypes.c_wchar_p)

_RecognizeOne = _PYAUDIODLL.RecognizeOne
_RecognizeOne.argtypes = [ctypes.c_voidp, ctypes.c_uint32, _Recognize_Callback, _Recognize_Callback]
_RecognizeOne.restype = ctypes.HRESULT

_RecognizeAsync = _PYAUDIODLL.RecognizeAsync
_RecognizeAsync.argtypes = [ctypes.c_voidp, ctypes.c_bool, _Recognize_Callback, _Recognize_Callback, ctypes.POINTER(ctypes.c_voidp)]
_RecognizeAsync.restype = ctypes.HRESULT

_StopRecognizeAsync = _PYAUDIODLL.StopRecognizeAsync
_StopRecognizeAsync.argtypes = [ctypes.c_voidp]
_StopRecognizeAsync.restype = ctypes.HRESULT

_EnumRecognizersCallback = ctypes.WINFUNCTYPE(None, ctypes.c_wchar_p, ctypes.c_wchar_p, ctypes.c_voidp)

class Grammar(object):
    """Represents a speech grammar constructed from an XML file"""
    def __init__(self, filename):
        self.filename = filename

    def __del__(self):
        #_IUnknownRelease(self._reco_ctx)
        _IUnknownRelease(self._grammar)


class RecognizerInfo(object):
    def __init__(self, id, description, token):
        self.id = id
        self.description = description
        self._token = token

    def __del__(self):
        _IUnknownRelease(self._token)

    def __repr__(self):
        return 'RecognizerInfo(%r, %r, ...)' % (self.id, self.description)


class RecognitionResult(object):
    def __init__(self, text, alternates = None):
        self.text = text
        if alternates:
            self.alternates = tuple(RecognitionResult(alt) for alt in alternates)
        else:
            self.alternates = ()
                

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


class RecognitionEventArgs(object):
    """Provides information about speech recognition events."""

    def __init__(self, result):
        self.result = result


class SpeechRecognitionEngine(object):
    """Provides the means to access and manage an in-process speech recognition engine."""

    def __init__(self, recognizer = None):
        self.speech_recognized = _event()
        self._async_handle = None

        if isinstance(recognizer, str):
            # TODO: Lookup by ID
            pass
        elif isinstance(recognizer, RecognizerInfo):
            rec = ctypes.c_voidp()
            _CreateRecognizer(recognizer._token, ctypes.byref(rec))
            self._rec = rec
        elif recognizer is None:
            rec = ctypes.c_voidp()
            _CreateRecognizer(None, ctypes.byref(rec))
            self._rec = rec
        else:
            raise TypeError('Bad type for recognizer: ' + repr(recognizer))
        
    def __del__(self):
        # TODO: Need to shut down any listening threads
        self.recognize_async_stop()
        _IUnknownRelease(self._rec)

    def load_grammar(self, grammar):
        if isinstance(grammar, str):
            grammar_obj = Grammar(grammar)
        else:
            grammar_obj = grammar

        comGrammar = ctypes.c_voidp()
        _LoadGrammar(grammar_obj.filename, self._rec, ctypes.byref(comGrammar))
        grammar_obj._grammar = comGrammar
        return grammar_obj

    def set_input_to_audio_file(self, stream):
        """sets the input to a Python file-like object which implements read"""

        stream_obj = getattr(stream, '__ISpStreamFormat__', None)
        if stream_obj is not None:
            # optimization: we can avoid going through Python to do the reading by passing
            # the original ISpStreamFormat object through
            _SetInputStream(self._rec, stream_obj)
        else:
            def reader(byteCount, buffer, bytesRead):
                bytes = stream.read(byteCount)
                ctypes.memmove(buffer, bytes, len(bytes))
                bytesRead.contents.value = len(bytes)
                return 0
            
            self._reader = _ReadCallback(reader)
            _SetInputFile(self._rec, self._reader)

    def recognize_sync(self, timeout = 30000):
        """attempts to recognize speech and returns the recognized text.  
        
By default times out after 30 seconds"""
        res = []
        alts = []
        def callback(text):
            res.append(text)
            
        def alt_callback(text):
            if text is not None:
                alts.append(text)

        _RecognizeOne(self._rec, timeout, _Recognize_Callback(callback), _Recognize_Callback(alt_callback))
        if res:
            return RecognitionResult(res[0], alts)

        return None

    def recognize_async(self, multiple = False):
        cur_result = []
        def callback(text):
            cur_result.append(text)

        def alt_callback(text):            
            if text == None:
                # send the event
                result = RecognitionResult(cur_result[0], cur_result[1:])
                event_args = RecognitionEventArgs(result)
                self.speech_recognized.fire(event_args)
                del cur_result[:]
            else:
                cur_result.append(text)

        stop_listening_handle = ctypes.c_voidp()
        
        # keep alive our function pointers on ourselves...
        self._async_callback = async_callback =_Recognize_Callback(callback)
        self._async_alt_callback = async_alt_callback = _Recognize_Callback(alt_callback)

        _RecognizeAsync(self._rec, multiple, async_callback, async_alt_callback, ctypes.byref(stop_listening_handle))
        self._async_handle = stop_listening_handle

    def recognize_async_stop(self):
        if self._async_handle is not None:
            _StopRecognizeAsync(self._async_handle)
            self._async_handle = None

    @staticmethod
    def installed_recognizers():
        ids = []
        def callback(id, description, token):
            ids.append(RecognizerInfo(id, description, token))
        _EnumRecognizers(_EnumRecognizersCallback(callback))

        return ids

