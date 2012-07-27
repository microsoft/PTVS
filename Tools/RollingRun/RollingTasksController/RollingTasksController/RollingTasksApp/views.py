import os
from xml.dom import minidom
from datetime import datetime
from django.http import HttpResponse
from django.template.loader import render_to_string, get_template 
from django.template import Context

def _get_share_server():
    return open('share_server.txt').readline().strip()

def _get_machines():
    machines = {}
    machine_file = open('machines.txt')
    while True:
        line = machine_file.readline().strip()
        if '=' in line:
            name, description = line.split('=')
            machines[name] = description
        else:
            break
    machine_file.close()
    return machines

share_server = _get_share_server()
scheduled_path = r'\\' + share_server + r'\RollingTasks\scheduled'
not_started_path = r'\\' + share_server + r'\RollingTasks\not_started'
inprocess_path = r'\\' + share_server + r'\RollingTasks\inprocess'
completed_path = r'\\' + share_server + r'\RollingTasks\completed'

class Task:
    def __init__(self, name):
        self.name = name
        self.rolling = 'Yes'
        self.create_date = ''
        self.results = ''
        self.status = ''
        self.schedule = 24
        self.machine_description = ''

def _get_results(task_name):
    not_started_path = r'\\' + share_server + r'\RollingTasks\not_started'
    inprocess_path = r'\\' + share_server + r'\RollingTasks\inprocess'
    completed_path = r'\\' + share_server + r'\RollingTasks\completed'

    if task_name in os.listdir(not_started_path):
        return 'Waiting for execution', ''
    elif task_name in os.listdir(inprocess_path):
        return 'In Process', ''
    elif task_name in os.listdir(completed_path):
        for name in os.listdir(completed_path + '\\TestResults'):
            if name.endswith('.trx'):
                return 'Completed', completed_path + '\\TestResults\\' + name
    else:
        return 'Waiting for scheduling', ''

def _get_result_and_status(task_name):
    status = 'Not scheduled'
    test_result = ''
    if task_name in os.listdir(not_started_path):
        status = 'Waiting for execution'
    elif task_name in os.listdir(inprocess_path):
        status = 'In Processing'
    elif task_name in os.listdir(completed_path):
        status = 'Completed'
        for result in os.listdir(completed_path + '\\' + task_name + '\\TestResults'):
            if result.endswith('.trx'):
                test_result = completed_path + '\\' + task_name + '\\TestResults'
                break
    return status, test_result


def _get_task(scheduled_path, task_name):
    with open(scheduled_path + '\\' + task_name + '\\task.config') as task_config:
        task = Task(task_name)
        while True:
            line = task_config.readline().strip()
            if '=' in line:
                name, value = line.split('=')
                if name.lower() in ['rolling', 'create_date']:
                    setattr(task, name, value)
            else:
                break

    if task.rolling.lower() == 'yes':
        task.results = 'view_results?task_name=' + task.name
        task.status = 'N/A'
    else:
        task.status, task.results = _get_result_and_status(task.name)
    machine_name = task.name.split('__')[0]
    task.machine_description = _get_machines()[machine_name]
    return task

def list_tasks(request):
    task_names = os.listdir(scheduled_path)
    tasks = []
    for task_name in task_names:
        tasks.append(_get_task(scheduled_path, task_name))
    return HttpResponse(render_to_string('listtasks.html', Context({'tasks': tasks})))

class Testcase:
    def __init__(self, name=None, path=None, id=None, parent_id=None, selected=None):
        self.name = name
        self.path = path
        self.selected = selected
        self.id = id
        self.parent_id = parent_id

def _get_testcases(testlist_file, exclude_no_testlink=True):
    id_name_map = {}
    id_parent_map = {}
    testcases = []
    xmldoc = minidom.parse(testlist_file)
    xml_test_lists = xmldoc.getElementsByTagName('TestLists')[0]
    test_lists = xml_test_lists.getElementsByTagName('TestList')
    for test_list in test_lists:
        name = test_list.getAttribute('name')
        id = test_list.getAttribute('id')
        id_name_map[id] = name
        parent_id = test_list.getAttribute('parentListId')
        if parent_id:
            id_parent_map[id] = parent_id

    for test_list in test_lists:
        name = test_list.getAttribute('name')
        id = test_list.getAttribute('id')
        parent_id = test_list.getAttribute('parentListId')
        path = name
        curr_parent_id = parent_id
        while curr_parent_id:
            if id_parent_map.has_key(curr_parent_id):
                path = id_name_map[curr_parent_id] + '/' + path
                curr_parent_id = id_parent_map[curr_parent_id]
            else:
                break
        if exclude_no_testlink and not test_list.getElementsByTagName('TestLink'):
            continue
        testcases.append(Testcase(name, path, id, parent_id, selected='No'))

    testcases.sort(key=lambda x: x.name.lower())

    return testcases

def add_task(request):
    testcases = _get_testcases('PythonTools.vsmdi')
    t = get_template('task.html')
    return HttpResponse(t.render(Context({'task':None, 'testcases':testcases, 'machines':_get_machines().iteritems()})))

