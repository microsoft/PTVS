def func_with_bp():
    print('bp')
    print('ok')

def global_func():
    func_with_bp()
    print('ok')

global_func()
