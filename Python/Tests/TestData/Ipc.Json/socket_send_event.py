import os
import sys
import ptvsd.ipcjson as _ipc

class SocketIpcChannel(_ipc.SocketIO, _ipc.IpcChannel):
    def __init__(self, *args, **kwargs):
        super(SocketIpcChannel, self).__init__(*args, **kwargs)

def main():
    from optparse import OptionParser

    parser = OptionParser()
    parser.add_option('-r', '--result-port', type='int')
    (opts, _) = parser.parse_args()

    channel = SocketIpcChannel(port = opts.result_port)
    channel.send_event(
        name='testEvent', 
        dataText='python event data text',
        dataInt32=76
    )
    channel.close()

    sys.exit(0)

if __name__ == '__main__':
    main()
