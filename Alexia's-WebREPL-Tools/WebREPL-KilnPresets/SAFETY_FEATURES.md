# Safety Features - Undo/Redo & Save As Copy

## Overview
We've added comprehensive safety features to prevent accidental data loss while editing fire presets.

## Undo/Redo System ↶↷

### Features
- **Debounced State Saving**: Changes are batched with an 800ms delay to avoid cluttering undo history
- **Keyboard Shortcuts**: Ctrl+Z (Undo) and Ctrl+Y (Redo)
- **Visual Feedback**: Buttons are enabled/disabled based on undo/redo stack availability
- **Smart Tracking**: Only saves meaningful changes, not intermediate typing
- **Multiple Undo Levels**: Unlimited undo history during editing session

### How It Works

1. **Initial State**: When you open the editor, the initial state is saved
2. **Making Changes**: As you modify instructions, the timer starts
3. **Debounce Period**: After 800ms of no changes, state is saved to undo stack
4. **Undo Operation**:
   - Saves current state to redo stack
   - Restores previous state from undo stack
   - Updates graph automatically
5. **Redo Operation**:
   - Restores next state from redo stack
   - Updates graph automatically

### What Triggers Undo Saves
- Adding instructions (Heat, Ramp, Drop, Soak, Cool)
- Deleting instructions
- Changing target temperatures
- Modifying durations
- Reordering instructions (drag & drop)

### Technical Details
```csharp
// Undo stack holds previous states
Stack<List<FireInstruction>> _undoStack

// Redo stack holds undone states
Stack<List<FireInstruction>> _redoStack

// Debounce timer delays saves
DispatcherTimer _undoSaveTimer (800ms interval)
```

## Save As Copy Feature 📋

### Purpose
Create a duplicate of an existing preset with a new key name, allowing you to:
- Use an existing preset as a template
- Make variations without modifying the original
- Experiment safely with proven presets

### How to Use

1. **Right-click** on any preset in the local library
2. Select **"Save As Copy..."** from context menu
3. **Dialog appears** showing:
   - Original key (read-only)
   - New key field (pre-filled with "originalname_copy")
4. **Edit the new key** to your desired name
5. Click **"Save Copy"** or press Enter

### Features
- Auto-fills with `{original}_copy` suggestion
- Pre-selects text for easy replacement
- Validates that new key is not empty
- Copies all phases and settings
- Places copy in the same category
- Shows success message in status bar

### Example Workflow
```
1. Right-click "jar_ceramic_cast"
2. Select "Save As Copy..."
3. Change to "jar_ceramic_cast_experimental"
4. Click "Save Copy"
5. New preset appears in the same category
6. Edit the copy without affecting original
```

## Cancel Protection 🛡️

### Preset Editor
- **Cancel Button**: Discards all changes and closes editor
- **X Button**: Same as Cancel - no changes saved
- **ESC Key**: Closes without saving (IsCancel="True")
- All changes lost if you don't click Save

### Before You Lose Work
The application doesn't automatically save. You must:
1. Make your edits
2. Review the graph
3. Click the green **Save** button
4. Only then are changes written to disk

## Best Practices

### Using Undo/Redo
✅ **Do**:
- Make changes in batches
- Use undo to backtrack experiments
- Rely on visual graph feedback

❌ **Don't**:
- Rapid-fire clicks (let debounce settle)
- Close editor thinking undo persists (it doesn't)
- Mix undo with manual file editing

### Using Save As Copy
✅ **Do**:
- Copy before experimenting
- Use descriptive new names
- Keep originals as templates

❌ **Don't**:
- Overwrite original accidentally
- Use same key name (will replace)
- Forget which is which

### General Safety
✅ **Always**:
- Review the graph before saving
- Use descriptive key names
- Test new presets on test pieces first
- Keep backups of critical presets

❌ **Never**:
- Save without reviewing changes
- Delete presets you might need
- Push untested presets to device

## Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| Undo | Ctrl+Z |
| Redo | Ctrl+Y |
| Save | Enter (when Save focused) |
| Cancel | ESC |

## Troubleshooting

### Undo Button Disabled
- No previous states available
- At initial state already
- Just opened editor (nothing to undo)

### Redo Button Disabled
- No undone changes available
- Made new edits after undo (redo stack cleared)
- Just opened editor

### "Save Copy" Not in Menu
- Menu only appears on right-click
- Make sure you're clicking a preset (not category)
- Try refreshing local library

## Implementation Notes

### Undo System Architecture
- Uses `ObservableCollection` change tracking
- Debounces with `DispatcherTimer`
- Deep copies `FireInstruction` objects
- Preserves instruction order and all properties
- Clears redo stack on new changes (standard behavior)

### Save As Copy Process
1. Shows `SaveAsCopyDialog` with original key
2. User enters new key
3. Deep copies entire `FirePreset` object
4. All `FireInstruction` phases duplicated
5. Saves to same category folder
6. Refreshes library display
7. Shows confirmation in status bar

## Future Enhancements (Possible)

- [ ] Persistent undo across editor sessions
- [ ] Undo/redo with step descriptions
- [ ] Auto-save drafts
- [ ] Version history
- [ ] Compare two presets side-by-side
- [ ] Batch copy/rename operations
