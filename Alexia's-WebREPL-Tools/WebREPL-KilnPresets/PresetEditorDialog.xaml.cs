using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WebREPL_KilnPresets;

public partial class PresetEditorDialog : Window
{
    public FirePreset? Preset { get; private set; }
    private ObservableCollection<InstructionViewModel> _instructions = new();
    private const float ESTIMATED_RATE = 300.0f; // °C per hour for Heat and Drop

    public PresetEditorDialog(FirePreset? preset, List<string> categories)
    {
        InitializeComponent();

        CategoryComboBox.ItemsSource = categories;

        if (preset != null)
        {
            Preset = preset;
            KeyTextBox.Text = preset.Key;
            CategoryComboBox.Text = preset.Category;
            DescriptionTextBox.Text = preset.Name;

            foreach (var instruction in preset.Phases)
            {
                _instructions.Add(new InstructionViewModel(instruction));
            }
        }
        else
        {
            Preset = new FirePreset();
            if (categories.Count > 0)
                CategoryComboBox.Text = categories[0];
        }

        InstructionsGrid.ItemsSource = _instructions;

        _instructions.CollectionChanged += (s, e) => UpdateProfileChart();
        foreach (var instruction in _instructions)
        {
            instruction.PropertyChanged += (s, e) => UpdateProfileChart();
        }

        ProfileCanvas.SizeChanged += (s, e) => UpdateProfileChart();

        UpdateProfileChart();
    }

    private void AddHeat_Click(object sender, RoutedEventArgs e)
    {
        var vm = new InstructionViewModel(new FireInstruction { Type = "H" });
        vm.PropertyChanged += (s, e) => UpdateProfileChart();
        _instructions.Add(vm);
    }

    private void AddRamp_Click(object sender, RoutedEventArgs e)
    {
        var vm = new InstructionViewModel(new FireInstruction { Type = "R", Duration = 0, Target = 0 });
        vm.PropertyChanged += (s, e) => UpdateProfileChart();
        _instructions.Add(vm);
    }

    private void AddDown_Click(object sender, RoutedEventArgs e)
    {
        var vm = new InstructionViewModel(new FireInstruction { Type = "D", Target = 0 });
        vm.PropertyChanged += (s, e) => UpdateProfileChart();
        _instructions.Add(vm);
    }

    private void AddSoak_Click(object sender, RoutedEventArgs e)
    {
        var vm = new InstructionViewModel(new FireInstruction { Type = "S", Duration = 0 });
        vm.PropertyChanged += (s, e) => UpdateProfileChart();
        _instructions.Add(vm);
    }

