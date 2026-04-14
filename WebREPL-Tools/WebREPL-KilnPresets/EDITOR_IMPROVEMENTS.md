# Preset Editor UI Improvements

## New Features

### 1. Drag and Drop Reordering
You can now reorder fire instructions by dragging and dropping them in the grid:
- Click and hold on any instruction row
- Drag it to the desired position
- Drop it to reorder

This makes it much easier to adjust your firing schedule without having to use the up/down arrow buttons.

### 2. Numeric Up/Down Controls

All numeric input fields now have scroll buttons (▲ ▼) for easy value adjustment:

#### Target Temperature
- Click ▲ to increase by **10°C**
- Click ▼ to decrease by **10°C**
- Still allows typing values directly

#### Duration Fields (Hours, Minutes, Seconds)
- Click ▲ to increase by **1 unit**
- Click ▼ to decrease by **1 unit**
- Smart overflow handling:
  - 60 seconds → 1 minute, 0 seconds
  - 60 minutes → 1 hour, 0 minutes
  - Underflow works in reverse

### 3. Improved Layout

The editor now has a more professional look:
- Cleaner spacing and alignment
- Larger width (1200px) to accommodate all controls
- Duration field expanded to 250px for better up/down button visibility
- Consistent button styling throughout

## Usage Tips

### Quick Temperature Adjustment
1. Use the ▲▼ buttons for quick 10°C increments
2. Type exact values when you know the target
3. Typical steps: 100, 200, 300, 400, 500, 600, etc.

### Duration Editing
1. Start with hours using ▲▼
2. Fine-tune with minutes
3. Add seconds if needed for precision
4. Example: 2h 30m 0s for a 2.5 hour ramp

### Reordering Instructions
1. Drag rows to rearrange your firing schedule
2. Much faster than repeatedly clicking ↑ or ↓
3. The graph updates automatically as you reorder

### Workflow Example

Building a firing schedule:
1. Add all phases using the "Add X" buttons
2. Set temperatures using ▲▼ buttons (10°C steps)
3. Set durations using ▲▼ buttons
4. Drag phases to correct order if needed
5. Watch the graph update in real-time
6. Fine-tune values by typing directly

## Keyboard Shortcuts

All text boxes still accept keyboard input:
- Type numbers directly for precise values
- Tab to move between fields
- Arrow keys work in text boxes for cursor movement
- Enter to confirm and move to next field

## Design Philosophy

The editor follows these principles:
- **Mouse-friendly**: Scroll buttons for quick adjustments
- **Keyboard-friendly**: Direct typing for precise values
- **Visual**: Real-time graph feedback
- **Intuitive**: Drag-drop feels natural
- **Forgiving**: Smart overflow handling prevents invalid values
