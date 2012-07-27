This for running rolling task. You need to start RollingRunHost, RollingTaskController and RollingRunClient to make the system to work.

The whole process:

1. Setup RunHost machine
2. Start RunTaskController
3. Start RunClient in different machines

RunHost: sync the source and build the binaries and generate the task runs base on the scheduled tasks.
RunTaskController: manages the tasks (add, change and delete tasks)
RunClient: get task and run the task


prerequisites
1. You need to have a share server to store everything, for example: wtestserver. 
2. create 2 folders in the share server named: Build, RollingTasks.  After this, you should be able to access \\wtestserver\Build and \\wtestserver\RollingTasks
3. open share_server.txt in RollingRunHost, RollingTaskController and RollingRunClient and change the content to the server name (wtestserver)
4. open machines.txt in RollingTaskController and add all the client machines that are used to run the tasks in this format:
    machine_name=machine_description  Example: winx64vs2010=windows 7(x64), VS 2010 Ultimate
5. Open machine_name.txt in RollingRunClient and replace it with the client machine name.  This one will be different for different client machine.
6. Runhost machines must be setup properly to build ptvs (all the required components must be installed first)
7. RunTaskController and Runclient machine must have python installed and have it in the path. 


To start runhost:
1. Open visual stduio command prompt, make sure you have python.exe in your path and you can run powershell script from the prompt.
2. Go to rollingrunhost and type "python.exe RollingRunHost.py"

To Start runtaskcontroller:
1. open visual stduio command prompt
2. go to rollingtaskcontroller and type "python.exe manage.py runserver [servername:port]"

To start runclient:
1. Open visual stduio command prompt as administrator
2. go to rollingrunclient and type "python.exe rollingrunclient.py"


