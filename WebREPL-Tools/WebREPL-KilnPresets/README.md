# WebREPL Kiln Preset Manager

A WPF application for managing fire presets for kiln controller software via WebREPL.

## Features

- **Dual-Pane Interface**: Local library on the left, remote device files on the right
- **Category-Based Organization**: Presets are organized in folders by category
- **Preset Editor**: Edit fire instructions with easy hours/minutes/seconds duration input
- **Drag & Drop**: Move presets between categories
- **One-Click Push**: Send presets directly to connected device
- **Shared Configuration**: Uses the same host configuration as WebREPL Commander

## Fire Instruction Types

- **H (Heat)**: Heat to target temperature (no duration)
- **R (Ramp Up)**: Gradual temperature increase over specified duration to target temp
- **D (Drop)**: Freefall cool to target temperature (no duration)
- **S (Soak)**: Hold previous target temperature for specified duration
- **C (Cool)**: Controlled cool down (down ramp) over specified duration to target temp

## Local Library

The application creates a `FirePresetLibrary` folder in your Documents directory. Presets are stored as JSON files organized by category folders.

Example structure:

```
Documents/
  FirePresetLibrary/
    ceramic/
      jar_ceramic_cast.json
      bowl_earthenware.json
    glass/
      fusing_standard.json
```

## JSON Format

Each preset file contains:

- `Key`: The file name (without .json extension)
- `Category`: The category/folder name
- `Name`: Human-readable description
- `Phases`: Array of fire instructions (phases)

Example:

```json
{
  "Key": "jar_ceramic_cast",
  "Category": "ceramic",
  "Name": "Standard ceramic jar cast firing schedule",
  "Phases": [
    {
      "Type": "H",
      "Target": 100
    },
    {
      "Type": "R",
      "Duration": 7200,
      "Target": 600
    }
  ]
}
```

## Host Configuration

Host configurations are shared with WebREPL Commander and stored in:
`~/.webrepl-commander/hosts.json`

## Usage

1. Launch the application
2. Use **Device > Manage Hosts** to add your kiln controller connection
3. Use **Device > Connect** to connect to your device
4. The local library will show your saved presets
5. The remote side will show files in the `/presets` folder on your device
6. Double-click any preset to edit it
7. Drag presets to different categories to reorganize
8. Use the preset editor to add, modify, or delete fire instructions

## Requirements

- .NET 10.0
- WebREPL-enabled device (ESP8266/ESP32)
