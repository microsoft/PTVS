import runpy
import os

def execute_file(file_path):
    print("Executing " + file_path)
    try:
        runpy.run_path(file_path)
    except Exception as e:
        print(f"Error executing {file_path}: {str(e)}")

def main(pipe_path):
    pipe = os.open(pipe_path, os.O_BINARY | os.O_RDWR)
    try:
        while True:
            data = os.read(pipe, 64*1024)
            execute_file(data.decode())
            os.write(pipe, b"1")
    except KeyboardInterrupt:
        print("Execution interrupted by user.")
    finally:
        os.close(pipe)
        print("Runner exited")


if __name__ == "__main__":
    main("\\\\.\\pipe\\MyNamedPipe")
else:
    print("Usage: python runner.py")

