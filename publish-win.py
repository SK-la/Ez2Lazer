#!/usr/bin/env python3
"""Wrapper: forward to publish.py with --platform windows unless overridden.

If called with explicit platform/os flags, forward as-is. If called without any
arguments, default to --platform windows.
"""
import os
import sys
from subprocess import run

def main():
    args = sys.argv[1:]
    # If no args provided, default to platform=windows. If any args are provided,
    # forward them as-is and do not apply filename shortcut.
    if len(args) == 0:
        cmd = [sys.executable, os.path.join(os.path.dirname(__file__), 'publish.py'), '--platform', 'windows']
    else:
        cmd = [sys.executable, os.path.join(os.path.dirname(__file__), 'publish.py')] + args
    return run(cmd).returncode

if __name__ == '__main__':
    sys.exit(main())
