using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using WebREPL.Core;

namespace WebREPL_KilnPresets;

public partial class MainWindow : Window
{
    private PresetLibraryManager _libraryManager;
    private HostManager _hostManager;
    private WebReplClient? _client;
    private HostConfiguration? _currentHost;

    public MainWindow()
    {
        InitializeComponent();
        _libraryManager = new PresetLibraryManager();
        _hostManager = new HostManager();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadLocalLibrary();
    }

    private void LoadLocalLibrary()
    {
        var expandedCategories = new HashSet<string>();
        foreach (TreeViewItem item in LocalTreeView.Items)
        {
            if (item.IsExpanded && item.Tag is string category)
            {
                expandedCategories.Add(category);
            }
        }

        LocalTreeView.Items.Clear();
        var presets = _libraryManager.LoadAllPresets();
        var categories = presets.GroupBy(p => p.Category).OrderBy(g => g.Key);

        foreach (var category in categories)
        {
            var categoryNode = new TreeViewItem
            {
                Header = $"📁 {category.Key} ({category.Count()})",
                Tag = category.Key,
                IsExpanded = expandedCategories.Contains(category.Key)
            };

            foreach (var preset in category.OrderBy(p => p.Key))
            {
                var presetNode = new TreeViewItem
                {
                    Header = $"📄 {preset.Key}",
                    Tag = preset
                };

                if (!string.IsNullOrWhiteSpace(preset.Name))
                {
                    presetNode.Header = $"📄 {preset.Key} - {preset.Name}";
                }

                categoryNode.Items.Add(presetNode);
            }

            LocalTreeView.Items.Add(categoryNode);
        }
    }

    private async void LoadRemotePresets()
    {
        if (_client == null) return;

        try
        {
            RemoteTreeView.Items.Clear();
            StatusBarText.Text = "Loading remote presets...";

            var ws = _client.GetWebSocket();
            var categories = new Dictionary<string, TreeViewItem>();

            var files = await ListRemoteFilesRecursiveAsync("/presets");

            foreach (var file in files.Where(f => f.EndsWith(".json")))
            {
                var parts = file.Split('/').Where(p => !string.IsNullOrEmpty(p)).ToArray();
                if (parts.Length >= 2)
                {
                    var category = parts[^2];
                    var fileName = parts[^1];

                    if (!categories.ContainsKey(category))
                    {
                        var categoryNode = new TreeViewItem
                        {
                            Header = category,
                            Tag = $"/presets/{category}"
                        };
                        categories[category] = categoryNode;
                        RemoteTreeView.Items.Add(categoryNode);
                    }

                    var fileNode = new TreeViewItem
                    {
                        Header = fileName,
                        Tag = file
                    };
                    categories[category].Items.Add(fileNode);
                }
            }

            StatusBarText.Text = $"Loaded {files.Count} remote files";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading remote presets: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusBarText.Text = "Error loading remote presets";
        }
    }

    private async Task<List<string>> ListRemoteFilesRecursiveAsync(string path)
    {
        if (_client == null) return new List<string>();

        var result = new List<string>();
        var ws = _client.GetWebSocket();

        try
        {
            var items = await RemoteCommands.RemoteLsAsync(ws, path);

            foreach (var item in items)
            {
                var fullPath = $"{path}/{item.Name}";

                if (item.IsDirectory)
                {
                    var subFiles = await ListRemoteFilesRecursiveAsync(fullPath);
                    result.AddRange(subFiles);
                }
                else
                {
                    result.Add(fullPath);
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private void NewPreset_Click(object sender, RoutedEventArgs e)
    {
        var categories = _libraryManager.GetCategories();
        var dialog = new PresetEditorDialog(null, categories);
        if (dialog.ShowDialog() == true && dialog.Preset != null)
        {
            _libraryManager.SavePreset(dialog.Preset);
            LoadLocalLibrary();
            StatusBarText.Text = $"Created preset: {dialog.Preset.Key}";
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var host = _hostManager.GetMostRecentlyUsed() ?? _hostManager.Hosts.FirstOrDefault();
        
        if (host == null)
        {
            MessageBox.Show("No hosts configured. Please add a host first.", "No Hosts", MessageBoxButton.OK, MessageBoxImage.Information);
            ManageHosts_Click(sender, e);
            return;
        }

        try
        {
            StatusBarText.Text = $"Connecting to {host.Host}:{host.Port}...";
            _client = new WebReplClient();
            await _client.ConnectAsync(host.Host, host.Port, host.Password);

            _currentHost = host;
            _hostManager.UpdateLastUsed(host.Name);

            StatusText.Text = $"Connected to {host.Name}";
            StatusBarText.Text = "Connected";
            RefreshMenuItem.IsEnabled = true;

            LoadRemotePresets();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to connect: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusBarText.Text = "Connection failed";
            StatusText.Text = "Not Connected";
        }
    }

    private void ManageHosts_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HostManagerDialog(_hostManager);
        dialog.ShowDialog();
    }

    private void RefreshRemote_Click(object sender, RoutedEventArgs e)
    {
        LoadRemotePresets();
    }

    private void RefreshLocal_Click(object sender, RoutedEventArgs e)
    {
        LoadLocalLibrary();
        StatusBarText.Text = "Local library refreshed";
    }

    private void OpenLibraryFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = _libraryManager.GetLibraryPath();
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Kiln Fire Preset Manager\n\nManage fire presets for kiln controller.\n\nShares host configuration with WebREPL Commander.",
            "About", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LocalTreeView_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LocalTreeView.SelectedItem is TreeViewItem item && item.Tag is FirePreset preset)
        {
            var categories = _libraryManager.GetCategories();
            var dialog = new PresetEditorDialog(preset, categories);
            if (dialog.ShowDialog() == true && dialog.Preset != null)
            {
                var oldCategory = preset.Category;
                _libraryManager.MovePreset(dialog.Preset, oldCategory);
                LoadLocalLibrary();
                StatusBarText.Text = $"Updated preset: {dialog.Preset.Key}";
            }
        }
    }

    private void LocalTreeView_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && LocalTreeView.SelectedItem is TreeViewItem item && item.Tag is FirePreset)
        {
            DragDrop.DoDragDrop(LocalTreeView, item, DragDropEffects.Copy);
        }
    }

