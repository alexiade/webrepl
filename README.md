# WebREPL Tools Collection

A comprehensive collection of tools for interacting with MicroPython devices via WebREPL protocol, featuring both the original web-based client and enhanced Python and C# implementations.

## Overview

This repository contains multiple implementations and tools for working with MicroPython's WebREPL protocol:

- **HTML/JavaScript Client** - Original browser-based WebREPL terminal
- **Python CLI Tools** - Command-line utilities for file transfer and device management
- **C# .NET Port** - Modern async implementation with library and FTP-like interface
- **Kiln Preset Manager** - Specialized tool for ceramic kiln controler im building for myself (MicroPython-based) This part is unlikely to be of use to anyone else, but it is the reason why I did the rest.

## Origins

This repository is derived from and extends the official MicroPython WebREPL client:

### Original Authors
- **Damien P. George** - MicroPython creator and WebREPL protocol designer (2016)
- **Paul Sokolovsky** - WebREPL implementation contributor (2016)
- **Jim Mussared** - WebREPL maintenance and updates (2022)

### Current Development
- **Alexia Liiv** - Python CLI tools, C# port, and kiln control extensions (2026)

### Source
The original WebREPL client (`htmljs/` folder) comes from the official MicroPython project:
- Repository: https://github.com/micropython/webrepl
- Website: http://micropython.org/webrepl

## What's in This Repository

### 1. Web-Based Client (`htmljs/`)
The original browser-based WebREPL terminal for accessing MicroPython devices. Open `webrepl.html` in a modern browser to get an interactive REPL over WebSockets.

**Features:**
- Terminal emulation with xterm.js
- WebSocket connection to MicroPython devices
- Basic file transfer capabilities
- Works locally without a web server

### 2. Python CLI Tools (`python/`)
Enhanced command-line utilities that extend the original WebREPL functionality.

**`webrepl_cli.py`** - Simple file transfer utility
```bash
python webrepl_cli.py -p password 192.168.4.1:local.py:remote.py
```

**`webrepl_ftp.py`** - Interactive FTP-like shell
```bash
python webrepl_ftp.py -p password 192.168.4.1
```

Commands: `ls`, `cd`, `get`, `put`, `rm`, `mkdir`, `cat`, `repl`, `exec`, and more.

### 3. C# .NET Implementation (`WebREPL-Tools/`)
A complete rewrite in modern C# with async/await patterns, providing:

- **WebREPL.Core** - Reusable library for integrating WebREPL into .NET applications
- **WebREPL-FTP** - Feature-complete FTP-like console application
- **WebREPL-Commander** - Enhanced command interface with additional features
- **WebREPL-KilnPresets** - Specialized tool for managing ceramic kiln firing schedules

**Advantages over Python:**
- Type safety with compile-time checking
- Better performance for I/O operations
- Native async/await support
- Easy integration into .NET applications
- Single-file executable deployment
- Full IDE support with IntelliSense

### 4. Kiln Control Data (`mess/`)
JSON configuration files for ceramic kiln firing schedules. These presets define temperature curves for glass fusing and casting operations, intended to be uploaded to MicroPython-controlled kilns.

## Use Cases

### Development & Testing
- Upload code to ESP32/ESP8266 devices
- Test MicroPython scripts remotely
- Interactive debugging via REPL

### Device Management
- File system navigation and management
- Batch file operations across multiple devices
- Remote configuration updates

### Industrial Applications
- Ceramic kiln control and monitoring
- Temperature profile management
- Automated firing schedule deployment

### Automation & CI/CD
- Integrate device operations into build pipelines
- Automated testing on physical hardware
- Batch firmware updates

## Getting Started

### Prerequisites
- MicroPython device with WebREPL enabled
- Network connectivity to the device (WiFi)
- WebREPL password configured on the device

### Quick Start - Web Client
1. Open `htmljs/webrepl.html` in a browser
2. Enter device IP (e.g., `ws://192.168.4.1:8266`)
3. Connect and enter password

### Quick Start - Python
```bash
cd python
python webrepl_ftp.py -p your_password 192.168.4.1
```

### Quick Start - C#
```bash
cd WebREPL-Tools/WebREPL-FTP
dotnet run -- -p your_password 192.168.4.1
```

## Requirements

### For Web Client
- Modern browser (Firefox, Chrome, Chromium)
- Local file access (must be opened as local file, not HTTPS)

### For Python Tools
- Python 3.6 or later
- Standard library only (no external dependencies)

### For C# Tools
- .NET 10.0 SDK or later
- Windows, Linux, or macOS

## License

MIT License - See [LICENSE](LICENSE) file for details.

This project incorporates code from the MicroPython WebREPL project, which is also licensed under the MIT License.

## Documentation

- **Web Client:** See `htmljs/README.md`
- **C# Tools:** See `WebREPL-Tools/PROJECT_OVERVIEW.md`
- **Python Tools:** Run with `--help` flag for usage information

## Contributing

This is a personal toolkit repository. The original WebREPL CLI client should be contributed to upstream MicroPython project. Enhancements to Python FTP and C# tools are welcome.

If you want to grab and run with the core library alone, have fun. It's absolutely not perfect yet. It sometimes hangs missing the output on the first commands, but once it gets going, it works. Ulimately, it will be used in the killn preset manager UI so when the hangs annoy me enough, I will figure it out. This same issue exists in the python variant too.

## Related Projects

- [MicroPython](https://micropython.org/) - Python for microcontrollers
- [MicroPython WebREPL](https://github.com/micropython/webrepl) - Official WebREPL client
- [ESP32 MicroPython](https://docs.micropython.org/en/latest/esp32/quickref.html) - MicroPython on ESP32

## Acknowledgments

Special thanks to the MicroPython team for creating and maintaining the WebREPL protocol and reference implementation.
