# WebREPL Kiln Presets - Quick Start Guide

## Overview

This application helps you manage fire presets for your kiln controller using WebREPL. It provides a graphical interface to create, edit, and transfer preset files between your computer and the kiln controller device.

## Initial Setup

1. **Launch the application**
2. **Configure your device connection:**
   - Go to **Device > Manage Hosts**
   - Click **Add**
   - Enter your device details:
     - Name: A friendly name for your kiln (e.g., "Studio Kiln")
     - Host: IP address of your device (e.g., 192.168.4.1)
     - Port: Usually 8266
     - Password: Your WebREPL password (if set)
   - Click **OK** to save

## Creating Your First Preset

1. Click **File > New Preset** or press the **New Preset** button
2. Fill in the preset details:
   - **Key**: Unique identifier (becomes the file name)
   - **Category**: Folder to organize your preset (e.g., "ceramic", "glass")
   - **Description**: What this preset is for
   - **Max Temperature**: Maximum temperature in Celsius

3. Add fire instructions:
   - **Heat (H)**: Quick heat to target temperature
   - **Ramp Up (R)**: Gradual increase to target temp over time
   - **Down Ramp (D)**: Gradual decrease to target temp over time
   - **Soak (S)**: Hold current temperature for duration
   - **Cool (C)**: Freefall cool to target temperature

4. For each instruction with duration:
   - Enter hours, minutes, and seconds separately
   - The system automatically converts to total seconds for storage

5. Use the arrow buttons (↑ ↓) to reorder instructions
6. Use the ✕ button to delete instructions
7. Click **Save** when done

## Working with Presets

### Local Library (Left Panel)

Your presets are stored in `Documents\FirePresetLibrary` organized by category folders.

**Actions:**
- **Double-click** a preset to edit it
- **Right-click** for context menu:
  - **Push to Device**: Send directly to connected device
  - **Edit**: Open the preset editor
  - **Delete**: Remove the preset

### Device Files (Right Panel)

Shows the `/presets` folder structure on your connected device.

**Actions:**
- **Double-click** a remote preset to view/edit it
- **Drag** from local library and **drop** on remote panel to upload

## Connecting to Your Device

1. Click **Device > Connect**
2. The application will connect to your most recently used host
3. Once connected, the status will show "Connected to [Device Name]"
4. The remote panel will automatically load the device's preset files

## Transferring Presets

### Upload to Device:
**Method 1:** Right-click a local preset → **Push to Device**
**Method 2:** Drag a preset from local panel and drop on the remote panel

The preset will be uploaded to the same category folder on the device.

### Download from Device:
**Method 1:** Double-click a remote preset to view/edit it

## Tips

- **Categories**: Keep your presets organized by material type (ceramic, glass, metal, etc.)
- **Naming**: Use descriptive key names (e.g., "jar_ceramic_cast", "bowl_earthenware")
- **Backup**: Your local library in Documents/FirePresetLibrary serves as a backup
- **Share Configuration**: This app shares host settings with WebREPL Commander
- **Folders**: Click **Open Folder** to browse your library in File Explorer

## Fire Instruction Types Explained

### Heat (H)
- Heats as fast as possible to target temperature
- No duration setting (immediate)
- Use for initial heating phases

### Ramp Up (R)
- Gradual, controlled temperature increase
- Requires both duration and target temperature
- Example: Ramp from 600°C to 1200°C over 3 hours

### Drop (D)
- Freefall cooling with door closed
- Requires only target temperature
- No duration (cools naturally until reaching target)
- Use for quick cooling phases

### Soak (S)
- Holds current temperature
- Requires only duration
- Use to ensure even heat distribution

### Cool (C)
- Controlled cool down (down ramp)
- Requires both duration and target temperature
- Gradual temperature decrease
- Use for controlled cooling phases
- Example: Cool from 1200°C to 600°C over 2 hours

## Example: Ceramic Jar Cast

```
1. Heat to 100°C (immediate)
2. Ramp Up to 600°C over 2 hours
3. Soak for 30 minutes
4. Ramp Up to 1200°C over 3 hours
5. Soak for 1 hour
6. Cool (down ramp) to 600°C over 2 hours
7. Drop to 200°C (natural cooling)
```

## Troubleshooting

**Can't connect to device:**
- Check device is powered on and WebREPL is enabled
- Verify IP address is correct
- Ensure you're on the same network (or connected to device's AP)

**Upload fails:**
- Ensure device has enough storage space
- Check that /presets folder exists on device
- Verify write permissions

**Preset not appearing:**
- Click **Refresh** buttons to reload lists
- Check file is saved with .json extension
- Verify JSON format is valid

## Need More Help?

- Check the example file: `jar_ceramic_cast.json.example`
- The Category in the JSON must match the folder name
- File name is always the "Key" value + .json