    private async void PushToDevice_Click(object sender, RoutedEventArgs e)
    {
        if (_client == null)
        {
            MessageBox.Show("Please connect to a device first.", "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (LocalTreeView.SelectedItem is TreeViewItem item && item.Tag is FirePreset preset)
        {
            try
            {
                StatusBarText.Text = $"Pushing {preset.Key} to device...";

                var categoryPath = Path.Combine(_libraryManager.GetLibraryPath(), preset.Category);
                var localFilePath = Path.Combine(categoryPath, preset.FileName);

                var remoteCategoryPath = $"/presets/{preset.Category}";
                var remoteFilePath = $"{remoteCategoryPath}/{preset.FileName}";

                var ws = _client.GetWebSocket();
                try
                {
                    await RemoteCommands.RemoteMkdirAsync(ws, remoteCategoryPath);
                }
                catch { }

                await _client.PutFileAsync(localFilePath, remoteFilePath);

                StatusBarText.Text = $"Successfully pushed {preset.Key} to device";
                LoadRemotePresets();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error pushing to device: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusBarText.Text = "Push failed";
            }
        }
    }

    private void EditPreset_Click(object sender, RoutedEventArgs e)
    {
        if (LocalTreeView.SelectedItem is TreeViewItem item && item.Tag is FirePreset preset)
        {
            var categories = _libraryManager.GetCategories();
            var dialog = new PresetEditorDialog(preset, categories);
            if (dialog.ShowDialog() == true && dialog.Preset != null)
            {
                var oldCategory = preset.Category;
                _libraryManager.MovePreset(dialog.Preset, oldCategory);
                LoadLocalLibrary();
                StatusBarText.Text = $"Updated preset: {dialog.Preset.Key}";
            }
        }
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (LocalTreeView.SelectedItem is TreeViewItem item && item.Tag is FirePreset preset)
        {
            var result = MessageBox.Show($"Are you sure you want to delete '{preset.Key}'?", 
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _libraryManager.DeletePreset(preset);
                    LoadLocalLibrary();
                    StatusBarText.Text = $"Deleted preset: {preset.Key}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting preset: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void SaveAsCopy_Click(object sender, RoutedEventArgs e)
    {
        if (LocalTreeView.SelectedItem is TreeViewItem item && item.Tag is FirePreset preset)
        {
            var dialog = new SaveAsCopyDialog(preset.Key);
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewKey))
            {
                try
                {
                    var newPreset = new FirePreset
                    {
                        Key = dialog.NewKey.Trim(),
                        Category = preset.Category,
                        Name = preset.Name,
                        Phases = preset.Phases.Select(p => new FireInstruction
                        {
                            Type = p.Type,
                            Duration = p.Duration,
                            Target = p.Target
                        }).ToList()
                    };

                    _libraryManager.SavePreset(newPreset);
                    LoadLocalLibrary();
                    StatusBarText.Text = $"Copied '{preset.Key}' to '{newPreset.Key}'";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving copy: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async void RemoteTreeView_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_client == null) return;

        if (RemoteTreeView.SelectedItem is TreeViewItem item && item.Tag is string remotePath && remotePath.EndsWith(".json"))
        {
            try
            {
                StatusBarText.Text = "Downloading preset...";

                var tempFile = Path.GetTempFileName();
                await _client.GetFileAsync(remotePath, tempFile);
                var content = await File.ReadAllTextAsync(tempFile);
                File.Delete(tempFile);

                var preset = System.Text.Json.JsonSerializer.Deserialize<FirePreset>(content);
                if (preset != null)
                {
                    var categories = _libraryManager.GetCategories();
                    var dialog = new PresetEditorDialog(preset, categories);
                    dialog.ShowDialog();
                }

                StatusBarText.Text = "Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading preset: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusBarText.Text = "Error";
            }
        }
    }

    private void LocalTreeView_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) || 
            e.Data.GetDataPresent(typeof(TreeViewItem)))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private async void LocalTreeView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            var position = e.GetPosition(LocalTreeView);
            var element = LocalTreeView.InputHitTest(position) as DependencyObject;

            TreeViewItem? targetItem = null;
            while (element != null && targetItem == null)
            {
                if (element is TreeViewItem item)
                    targetItem = item;
                element = System.Windows.Media.VisualTreeHelper.GetParent(element);
            }

            string targetCategory = "";
            if (targetItem != null)
            {
                if (targetItem.Tag is string categoryName)
                {
                    targetCategory = categoryName;
                }
                else if (targetItem.Tag is FirePreset preset)
                {
                    targetCategory = preset.Category;
                }
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                await HandleDroppedFiles(files, targetCategory);
            }
            else if (e.Data.GetDataPresent(typeof(TreeViewItem)))
            {
                var draggedItem = e.Data.GetData(typeof(TreeViewItem)) as TreeViewItem;
                if (draggedItem?.Tag is FirePreset draggedPreset && !string.IsNullOrEmpty(targetCategory))
                {
                    if (draggedPreset.Category != targetCategory)
                    {
                        var result = MessageBox.Show(
                            $"Move '{draggedPreset.Key}' from '{draggedPreset.Category}' to '{targetCategory}'?",
                            "Confirm Move",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            var oldCategory = draggedPreset.Category;
                            draggedPreset.Category = targetCategory;
                            _libraryManager.MovePreset(draggedPreset, oldCategory);
                            LoadLocalLibrary();
                            StatusBarText.Text = $"Moved {draggedPreset.Key} to {targetCategory}";
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error handling drop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusBarText.Text = "Drop operation failed";
        }
    }

    private async Task HandleDroppedFiles(string[] files, string targetCategory)
    {
        var imported = 0;
        var errors = new List<string>();

        foreach (var file in files)
        {
            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var content = await File.ReadAllTextAsync(file);
                var preset = System.Text.Json.JsonSerializer.Deserialize<FirePreset>(content);

                if (preset == null)
                {
                    errors.Add($"{Path.GetFileName(file)}: Invalid JSON format");
                    continue;
                }

                if (!string.IsNullOrEmpty(targetCategory))
                {
                    preset.Category = targetCategory;
                }

                if (string.IsNullOrEmpty(preset.Category))
                {
                    var categories = _libraryManager.GetCategories();
                    if (categories.Count > 0)
                    {
                        preset.Category = categories[0];
                    }
                    else
                    {
                        preset.Category = "general";
                    }
                }

                _libraryManager.SavePreset(preset);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        LoadLocalLibrary();

        if (imported > 0)
        {
            StatusBarText.Text = $"Imported {imported} preset(s)";
        }

        if (errors.Count > 0)
        {
            var errorMessage = $"Imported {imported} preset(s) with {errors.Count} error(s):\n\n" +
                               string.Join("\n", errors.Take(5));
            if (errors.Count > 5)
                errorMessage += $"\n... and {errors.Count - 5} more";

            MessageBox.Show(errorMessage, "Import Results", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RemoteTreeView_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void RemoteTreeView_Drop(object sender, DragEventArgs e)
    {
        if (_client == null)
        {
            MessageBox.Show("Please connect to a device first.", "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var position = e.GetPosition(RemoteTreeView);
        var element = RemoteTreeView.InputHitTest(position) as DependencyObject;

        TreeViewItem? targetItem = null;
        while (element != null && targetItem == null)
        {
            if (element is TreeViewItem item)
                targetItem = item;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }

        string targetCategory = "/";
        if (targetItem != null && targetItem.Tag is string tag)
        {
            if (tag.EndsWith(".json"))
            {
                var parts = tag.Split('/').Where(p => !string.IsNullOrEmpty(p)).ToArray();
                if (parts.Length >= 2)
                    targetCategory = parts[^2];
            }
            else if (tag.StartsWith("/presets/"))
            {
                targetCategory = tag.Replace("/presets/", "").Trim('/');
            }
        }

        if (LocalTreeView.SelectedItem is TreeViewItem localItem && localItem.Tag is FirePreset preset)
        {
            try
            {
                StatusBarText.Text = $"Uploading {preset.Key}...";

                var categoryPath = Path.Combine(_libraryManager.GetLibraryPath(), preset.Category);
                var localFilePath = Path.Combine(categoryPath, preset.FileName);

                var remoteCategoryPath = $"/presets/{targetCategory}";
                var remoteFilePath = $"{remoteCategoryPath}/{preset.FileName}";

                var ws = _client.GetWebSocket();
                try
                {
                    await RemoteCommands.RemoteMkdirAsync(ws, remoteCategoryPath);
                }
                catch { }

                await _client.PutFileAsync(localFilePath, remoteFilePath);

                StatusBarText.Text = $"Uploaded {preset.Key} to {targetCategory}";
                LoadRemotePresets();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error uploading preset: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusBarText.Text = "Upload failed";
            }
        }
    }
}

public class IsStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
