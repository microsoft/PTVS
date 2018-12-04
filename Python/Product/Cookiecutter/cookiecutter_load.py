# Python Tools for Visual Studio
# Copyright(c) Microsoft Corporation
# All rights reserved.
# 
# Licensed under the Apache License, Version 2.0 (the License); you may not use
# this file except in compliance with the License. You may obtain a copy of the
# License at http://www.apache.org/licenses/LICENSE-2.0
# 
# THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
# IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
# MERCHANTABILITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

import json
import os.path
import sys

from cookiecutter.config import get_user_config
from cookiecutter.generate import generate_context

def main():
    repo_dir = sys.argv[1]
    user_config_path = sys.argv[2]
    user_config_path = user_config_path if os.path.isfile(user_config_path) else None
    context_file = os.path.join(repo_dir, 'cookiecutter.json')
    config_dict = get_user_config(user_config_path)
    context = generate_context(context_file, config_dict['default_context'])
    print(json.dumps(context))

if __name__ == "__main__":
    sys.exit(int(main() or 0))
