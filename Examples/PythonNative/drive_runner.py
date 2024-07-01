# This file is for testing the runner.py without using C code
import threading
import win32pipe as win32pipe
import win32file as win32file

def main(pipe_path):
    pipe = win32pipe.CreateNamedPipe("\\\\.\\pipe\\MyNamedPipe", win32pipe.PIPE_ACCESS_DUPLEX, win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_READMODE_BYTE | win32pipe.PIPE_WAIT, 1, 65536, 65536, 300, None)
    input('Start the runner.py script and press Enter to continue...')
    while (win32pipe.ConnectNamedPipe(pipe, None) == 0):
        threading.Sleep(0.1)

    print('Sending data to the pipe')
    try:
        for i in range(2):
            some_data = b"C:\\Users\\rchiodo\\source\\repos\\PythonNativeSeparateThread\\test.py"
            win32file.WriteFile(pipe, some_data)
            result = win32file.ReadFile(pipe, 64*1024)
            print(f"Received {result[1].decode()}")
    except KeyboardInterrupt:
        print("Execution interrupted by user.")
    finally:
        win32file.CloseHandle(pipe)

if __name__ == "__main__":
    main("\\\\.\\pipe\\MyNamedPipe")
