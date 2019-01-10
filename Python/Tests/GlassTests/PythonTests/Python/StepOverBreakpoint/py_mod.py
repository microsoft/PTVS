def global_func():
    def inner_func():
        pass
    inner_func()
    print('ok')

global_func()
