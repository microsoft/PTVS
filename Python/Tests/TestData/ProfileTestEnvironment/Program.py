def user_env_var_valid():
    pass

import os
assert os.environ['USER_ENV_VAR'] == '123'
user_env_var_valid()
