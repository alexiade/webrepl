#!/usr/bin/env python
from __future__ import print_function
import sys
import os
import struct
import time

try:
    import usocket as socket
except ImportError:
    import socket

import local_commands as lcmd
import remote_commands as rcmd

USE_BUILTIN_UWEBSOCKET = 0
DEBUG = 0

WEBREPL_FRAME_TXT = 0x81
WEBREPL_FRAME_BIN = 0x82


def debugmsg(msg):
    if DEBUG:
        print(msg)


if USE_BUILTIN_UWEBSOCKET:
    from uwebsocket import websocket
else:
    class websocket:
        def __init__(self, s):
            self.s = s
            self.buf = b""

        def write(self, data, frame=WEBREPL_FRAME_BIN):
            l = len(data)
            if l < 126:
                hdr = struct.pack(">BB", frame, l)
            else:
                hdr = struct.pack(">BBH", frame, 126, l)
            self.s.send(hdr)
            self.s.send(data)

        def recvexactly(self, sz):
            res = b""
            while sz:
                data = self.s.recv(sz)
                if not data:
                    break
                res += data
                sz -= len(data)
            return res

        def read(self, size, text_ok=False):
            if not self.buf:
                while True:
                    hdr = self.recvexactly(2)
                    assert len(hdr) == 2
                    fl, sz = struct.unpack(">BB", hdr)
                    if sz == 126:
                        hdr = self.recvexactly(2)
                        assert len(hdr) == 2
                        (sz,) = struct.unpack(">H", hdr)
                    if fl == 0x82:
                        break
                    if text_ok and fl == 0x81:
                        break
                    debugmsg("Got unexpected websocket record of type %x, skipping it" % fl)
                    while sz:
                        skip = self.s.recv(sz)
                        debugmsg("Skip data: %s" % skip)
                        sz -= len(skip)
                data = self.recvexactly(sz)
                assert len(data) == sz
                self.buf = data
            d = self.buf[:size]
            self.buf = self.buf[size:]
            assert len(d) == size, len(d)
            return d

        def ioctl(self, req, val):
            assert req == 9 and val == 2


def login(ws, passwd):
    while True:
        c = ws.read(1, text_ok=True)
        if c == b":":
            assert ws.read(1, text_ok=True) == b" "
            break
    ws.write(passwd.encode("utf-8") + b"\r")


def get_ver(ws):
    rcmd.send_req(ws, rcmd.WEBREPL_GET_VER)
    d = ws.read(3)
    d = struct.unpack("<BBB", d)
    return d


def cmdloop(ws):
    """Interactive FTP-ish shell"""
    localcwd = os.getcwd()

    try:
        remotecwd = rcmd.remote_pwd(ws)
    except Exception as e:
        print(f"Warning: Could not get remote directory: {e}")
        remotecwd = "/"

    while True:
        try:
            cmd = input(f"cmd [local:{localcwd} remote:{remotecwd}]: ").strip()
        except EOFError:
            break
        if not cmd:
            continue
        parts = cmd.split()
        if not parts:
            continue
        c = parts[0].lower()
        args = parts[1:]

        if c in ("exit", "quit"):
            break
        elif c == "lls":
            lcmd.list_local_files(localcwd)
        elif c == "lcd":
            if not args:
                print(localcwd)
            else:
                try:
                    localcwd = lcmd.change_local_dir(localcwd, args[0])
                except Exception as e:
                    print(f"lcd: {e}")
        elif c == "lpwd":
            print(lcmd.get_local_dir(localcwd))
        elif c == "ls":
            try:
                files = rcmd.remote_ls(ws, remotecwd)
                rcmd.print_remote_ls(files)
            except Exception as e:
                print(f"ls: {e}")
        elif c == "cd":
            if not args:
                print(remotecwd)
            else:
                try:
                    rcmd.remote_cd(ws, args[0])
                    remotecwd = rcmd.remote_pwd(ws)
                except Exception as e:
                    print(f"cd: {e}")
        elif c == "pwd":
            try:
                remotecwd = rcmd.remote_pwd(ws)
                print(remotecwd)
            except Exception as e:
                print(f"pwd: {e}")
        elif c == "get":
            if not args:
                print("get <remote_file> [local_file]")
            else:
                remote_file = args[0]
                local_file = args[1] if len(args) > 1 else lcmd.get_basename(remote_file)
                try:
                    rcmd.get_file(ws, local_file, remote_file)
                    print("Downloaded", remote_file, "to", local_file)
                except Exception as e:
                    print(f"get: {e}")
        elif c == "getall":
            if not args:
                local_dir = os.path.basename(remotecwd) or "download"
            else:
                local_dir = args[0]
            try:
                print(f"Downloading all files from {remotecwd} to {local_dir}")
                rcmd.remote_getall(ws, remotecwd, local_dir)
            except Exception as e:
                print(f"getall: {e}")
        elif c == "put":
            if not args:
                print("put <local_file> [remote_file]")
            else:
                local_file = args[0]
                remote_file = args[1] if len(args) > 1 else lcmd.get_basename(local_file)
                try:
                    rcmd.put_file(ws, local_file, remote_file)
                    print("Uploaded", local_file, "to", remote_file)
                except Exception as e:
                    print(f"put: {e}")
        elif c == "del" or c == "rm":
            if not args:
                print("del <remote_file>")
            else:
                remote_file = args[0]
                try:
                    rcmd.remote_del(ws, remote_file)
                    print(f"Deleted {remote_file}")
                except Exception as e:
                    print(f"del: {e}")
        elif c == "reset" or c == "reboot":
            try:
                rcmd.remote_reset(ws, hard=True)
                break
            except Exception as e:
                print(f"reset: {e}")
        elif c == "restart":
            try:
                rcmd.remote_restart(ws)
                break
            except Exception as e:
                print(f"restart: {e}")
        elif c == "interrupt" or c == "break":
            try:
                rcmd.interrupt_running_code(ws)
                print("Interrupt sent.")
                remotecwd = rcmd.remote_pwd(ws)
            except Exception as e:
                print(f"interrupt: {e}")
        elif c == "help":
            print("Available commands:")
            print("  ls                # List remote directory")
            print("  cd <dir>          # Change remote directory")
            print("  pwd               # Print remote directory")
            print("  get rfile [lfile] # Download remote file")
            print("  getall [ldir]     # Recursively download all files from remote pwd")
            print("  put lfile [rfile] # Upload file")
            print("  del, rm <rfile>   # Delete remote file")
            print("  lls               # List local directory")
            print("  lcd <dir>         # Change local directory")
            print("  lpwd              # Print local directory")
            print("  reset, reboot     # Hard reset MicroPython board")
            print("  restart           # Restart worker (custom)")
            print("  interrupt, break  # Send Ctrl+C to break running code")
            print("  exit, quit        # Exit")
            print("  help              # This help message")
        else:
            print("Unknown command (try 'help')")


