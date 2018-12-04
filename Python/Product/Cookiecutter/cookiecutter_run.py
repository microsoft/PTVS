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
import logging
import os.path
import sys

from cookiecutter.main import cookiecutter

def main():
    context_json_path = sys.argv[1]
    template_path = sys.argv[2]
    output_path = sys.argv[3]
    user_config_path = sys.argv[4]
    user_config_path = user_config_path if os.path.isfile(user_config_path) else None
    context = json.load(open(context_json_path, 'r'))

    logging.basicConfig(
        format=u'%(levelname)s %(filename)s: %(message)s',
        level=logging.DEBUG
    )

    cookiecutter(
        template_path,
        no_input=True,
        extra_context=context,
        output_dir=output_path,
        config_file=user_config_path,
    )

if __name__ == "__main__":
    sys.exit(int(main() or 0))
