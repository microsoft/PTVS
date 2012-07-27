import sys
import os
import time
from datetime import datetime
from sets import Set

import synctfs

class RollingRunHost:
    def __init__(self):
        self.inprocess_tasks = None
        self.share_server = self._get_share_server()
        self.need_to_build = True

        self._create_all_required_directories()

    def _create_all_required_directories(self):
        if not os.path.exists(r'\\' + self.share_server + r'\Build\ptvs'):
            os.mkdir(r'\\' + self.share_server + r'\Build\ptvs')
        if not os.path.exists(r'\\' + self.share_server + r'\RollingTasks\not_started'):
            os.mkdir(r'\\' + self.share_server + r'\RollingTasks\not_started')
        if not os.path.exists(r'\\' + self.share_server + r'\RollingTasks\inprocess'):
            os.mkdir(r'\\' + self.share_server + r'\RollingTasks\inprocess')
        if not os.path.exists(r'\\' + self.share_server + r'\RollingTasks\completed'):
            os.mkdir(r'\\' + self.share_server + r'\RollingTasks\completed')
        if not os.path.exists(r'\\' + self.share_server + r'\RollingTasks\scheduled'):
            os.mkdir(r'\\' + self.share_server + r'\RollingTasks\scheduled')

    def loop(self):
        while True:
            print('Starting loop.....')
            self.inprocess_tasks = self._get_inprocess_tasks()
            self._sync_tfs()
            self._build_ptvs_if_needed() 
            self._generate_tasks()  
            time.sleep(10) 

    def _get_share_server(self):
        with open('share_server.txt') as f:
            share_server = f.readline().strip()
        return share_server

    def _sync_tfs(self):
        self.need_to_build = synctfs.sync_tfs()

    def _no_copy_in_process(self):
        for task in self.inprocess_tasks:
            if 'copy_in_process' in os.listdir(task):
                return False
        return True

    def _build_ptvs(self):
        print('_build_ptvs')
        #create build_block file to indicate is about to build the ptvs
        #the client should not do any copy until the building completed.
        #one exception is the client is already in copying process, then 
        #this function will wait until the copy is done. 
        lock_file = r'\\' + self.share_server + r'\build\build_lock'
        f = open(lock_file, 'w')
        f.close()
        while True:
            if self._no_copy_in_process():
                print('nocopy')
                os.system('rd /s /q \\' + self.share_server + r'\build\ptvs')
                os.system('buildptvs.bat')
                if self._all_binaries_exist():
                    os.system('del /q ' + lock_file)
                    os.system(r'copy build.log \\' + self.share_server + r'\build\build.log')
                else:
                    os.system(r'copy build.log \\' + self.share_server + r'\build\error.log')
                break
            else:
                time.sleep(5)

    def _build_ptvs_if_needed(self):
        print('_build_ptvs_if_needed')
        if self.need_to_build: 
            print('build')           
            self._build_ptvs()
        elif not self._all_binaries_exist():
            if not 'build_lock' in os.listdir(r'\\' + self.share_server + r'\build'):
                self._build_ptvs()

    def _all_binaries_exist(self):
        binaries_dir = r'\\' + self.share_server + r'\build\ptvs\release'
        if not os.path.exists(binaries_dir):
           return False
        binaries = Set(os.listdir(binaries_dir))
        required_msi = Set(['PythonToolsInstaller.msi', 'PyKinectInstaller.msi', 'PyvotInstaller.msi'])
        return required_msi.issubset(binaries)

    def _get_inprocess_tasks(self): 
        inprocess_tasks = []
        for task in os.listdir(r'\\' + self.share_server + r'\RollingTasks\inprocess'):
            if task.startswith('rollingtask'):
                inprocess_tasks.append(r'\\' + self.share_server + '\\RollingTasks\\inprocess\\' + task)
        return inprocess_tasks

    def _generate_tasks(self):
        scheduled_path = r'\\' + self.share_server + r'\RollingTasks\scheduled'
        for task_name in os.listdir(scheduled_path):
            with open(scheduled_path + '\\' + task_name + '\\task.config') as task_config:
                line = task_config.readline().strip()
                name, value = line.split('=')
                task_to_generate = task_name
                if name.strip().lower() == 'rolling' and value.strip().lower() == 'yes':
                    task_to_generate += '__' + datetime.now().strftime('%Y%m%d')
                if self._task_not_generated(task_to_generate):
                    not_started_path = r'\\' + self.share_server + r'\RollingTasks\not_started'
                    target_dir = not_started_path + "\\" + task_to_generate + "\\"
                    source_dir = scheduled_path + "\\" + task_name 
                    os.system('robocopy /s /e ' + source_dir + ' ' + target_dir)
    
    def _task_not_generated(self, task):
        path_to_check = ['not_started', 'inprocess', 'completed']
        for path in path_to_check:
            path = r'\\' + self.share_server + '\\RollingTasks\\' + path
            if task in os.listdir(path):
                return False
        return True

if __name__ == '__main__':
    RollingRunHost().loop()


    


    
