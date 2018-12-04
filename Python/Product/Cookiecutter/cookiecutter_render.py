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

from collections import OrderedDict

from cookiecutter.config import get_user_config
from cookiecutter.environment import StrictEnvironment
from cookiecutter.generate import generate_context
from cookiecutter.prompt import render_variable, prompt_choice_for_config
from future.utils import iteritems
from jinja2.exceptions import UndefinedError


def render_obj(env, o, cookiecutter_dict):
    if isinstance(o, list):
        return render_list(env, o, cookiecutter_dict)
    elif isinstance(o, dict):
        return render_dict(env, o, cookiecutter_dict)
    else:
        return render_variable(env, o, cookiecutter_dict)

def render_list(env, l, cookiecutter_dict):
    return [
        render_obj(env, raw, cookiecutter_dict) for raw in l
    ]

def render_dict(env, d, cookiecutter_dict):
    return {
        k: render_obj(env, v, cookiecutter_dict) for k, v in iteritems(d)
    }

def render_context(context, output_folder_path):
    cookiecutter_dict = OrderedDict()

    # inject the output folder path at the beginning, so all variables can refer to it
    cookiecutter_dict['_output_folder_path'] = output_folder_path;

    env = StrictEnvironment(context=context)

    for key, raw in iteritems(context['cookiecutter']):
        if key.startswith('_'):
            # unlike cookiecutter's prompt_for_config, we render internal variables
            cookiecutter_dict[key] = render_obj(env, raw, cookiecutter_dict)
            continue

        try:
            if isinstance(raw, list):
                # We are dealing with a choice variable
                val = prompt_choice_for_config(
                    cookiecutter_dict, env, key, raw, no_input=True
                )
            else:
                # We are dealing with a regular variable
                val = render_variable(env, raw, cookiecutter_dict)
        except UndefinedError as err:
            msg = "Unable to render variable '{}'".format(key)
            raise UndefinedVariableInTemplate(msg, err, context)

        cookiecutter_dict[key] = val

    return { 'cookiecutter' : cookiecutter_dict }

def main():
    repo_dir = sys.argv[1]
    user_config_path = sys.argv[2]
    user_config_path = user_config_path if os.path.isfile(user_config_path) else None
    output_folder_path = sys.argv[3]
    context_json_path = sys.argv[4]

    extra_context = json.load(open(context_json_path, 'r'))

    context_file = os.path.join(repo_dir, 'cookiecutter.json')
    config_dict = get_user_config(user_config_path)
    context = generate_context(context_file, config_dict['default_context'], extra_context=extra_context)

    rendered_context = render_context(context, output_folder_path)
    print(json.dumps(rendered_context))

if __name__ == "__main__":
    sys.exit(int(main() or 0))
