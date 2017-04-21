import os
import sys

try:
  import visualstudio_py_ipcjson as _ipc
except ImportError:
  import ptvsd.visualstudio_py_ipcjson as _ipc

class TestChannelBase(object):
    def __init__(self, *args, **kwargs):
        self.__message = []

    def close(self):
        self._close()

    def process_one_message(self):
        self._wait_for_message()
        return self.__message.pop(0)

    def _receive_message(self, message):
        self.__message.append(message)

class TestChannel(_ipc.SocketIO, TestChannelBase):
    pass

def main():
    from optparse import OptionParser

    parser = OptionParser()
    parser.add_option('-r', '--result-port', type='int')
    (opts, _) = parser.parse_args()

    channel = TestChannel(port = opts.result_port)
    msg = channel.process_one_message()
    channel.close()

    sys.exit(0)

if __name__ == '__main__':
    main()
