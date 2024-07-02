This folder has a C++ application that runs Python code.

It starts a 'runner.py' file in a separate thread and then uses named pipes to send it python scripts to run. Yes it's not very secure, but it's just an example :). You'd likely have to make the named pipe random (like a guid) and send that guid to the runner on the command line.

It was created to test attaching debugpy to python code running in a C++ app and to test mixed mode debugging.