    private void AddCool_Click(object sender, RoutedEventArgs e)
    {
        var vm = new InstructionViewModel(new FireInstruction { Type = "C", Duration = 0, Target = 0 });
        vm.PropertyChanged += (s, e) => UpdateProfileChart();
        _instructions.Add(vm);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is InstructionViewModel instruction)
        {
            var index = _instructions.IndexOf(instruction);
            if (index > 0)
            {
                _instructions.Move(index, index - 1);
            }
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is InstructionViewModel instruction)
        {
            var index = _instructions.IndexOf(instruction);
            if (index < _instructions.Count - 1)
            {
                _instructions.Move(index, index + 1);
            }
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is InstructionViewModel instruction)
        {
            _instructions.Remove(instruction);
        }
    }

    private void UpdateProfileChart()
    {
        ProfileCanvas.Children.Clear();

        if (ProfileCanvas.ActualWidth < 10 || ProfileCanvas.ActualHeight < 10)
            return;

        var points = CalculateProfilePoints();
        if (points.Count < 2)
        {
            ProfileStatsText.Text = "Total Time: 0h 0m";
            return;
        }

        var totalTime = points[points.Count - 1].Time;
        var maxTemp = points.Max(p => p.Temperature);
        var minTemp = points.Min(p => p.Temperature);

        var hours = (int)(totalTime / 3600);
        var minutes = (int)((totalTime % 3600) / 60);
        ProfileStatsText.Text = $"Total Time: {hours}h {minutes}m | Max Temp: {maxTemp}°C";

        DrawChart(points, maxTemp, minTemp, totalTime);
    }

    private List<ProfilePoint> CalculateProfilePoints()
    {
        var points = new List<ProfilePoint>();
        float currentTime = 0;
        int currentTemp = 20; // Assume room temperature start

        points.Add(new ProfilePoint(0, currentTemp, false));

        foreach (var instruction in _instructions)
        {
            var type = instruction.ToFireInstruction().Type;
            var target = instruction.Target ?? currentTemp;
            var duration = instruction.ToFireInstruction().Duration ?? 0;

            switch (type)
            {
                case "H": // Heat - estimate duration
                    var tempDiff = Math.Abs(target - currentTemp);
                    var estimatedDuration = (tempDiff / ESTIMATED_RATE) * 3600;
                    currentTime += estimatedDuration;
                    points.Add(new ProfilePoint(currentTime, target, true));
                    currentTemp = target;
                    break;

                case "R": // Ramp Up
                    currentTime += duration;
                    points.Add(new ProfilePoint(currentTime, target, false));
                    currentTemp = target;
                    break;

                case "D": // Drop - estimate duration
                    tempDiff = Math.Abs(currentTemp - target);
                    estimatedDuration = (tempDiff / ESTIMATED_RATE) * 3600;
                    currentTime += estimatedDuration;
                    points.Add(new ProfilePoint(currentTime, target, true));
                    currentTemp = target;
                    break;

                case "S": // Soak
                    currentTime += duration;
                    points.Add(new ProfilePoint(currentTime, currentTemp, false));
                    break;

                case "C": // Cool (controlled down ramp)
                    currentTime += duration;
                    points.Add(new ProfilePoint(currentTime, target, false));
                    currentTemp = target;
                    break;
            }
        }

        return points;
    }

    private void DrawChart(List<ProfilePoint> points, int maxTemp, int minTemp, float totalTime)
    {
        var margin = 40.0;
        var width = ProfileCanvas.ActualWidth - margin * 2;
        var height = ProfileCanvas.ActualHeight - margin * 2;

        if (width < 10 || height < 10) return;

        var tempRange = Math.Max(maxTemp - minTemp, 100);
        var timeRange = Math.Max(totalTime, 1);

        // Draw axes
        var axisLine = new Line
        {
            X1 = margin,
            Y1 = margin,
            X2 = margin,
            Y2 = ProfileCanvas.ActualHeight - margin,
            Stroke = Brushes.Black,
            StrokeThickness = 2
        };
        ProfileCanvas.Children.Add(axisLine);

        axisLine = new Line
        {
            X1 = margin,
            Y1 = ProfileCanvas.ActualHeight - margin,
            X2 = ProfileCanvas.ActualWidth - margin,
            Y2 = ProfileCanvas.ActualHeight - margin,
            Stroke = Brushes.Black,
            StrokeThickness = 2
        };
        ProfileCanvas.Children.Add(axisLine);

        // Draw axis labels
        var tempLabel = new TextBlock
        {
            Text = "Temperature (°C)",
            FontSize = 10
        };
        Canvas.SetLeft(tempLabel, 5);
        Canvas.SetTop(tempLabel, 5);
        ProfileCanvas.Children.Add(tempLabel);

        var timeLabel = new TextBlock
        {
            Text = "Time (hours)",
            FontSize = 10
        };
        Canvas.SetLeft(timeLabel, ProfileCanvas.ActualWidth - 80);
        Canvas.SetTop(timeLabel, ProfileCanvas.ActualHeight - 30);
        ProfileCanvas.Children.Add(timeLabel);

        // Draw grid lines and temp markers
        for (int i = 0; i <= 4; i++)
        {
            var temp = minTemp + (tempRange * i / 4);
            var y = ProfileCanvas.ActualHeight - margin - (height * i / 4);

            var gridLine = new Line
            {
                X1 = margin,
                Y1 = y,
                X2 = ProfileCanvas.ActualWidth - margin,
                Y2 = y,
                Stroke = Brushes.LightGray,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }
            };
            ProfileCanvas.Children.Add(gridLine);

            var label = new TextBlock
            {
                Text = temp.ToString("F0"),
                FontSize = 9
            };
            Canvas.SetLeft(label, margin - 35);
            Canvas.SetTop(label, y - 7);
            ProfileCanvas.Children.Add(label);
        }

        // Draw profile lines
        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];

            var x1 = margin + (p1.Time / timeRange) * width;
            var y1 = ProfileCanvas.ActualHeight - margin - ((p1.Temperature - minTemp) / (float)tempRange) * height;
            var x2 = margin + (p2.Time / timeRange) * width;
            var y2 = ProfileCanvas.ActualHeight - margin - ((p2.Temperature - minTemp) / (float)tempRange) * height;

            var line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = p2.IsEstimated ? Brushes.Orange : Brushes.Blue,
                StrokeThickness = p2.IsEstimated ? 2 : 2
            };

            if (p2.IsEstimated)
            {
                line.StrokeDashArray = new DoubleCollection { 4, 2 };
            }

            ProfileCanvas.Children.Add(line);

            // Add points
            var ellipse = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = p2.IsEstimated ? Brushes.Orange : Brushes.Blue
            };
            Canvas.SetLeft(ellipse, x2 - 3);
            Canvas.SetTop(ellipse, y2 - 3);
            ProfileCanvas.Children.Add(ellipse);
        }

        // Add legend
        var legendY = margin + 10;

        var actualLine = new Line
        {
            X1 = ProfileCanvas.ActualWidth - margin - 100,
            Y1 = legendY,
            X2 = ProfileCanvas.ActualWidth - margin - 70,
            Y2 = legendY,
            Stroke = Brushes.Blue,
            StrokeThickness = 2
        };
        ProfileCanvas.Children.Add(actualLine);

        var actualLabel = new TextBlock
        {
            Text = "Actual",
            FontSize = 9
        };
        Canvas.SetLeft(actualLabel, ProfileCanvas.ActualWidth - margin - 65);
        Canvas.SetTop(actualLabel, legendY - 7);
        ProfileCanvas.Children.Add(actualLabel);

        legendY += 15;

        var estimatedLine = new Line
        {
            X1 = ProfileCanvas.ActualWidth - margin - 100,
            Y1 = legendY,
            X2 = ProfileCanvas.ActualWidth - margin - 70,
            Y2 = legendY,
            Stroke = Brushes.Orange,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        ProfileCanvas.Children.Add(estimatedLine);

        var estimatedLabel = new TextBlock
        {
            Text = "Estimated",
            FontSize = 9
        };
        Canvas.SetLeft(estimatedLabel, ProfileCanvas.ActualWidth - margin - 65);
        Canvas.SetTop(estimatedLabel, legendY - 7);
        ProfileCanvas.Children.Add(estimatedLabel);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(KeyTextBox.Text))
        {
            MessageBox.Show("Key (file name) is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(CategoryComboBox.Text))
        {
            MessageBox.Show("Category is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Preset = new FirePreset
        {
            Key = KeyTextBox.Text.Trim(),
            Category = CategoryComboBox.Text.Trim(),
            Name = DescriptionTextBox.Text.Trim(),
            Phases = _instructions.Select(vm => vm.ToFireInstruction()).ToList()
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class InstructionViewModel : INotifyPropertyChanged
{
    private FireInstruction _instruction;
    private int _durationHours;
    private int _durationMinutes;
    private int _durationSeconds;

    public event PropertyChangedEventHandler? PropertyChanged;

    public InstructionViewModel(FireInstruction instruction)
    {
        _instruction = instruction;

        if (instruction.Duration.HasValue)
        {
            var totalSeconds = (int)instruction.Duration.Value;
            _durationHours = totalSeconds / 3600;
            _durationMinutes = (totalSeconds % 3600) / 60;
            _durationSeconds = totalSeconds % 60;
        }
    }

    public string DisplayName => _instruction.DisplayName;

    public bool ShowTarget
    {
        get
        {
            return _instruction.Type switch
            {
                "H" => true,  // Heat - has target
                "R" => true,  // Ramp Up - has target
                "D" => true,  // Drop - has target
                "S" => false, // Soak - no target
                "C" => true,  // Cool - has target
                _ => true
            };
        }
    }

    public bool ShowDuration
    {
        get
        {
            return _instruction.Type switch
            {
                "H" => false, // Heat - no duration
                "R" => true,  // Ramp Up - has duration
                "D" => false, // Drop - no duration
                "S" => true,  // Soak - has duration
                "C" => true,  // Cool - has duration
                _ => true
            };
        }
    }

    public int? Target
    {
        get => _instruction.Target;
        set
        {
            _instruction.Target = value;
            OnPropertyChanged();
        }
    }

    public int DurationHours
    {
        get => _durationHours;
        set
        {
            _durationHours = value;
            UpdateDuration();
            OnPropertyChanged();
        }
    }

    public int DurationMinutes
    {
        get => _durationMinutes;
        set
        {
            _durationMinutes = value;
            UpdateDuration();
            OnPropertyChanged();
        }
    }

    public int DurationSeconds
    {
        get => _durationSeconds;
        set
        {
            _durationSeconds = value;
            UpdateDuration();
            OnPropertyChanged();
        }
    }

    private void UpdateDuration()
    {
        _instruction.Duration = (_durationHours * 3600) + (_durationMinutes * 60) + _durationSeconds;
    }

    public FireInstruction ToFireInstruction()
    {
        return _instruction;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ProfilePoint
{
    public float Time { get; set; }
    public int Temperature { get; set; }
    public bool IsEstimated { get; set; }

    public ProfilePoint(float time, int temperature, bool isEstimated)
    {
        Time = time;
        Temperature = temperature;
        IsEstimated = isEstimated;
    }
}
