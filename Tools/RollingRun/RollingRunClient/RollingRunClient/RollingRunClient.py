
import os
import time

class RollingTask:
    def __init__(self):
        self.status = None
        self.path = None
        
class RollingRunClient:
    def __init__(self):
        self.share_server = self._get_share_server()
        self.machine_name = self._get_machine_name()
        self.current_task = None

    def _get_share_server(self):
        with open('share_server.txt') as f:
            share_server = f.readline().strip()
        return share_server

    def _get_machine_name(self):
        with open('machine_name.txt') as f:
            machine_name = f.readline().strip()
        return machine_name

    def loop(self):
        while True:
            self.current_task = self._get_task()
            if self.current_task: 
                self._process_task()
            else:
                time.sleep(10)

    def _get_task(self):
        print('get task')
        not_started_dir = r'\\' + self.share_server + r'\RollingTasks\not_started'
        tasks = os.listdir(not_started_dir)
        for task in tasks:
            if task.startswith(self.machine_name):
                current_task = RollingTask()
                current_task.status = 0
                current_task.path = not_started_dir + '\\' + task
                return current_task

    def _process_task(self):
        if self._copy_binaries():
            print('in process')
            inprocess_path = self.current_task.path.replace('not_started', 'inprocess')
            os.system('move /y ' + self.current_task.path + ' ' + inprocess_path)
            self.current_task.status = 1
            self.current_task.path = inprocess_path
            self._install_ptvs()
            self._run_test()
            self._update_result()

    def _copy_binaries(self):
        print('copy binaries')
        build_block = r'\\' + self.share_server + '\\Build\\build_lock'
        if os.path.exists(build_block):
            return False

        copy_in_progress_path = self.current_task.path + '\\copy_in_progress'
        f = open(copy_in_progress_path, 'w')
        f.close()
        os.system('rd /s /q ptvs')
        os.system(r'robocopy/s /e /y \\' + self.share_server + '\\Build\\ptvs\\* .\\ptvs\\')
        os.system('del /q ' + copy_in_progress_path)
        return True

    def _install_ptvs(self):
        print('install ptvs')
        current_dir = os.getcwd()
        os.chdir('.\\ptvs')
        os.system('msiexec /passive /i PythonToolsIntaller.msi')
        os.chdir(current_dir)
        print(os.getcwd())

    def _run_test(self):
        print('run tests')
        current_dir = os.getcwd()
        os.chdir('ptvs\\release\\binaries')
        os.system('copy /y ' + self.current_task.path + '\\*')
        os.system('run.bat > ' + self.current_task.path + '\\rollingrun.log')
        os.chdir(current_dir)

    def _update_result(self):
        print('update result')
        os.system('robocopy/s /e /y .\\ptvs\\release\\binaries\\TestResults\\* ' + self.current_task.path + '\\TestResults\\')
        os.system('rd /s /q .\\ptvs\\release\\binaries\\TestResults')
        completed_path = self.current_task.path.replace('inprocess', 'completed')
        os.system('move /y ' + self.current_task.path + ' ' + completed_path)


if __name__ == '__main__':
    RollingRunClient().loop()





   