def view_results(request):
    task_name = request.GET['task_name']
    run_results = []
    run_results += _get_results(task_name, not_started_path, 'Waiting for execution')
    run_results += _get_results(task_name, inprocess_path, 'In Processing')
    run_results += _get_results(task_name, completed_path, 'Completed')
    t = get_template('results.html')
    
    return HttpResponse(t.render(Context({'run_results': run_results})))

class RunResult:
    def __init__(self):
        self.name = None
        self.status = None
        self.result = ''

def _get_results(task_name, path, status):
    run_results = []
    for run in os.listdir(path):
        if run.startswith(task_name + '__'):
            run_result = RunResult()
            run_result.name = run
            run_result.status = status
            testresult_path = os.path.join(path, run, 'TestResults')

            if os.path.exists(testresult_path):
                for result in os.listdir(testresult_path):
                    if result.endswith('.trx'):
                        run_result.result = testresult_path
                        break

            run_results.append(run_result)

    return run_results

def update_task(request):    
    task_name = request.GET['task_name']
    task = _get_task(scheduled_path, task_name)    
    testcases = _get_task_testcases(task_name)
    t = get_template('task.html')
    return HttpResponse(t.render(Context({'task':task, 'testcases':testcases, 'machines':_get_machines().iteritems()})))

def _get_task_testcases(task_name):
    task_path = scheduled_path + '\\' + task_name
    testcases = _get_testcases('PythonTools.vsmdi')
    task_testcases = _get_testcases(task_path + '\\test.vsmdi')

    task_testcase_paths = [task_testcase.path for task_testcase in task_testcases]
    
    for testcase in testcases:
        if testcase.path in task_testcase_paths:
            testcase.selected = 'yes'

    testcases.sort(key=lambda x: x.name.lower())

    return testcases

def delete_task(request):
    task_path = scheduled_path + '\\' + request.GET['task_name']
    os.system('rd /s /q ' + task_path)
    return list_tasks(request)

def _task_exists(task_name):
    return task_name in os.listdir(scheduled_path)

def save_task(request):
    task_name = request.GET['task_name']
    if not '__' in task_name:
        task_name = request.GET['machine_name'] + '__' + task_name
    if request.GET['action'].lower() == 'add' and _task_exists(task_name):
        return HttpResponse('<html><body><h2> Error! </h2> <br> The task has already exists. You need to give it a different Name.</body></html>')

    testcase_list = []
    parentids = []
    for name, value in request.GET.iteritems():
        if name not in ['task_name', 'rolling', 'interval']:
            testcase_list.append(name)

    _create_test_list(testcase_list)
    _create_task_config(request.GET['rolling'], request.GET['interval'])
    _create_run_script()
    _create_task_in_share_server(task_name)   
    return list_tasks(request)

def _create_test_list(testcase_list):
    testcases = _get_testcases('PythonTools.vsmdi', False)
    id_path_map = {}
    id_parent_map = {}
    for testcase in testcases:
        id_path_map[testcase.id] = testcase.path
        id_parent_map[testcase.id] = testcase.parent_id

    parent_ids = []
    xmldoc = minidom.parse('PythonTools.vsmdi')
    xml_test_lists = xmldoc.getElementsByTagName('TestLists')[0]
    test_lists = xml_test_lists.getElementsByTagName('TestList')
    for test_list in test_lists:
        id = test_list.getAttribute('id')
        path = id_path_map[id]
        if path in testcase_list:
            parent_id = test_list.getAttribute('parentListId')
            if parent_id not in parent_ids:
                parent_ids.append(parent_id)
    
    while True:
        pid_count = len(parent_ids)
        for parent_id in parent_ids:
            grandparent_id = id_parent_map[parent_id]
            if grandparent_id and grandparent_id not in parent_ids :
                parent_ids.append(grandparent_id)
        if len(parent_ids) == pid_count: 
            break

    for test_list in test_lists:
        id = test_list.getAttribute('id')
        parent_id = test_list.getAttribute('parentListId')
        path = id_path_map[id]
        if path not in testcase_list and id not in parent_ids:
            xml_test_lists.removeChild(test_list)
        else:
            continue

    test_list_file = open('test.vsmdi', 'w')
    test_list_file.write(xmldoc.toxml())
    test_list_file.close()

def _create_task_config(rolling, interval):
    with open('task.config', 'w') as task_config:
        task_config.write('rolling=' + rolling + '\n')
        task_config.write('interval=' + interval + '\n')
        task_config.write('create_date=' + datetime.now().strftime('%Y %b %d %H:%M:%S') + '\n')
    
def _create_run_script():
    run_script = open('run.bat', 'w')
    run_script.write('mstest.exe /testmetadata:test.vsmdi')
    run_script.close()

def _create_task_in_share_server(task_name):
    task_dir = scheduled_path + '\\' + task_name
    if not os.path.exists(task_dir):
        os.mkdir(task_dir)
    os.system('copy /y test.vsmdi ' + task_dir)
    os.system('copy /y task.config ' + task_dir)
    os.system('copy /y run.bat ' + task_dir)




