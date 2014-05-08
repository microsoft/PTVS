To run this project, we recommended creating a virtual environment and 
installing the dependent packages in that virtual environment.
A requirements.txt listing the dependent packages is included in the project.

To create a virtual environment:
1. Open Solution Explorer.
2. Right-click on Python Environments and select Add Virtual Environment.
3. Choose a base interpreter (we recommend Python 2.7 or Python 3.4).

To install the dependent packages:
1. Open Solution Explorer.
2. Right-click on requirements.txt and select Copy Full Path.
3. Right-click on the virtual environment and select Install Python Package.
4. In the text box, type the following:
     -r "<press CTRL-V to paste full path to requirements.txt>"

This project has default settings for the database (it uses sqlite). You'll
need to create the database before running the project.

To create the database:
1. Open Solution Explorer.
2. Right-click the project node and select Python->Django Sync DB.
3. You will be prompted to create a superuser, enter yes.
4. Enter a user name, email and password when prompted.
