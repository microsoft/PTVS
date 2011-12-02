 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # Available under the Microsoft PyKinect 1.0 Alpha license.  See LICENSE.txt
 # for more information.
 #
 # ###########################################################################/

import unittest
from unittest.main import TestProgram
from pykinect.nui import Device, Runtime, KinectError, ImageResolution, ImageStreamType, ImageType, ImageStream
from pykinect.audio import KinectAudioSource, GetKinectDeviceInfo
from speech.recognition import SpeechRecognitionEngine, Grammar
import time

class KinectTestCases(unittest.TestCase):
    def test_device(self):
        # our test cases run with only a single Kinect installed
        d = Device()        
        self.assertEqual(d.count, 1)
        
        # device is a singleton
        d2 = Device()        
        self.assertIs(d, d2)
    
    def test_runtime_creation(self):
        # create the runtime, check the instance
        with Runtime() as nui:        
            self.assertEqual(nui.instance_index, 0)
        
        # we should be able to create a 2nd runtime after the first is disposed
        with Runtime() as nui2:
            self.assertEqual(nui2.instance_index, 0)

        # accessing a disposed runtime should throw
        self.assertRaises(KinectError, lambda: nui2.instance_index)

        #with Runtime() as nui:
        #    # creating a 2nd runtime w/ the 1st existing should throw
        #    self.assertRaises(KinectError, Runtime)

    def test_video_stream(self):
        with Runtime() as nui:        
            nui.video_stream.open(ImageStreamType.Depth, 2, ImageResolution.Resolution640x480, ImageType.Color)            

            # we can only open a single stream at a time
            self.assertRaises(
                KinectError,
                nui.video_stream.open,
                ImageStreamType.Video, 
                2, 
                ImageResolution.Resolution1280x1024, 
                ImageType.Color
            )   

        
        valid_resolutions = {
            ImageType.Color : (ImageResolution.Resolution1280x1024, ImageResolution.Resolution640x480),
            ImageType.ColorYuv : (ImageResolution.Resolution640x480, ),
            ImageType.Color : (ImageResolution.Resolution640x480, ),
            ImageType.DepthAndPlayerIndex : (ImageResolution.Resolution320x240, ImageResolution.Resolution80x60),
            ImageType.Depth : (ImageResolution.Resolution320x240, ImageResolution.Resolution640x480, ImageResolution.Resolution80x60),
        } 

        for image_type, resolution_list in valid_resolutions.items():
            for resolution in resolution_list:
                with Runtime() as nui:   
                    nui.video_stream.open(ImageStreamType.Video, 2, resolution, image_type)

        with Runtime() as nui:    
            invalid_resolutions = {
                ImageType.Color : (ImageResolution.Resolution320x240, ImageResolution.Resolution80x60),
                ImageType.DepthAndPlayerIndex : (ImageResolution.Resolution1280x1024, ImageResolution.Resolution640x480),
                ImageType.ColorYuv : (ImageResolution.Resolution1280x1024, ImageResolution.Resolution320x240, ImageResolution.Resolution80x60),
                ImageType.Color : (ImageResolution.Resolution320x240, ImageResolution.Resolution80x60),
                ImageType.Depth : (ImageResolution.Resolution1280x1024, ),
            }

            for image_type, resolution_list in invalid_resolutions.items():
                for resolution in resolution_list:
                    self.assertRaises(
                                      KinectError, 
                                      nui.video_stream.open,
                                      ImageStreamType.Video, 
                                      2, 
                                      resolution, 
                                      image_type
                    )

    def test_image_stream_get_valid_resolutions(self):
        self.assertEqual(ImageStream.get_valid_resolutions(ImageType.DepthAndPlayerIndex), (ImageResolution.Resolution320x240, ))
        self.assertEqual(ImageStream.get_valid_resolutions(ImageType.Color), (ImageResolution.Resolution1280x1024, ImageResolution.Resolution640x480))
        self.assertEqual(ImageStream.get_valid_resolutions(ImageType.ColorYuv), (ImageResolution.Resolution640x480, ))
        self.assertEqual(ImageStream.get_valid_resolutions(ImageType.ColorYuvRaw), (ImageResolution.Resolution640x480, ))
        self.assertEqual(ImageStream.get_valid_resolutions(ImageType.Depth), (ImageResolution.Resolution640x480, ))
        self.assertRaises(KinectError, lambda: ImageStream.get_valid_resolutions(1000))

    def test_skeleton_engine(self):
        with Runtime() as nui:
            self.assertEqual(nui.skeleton_engine.enabled, False)
            nui.skeleton_engine.enabled = True
            self.assertEqual(nui.skeleton_engine.enabled, True)
            frame = nui.skeleton_engine.get_next_frame()

    def test_audio_source_file(self):
        source = KinectAudioSource()
        audio_file = source.start()
        data = audio_file.read()
        self.assertEqual(len(data), 4096)

    def test_audio_source_file_close(self):
        with KinectAudioSource() as source:
            audio_file = source.start()
            audio_file.close()

            self.assertRaises(IOError, lambda: audio_file.read())
        
    def test_audio_source_source_stop(self):
        with KinectAudioSource() as source:
            audio_file = source.start()
            source.stop()

            self.assertRaises(IOError, lambda: audio_file.read())

    def test_audio_source_properties(self):
        with KinectAudioSource() as source:
            source.feature_mode = True
            self.assertEqual(source.feature_mode, True)
            
            attrs = [
                      # name,                      default value    new value
                     ('acoustic_echo_suppression',  1,               0),
                     ('automatic_gain_control',     False,           True),
                     ('center_clip',                False,           True),
                     ('echo_length',                256,             128),
                     ('frame_size',                 256,             128),
                     ('gain_bounder',               True,            False),
                     ('mic_array_mode',             512,             256),
                     ('mic_array_preprocess',       True,            False),
                     ('noise_fill',                 False,           True),
                     ('noise_suppression',          1,               0),
                     ('source_mode',                True,            False),
                     ('system_mode',                2,               1),
                     ('voice_activity_detector',    0,               0),
            ]

            for name, default, new in attrs:
                self.assertEqual(getattr(source, name), default)
                setattr(source, name, new)
                self.assertEqual(getattr(source, name), new)

    def test_recognize_audio(self):
        pass
    """
        with KinectAudioSource() as source:
            audio_file = source.start()

            rec = SpeechRecognitionEngine()
            grammar = rec.load_grammar('Grammar.xml')

            rec.set_input_to_audio_file(audio_file)

            res = rec.recognize_sync()"""
    
    def test_get_kinect_info(self):
        devices = GetKinectDeviceInfo()
        self.assertEqual(len(devices), 1)
        device = devices[0]
        self.assertEqual(device.device_name, 'Microphone Array (Kinect USB Audio)')
        self.assertEqual(device.device_index, 1)

    def test_installed_recognizers(self):
        recognizers = SpeechRecognitionEngine.installed_recognizers()

        recognizer = SpeechRecognitionEngine(recognizers[0])

    def test_default_recognizer(self):
        rec = SpeechRecognitionEngine()
        grammar = rec.load_grammar('Grammar.xml')
        input = file('test.pcm', 'rb')
        rec.set_input_to_audio_file(input)

        self.assertEqual(rec.recognize_sync().text, 'down')
        self.assertEqual(rec.recognize_sync().text, 'left')
        self.assertEqual(rec.recognize_sync().text, 'right')

    def test_default_recognizer_async_one(self):
        rec = SpeechRecognitionEngine()
        grammar = rec.load_grammar('Grammar.xml')
        input = file('test.pcm', 'rb')
        rec.set_input_to_audio_file(input)

        recognized_values = []
        def recognized(result):
            recognized_values.append(result.result.text)

        rec.speech_recognized += recognized
        rec.recognize_async()
        
        time.sleep(5)
        self.assertEqual(len(recognized_values), 1)
        self.assertEqual(recognized_values[0], 'down')

    def test_default_recognizer_async_multiple(self):
        rec = SpeechRecognitionEngine()
        grammar = rec.load_grammar('Grammar.xml')
        input = file('test.pcm', 'rb')
        rec.set_input_to_audio_file(input)

        recognized_values = []
        def recognized(result):
            recognized_values.append(result.result.text)

        rec.speech_recognized += recognized
        rec.recognize_async(multiple=True)
        
        time.sleep(5)
        self.assertEqual(len(recognized_values), 3)
        self.assertEqual(recognized_values[0], 'down')
        self.assertEqual(recognized_values[1], 'left')
        self.assertEqual(recognized_values[2], 'right')

if __name__ == '__main__':
    unittest.main()


