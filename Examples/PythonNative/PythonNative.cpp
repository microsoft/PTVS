#define PY_SSIZE_T_CLEAN
#include <Python.h>
#include <Windows.h>
#include <iostream>

DWORD WINAPI runner(LPVOID lpParam)
{
    // Runs the python runner that will listen on the named pipe.
    Py_Initialize();

    std::cout << "Python version: " << PY_VERSION << std::endl;

    char startFile[] = "C:\\Users\\rchiodo\\source\\repos\\PythonNativeSeparateThread\\runner.py";

    PyObject* startObj = Py_BuildValue("s", startFile);
    FILE* file = _Py_fopen_obj(startObj, "r+");
    if (file != NULL) {
        PyRun_SimpleFile(file, startFile);
    }

    Py_Finalize();
    return 0;
}

HANDLE create_pipe() {
    HANDLE result = CreateNamedPipe(
        L"\\\\.\\pipe\\MyNamedPipe",
        PIPE_ACCESS_DUPLEX,        
        PIPE_TYPE_BYTE |
        PIPE_WAIT,                  
        1,                          
        4096,                       
        4096,                       
        0,                          
        NULL                        
    );

    if (result == INVALID_HANDLE_VALUE) {
        DWORD error = GetLastError();
        std::cout << "Named pipe failed: " << error << std::endl;
    }
    return result;
}


int main()
{
    HANDLE pipe = create_pipe();
    DWORD dwThreadId = 0;
    HANDLE runnerThread = CreateThread(NULL, 0, runner, NULL, 0, &dwThreadId);

    // Wait for the python code to attach to the pipe
    BOOL result = ConnectNamedPipe(pipe, NULL);
    while (!result && GetLastError() == ERROR_PIPE_LISTENING) {
        // Give it some time to connect.
        Sleep(100);
        std::cout << ".";
        result = ConnectNamedPipe(pipe, NULL);
    }

    // Should be able to attach now.
    std::cout << "Attaching should work now" << std::endl;

    while (1) {
        std::cout << "Enter the full path to a script to run:" << std::endl;
        std::string scriptName;
        std::cin >> scriptName;
        if (scriptName.size() > 0) {
            const char* message = scriptName.c_str();
            DWORD dwWritten = 0;
            result = WriteFile(pipe, message, strlen(message), &dwWritten, NULL);
            if (result == 0) {
                std::cout << "Error writing to pipe: " << GetLastError() << std::endl;
            }
            char buffer[1024] = {};
            result = ReadFile(pipe, buffer, 1024, &dwWritten, NULL);
            if (result == 0) {
                std::cout << "Error reading from pipe: " << GetLastError() << std::endl;
            }
        }
        else {
            break;
        }
    }


    // Wait for the thread to exit
    WaitForSingleObject(runnerThread, -1);

    CloseHandle(pipe);
}