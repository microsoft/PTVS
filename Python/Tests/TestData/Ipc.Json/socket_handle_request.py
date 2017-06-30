import os
import sys
import ptvsd.ipcjson as _ipc

class SocketIpcChannel(_ipc.SocketIO, _ipc.IpcChannel):
    def __init__(self, *args, **kwargs):
        super(SocketIpcChannel, self).__init__(*args, **kwargs)

    def on_testRequest(self, request, args):
        self.send_response(
            request,
            success=True,
            message='',
            requestText=args['dataText'],
            responseText='test response text'
        )

    def on_disconnect(self, request, args):
        self.send_response(request)
        self.__exit = True

def main():
    from optparse import OptionParser

    parser = OptionParser()
    parser.add_option('-r', '--result-port', type='int')
    (opts, _) = parser.parse_args()

    channel = SocketIpcChannel(port = opts.result_port)
    channel.process_messages()
    channel.close()

    sys.exit(0)

if __name__ == '__main__':
    main()
