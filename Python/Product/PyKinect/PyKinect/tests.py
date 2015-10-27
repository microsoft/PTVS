# PyKinect
# Copyright(c) Microsoft Corporation
# All rights reserved.
# 
# Licensed under the Apache License, Version 2.0 (the License); you may not use
# this file except in compliance with the License. You may obtain a copy of the
# License at http://www.apache.org/licenses/LICENSE-2.0
# 
# THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
# IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
# MERCHANTABLITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

import unittest
import ctypes
from unittest.main import TestProgram
from pykinect.nui import (Device, Runtime, KinectError, ImageResolution, 
                          ImageStreamType, ImageType, ImageStream)
from pykinect.audio import KinectAudioSource, GetKinectDeviceInfo
from winspeech.recognition import SpeechRecognitionEngine, Grammar
import time
from pykinect.nui.structs import (SkeletonData, SkeletonTrackingState, Vector, 
                                  JointId, SkeletonQuality, JointTrackingState, 
                                  ImageViewArea, SkeletonFrameQuality, NUI_SKELETON_COUNT, 
                                  SkeletonFrame, TransformSmoothParameters)


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

    def assertCtypesEquals(self, value1, value2):
        if type(type(value1)) is type(ctypes.Array):
            self.assertEqual(len(value1), len(value2))
            for i in range(len(value1)):
                self.assertCtypesEquals(value1[i], value2[i])
        else:
            self.assertEqual(value1, value2)

    def interop_prop_test(self, interop_obj, tests):
        for friendly_name, interop_name, value in tests:
            print friendly_name
            setattr(interop_obj, friendly_name, value)
            
            self.assertCtypesEquals(getattr(interop_obj, friendly_name), value)        
            self.assertCtypesEquals(getattr(interop_obj, interop_name), value)

    def test_skeleton_data(self):
        pos_arr = ctypes.ARRAY(Vector, JointId.count.value)()
        pos_arr[0] = Vector(2,4,6,8)
        joint_arr = ctypes.ARRAY(JointTrackingState, JointId.count.value)()
        joint_arr[0] = JointTrackingState.inferred

        tests = [('tracking_state', 'eTrackingState', SkeletonTrackingState.tracked),
                 ('tracking_id', 'dwTrackingID', 1),
                 ('enrollment_index', 'dwEnrollmentIndex', 1),
                 ('user_index', 'dwUserIndex', 1),
                 ('position', 'Position', Vector(1, 2, 3, 4)),
                 ('skeleton_positions', 'SkeletonPositions', pos_arr),
                 ('skeleton_position_tracking_states', 'eSkeletonPositionTrackingState', joint_arr),
                 ('skeleton_quality', 'Quality', SkeletonQuality.clipped_bottom),
                ]

        self.interop_prop_test(SkeletonData(), tests)

    def test_image_view_area(self):
        tests = [('zoom', 'Zoom', 1),
                 ('center_x', 'CenterX', 1),
                 ('center_y', 'CenterY', 1),
                ]

        self.interop_prop_test(ImageViewArea(), tests)

    def test_skeleton_frame(self):
        skel_data = ctypes.ARRAY(SkeletonData, NUI_SKELETON_COUNT)()
        sd = SkeletonData()
        sd.user_index = 5
        skel_data[0] = sd
        tests = [('timestamp', 'liTimeStamp', 1),
                 ('frame_number', 'dwFrameNumber', 2),
                 ('quality', 'Quality', SkeletonFrameQuality.camera_motion),
                 ('floor_clip_plane', 'vFloorClipPlane', Vector(2,4,6,8)),
                 ('normal_to_gravity', 'vNormalToGravity', Vector(1,2,3,4)),
                 ('skeleton_data', 'SkeletonData', skel_data),
                 ]

        self.interop_prop_test(SkeletonFrame(), tests)

    def test_TransformSmoothParameters(self):
        tests = [('smoothing', 'fSmoothing', 1),
                 ('correction', 'fCorrection', 2),
                 ('prediction', 'fPrediction', 3),
                 ('jitter_radius', 'fJitterRadius', 4),
                 ('max_deviation_radius', 'fMaxDeviationRadius', 5),
                 ]

        self.interop_prop_test(TransformSmoothParameters(), tests)
        pass

if __name__ == '__main__':
    unittest.main()


