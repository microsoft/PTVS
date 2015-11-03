#!/usr/bin/env python
import os
import sys

if __name__ == "__main__":
    os.environ.setdefault("DJANGO_SETTINGS_MODULE", "settings")
    sys.path.append(os.path.split(os.path.split(os.path.abspath(__file__))[0])[0])

    from django.core.management import execute_from_command_line

    execute_from_command_line(sys.argv)
