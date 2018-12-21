def global_func():
    def inner_func():
        pass
    print('ok')
    inner_func()
    print('ok')

global_func()