def help(rc=0):
    print("webrepl_ftp.py - FTP-like client for MicroPython WebREPL")
    print("Usage: webrepl_ftp.py [-p password] [-l local_dir] [-r remote_dir] <host>[:port]")
    print("Options:")
    print("  -p password    Specify password (otherwise prompted)")
    print("  -l local_dir   Set initial local working directory")
    print("  -r remote_dir  Set initial remote working directory")
    print("  -h, --help     Show this help")
    sys.exit(rc)


def error(msg):
    print(msg)
    sys.exit(1)


def parse_remote(remote):
    host, fname = remote.rsplit(":", 1)
    if fname == "":
        fname = "/"
    port = 8266
    if ":" in host:
        host, port = host.split(":")
        port = int(port)
    return (host, port, fname)


def client_handshake(sock):
    cl = sock.makefile("rwb", 0)
    cl.write(b"""\
GET / HTTP/1.1\r
Host: echo.websocket.org\r
Connection: Upgrade\r
Upgrade: websocket\r
Sec-WebSocket-Key: foo\r
\r
""")
    l = cl.readline()
    while 1:
        l = cl.readline()
        if l == b"\r\n":
            break


def main():
    # Parse command-line arguments
    passwd = None
    local_dir = None
    remote_dir = None

    # Parse flags
    i = 1
    while i < len(sys.argv):
        if sys.argv[i] == '-p' and i + 1 < len(sys.argv):
            passwd = sys.argv[i + 1]
            sys.argv.pop(i)
            sys.argv.pop(i)
        elif sys.argv[i] == '-l' and i + 1 < len(sys.argv):
            local_dir = sys.argv[i + 1]
            sys.argv.pop(i)
            sys.argv.pop(i)
        elif sys.argv[i] == '-r' and i + 1 < len(sys.argv):
            remote_dir = sys.argv[i + 1]
            sys.argv.pop(i)
            sys.argv.pop(i)
        else:
            i += 1

    # Check for help
    if len(sys.argv) == 2 and sys.argv[1] in ("-h", "--help", "help"):
        help(0)

    # Minimal args: host [:port]
    if len(sys.argv) < 2:
        print("Usage: %s [-p password] [-l local_dir] [-r remote_dir] <host>[:port]" % sys.argv[0])
        sys.exit(1)

    # Change local directory if specified
    if local_dir:
        try:
            os.chdir(local_dir)
            print(f"Changed local directory to: {os.getcwd()}")
        except Exception as e:
            print(f"Error: Could not change to local directory '{local_dir}': {e}")
            sys.exit(1)

    hoststr = sys.argv[1]
    port = 8266
    if ":" in hoststr:
        host, port = hoststr.split(":")
        port = int(port)
    else:
        host = hoststr

    # Get password if not provided via -p
    if passwd is None:
        import getpass
        try:
            passwd = getpass.getpass("Password: ")
        except Exception as e:
            # Handle non-TTY environments or getpass errors
            print(f"Error: Cannot prompt for password ({e})")
            print("Please use: -p <password>")
            sys.exit(1)

    print(f"Connecting to {host}:{port} ...")
    s = socket.socket()
    ai = socket.getaddrinfo(host, port)
    addr = ai[0][4]
    s.connect(addr)
    client_handshake(s)
    ws = websocket(s)
    login(ws, passwd)
    print("Remote WebREPL version:", get_ver(ws))
    ws.ioctl(9, 2)

    # Interrupt any running code before starting
    rcmd.interrupt_running_code(ws)

    # Change remote directory if specified
    if remote_dir:
        try:
            rcmd.remote_cd(ws, remote_dir)
            actual_dir = rcmd.remote_pwd(ws)
            print(f"Changed remote directory to: {actual_dir}")
        except Exception as e:
            print(f"Warning: Could not change to remote directory '{remote_dir}': {e}")

    cmdloop(ws)
    s.close()


if __name__ == "__main__":
    main()