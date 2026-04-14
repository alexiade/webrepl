# Fire Profile Graph Feature

## Overview

The Preset Editor now includes a real-time fire profile graph that visualizes the temperature curve of your firing schedule.

## Features

### Visual Profile
- **Temperature vs Time**: X-axis shows time in hours, Y-axis shows temperature in Celsius
- **Live Updates**: Graph updates automatically as you add, edit, or remove instructions
- **Grid Lines**: Shows temperature markers and reference lines for easy reading

### Estimated Duration Display

The graph uses a **300°C per hour** heating/cooling rate to estimate durations for:

- **Heat (H)** instructions - where only target temperature is specified
- **Drop (D)** instructions - where only target temperature is specified

### Color Coding

- **Blue Solid Lines**: Actual durations from your preset
  - Ramp Up (R)
  - Cool (C) 
  - Soak (S) - shown as horizontal lines

- **Orange Dashed Lines**: Estimated durations (60°C/hour assumption)
  - Heat (H) - heating estimate
  - Drop (D) - cooling estimate

### Statistics Display

At the top of the graph panel:
- **Total Time**: Complete firing duration including estimates
- **Max Temperature**: Highest temperature reached in the profile

## Estimation Assumptions

### Heat (H) - Fast Heating
```
Estimated Duration = (Target Temp - Current Temp) / 60°C per hour
```
Example: Heating from 20°C to 620°C
- Temperature difference: 600°C
- Estimated time: 600 / 60 = 10 hours

### Drop (D) - Natural Cooling
```
Estimated Duration = (Current Temp - Target Temp) / 60°C per hour
```
Example: Dropping from 1200°C to 600°C
- Temperature difference: 600°C
- Estimated time: 600 / 60 = 10 hours

## Important Notes

⚠️ **Estimated segments are approximations only!**

The 60°C per hour rate is a general assumption and actual rates vary based on:
- Kiln size and insulation
- Ambient temperature
- Element power
- Load density
- Door position (for Drop)

Use the estimated times for **planning purposes only**. Actual firing times may differ significantly.

## Reading the Graph

### Example Profile

```
1. Heat to 100°C (estimated: ~0.27 hours / 16 min) - Orange dashed
2. Ramp to 600°C over 2 hours - Blue solid
3. Soak for 30 min - Blue horizontal
4. Ramp to 1200°C over 3 hours - Blue solid
5. Soak for 1 hour - Blue horizontal
6. Cool to 600°C over 2 hours - Blue solid
7. Drop to 200°C (estimated: ~1.33 hours / 80 min) - Orange dashed
```

**Total Time**: Approximately 10 hours

The graph will show:
- Sharp rise (orange) for initial heat
- Gradual climbs (blue) for controlled ramps
- Flat sections (blue) for soaks
- Gradual descent (blue) for controlled cool
- Drop to final temp (orange) for natural cooling

## Tips

- **Plan Conservatively**: Add buffer time to estimated segments
- **Monitor First Firings**: Track actual times to calibrate your expectations
- **Consider Thermal Mass**: Loaded kilns heat/cool slower than empty ones
- **Safety First**: Never rush cooling phases below manufacturer recommendations
