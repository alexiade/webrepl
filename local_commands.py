#!/usr/bin/env python
"""Local file system operations for WebREPL FTP client"""
import os


def list_local_files(cwd):
    """List files in local directory"""
    files = os.listdir(cwd)
    for f in files:
        print(f)


def change_local_dir(cwd, path):
    """Change local directory, returns new cwd or raises exception"""
    if not path:
        return cwd
    os.chdir(path)
    return os.getcwd()


def get_local_dir(cwd):
    """Get current local directory"""
    return cwd


def get_basename(filepath):
    """Get basename of a file path"""
    return os.path.basename(filepath)
