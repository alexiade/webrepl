using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WebREPL.Core;
using MessageBox = System.Windows.MessageBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WebREPL_Commander;

public partial class MainWindow : Window
{
    private WebReplClient? _client;
    private string _currentLocalPath = "";
    private string _currentRemotePath = "/";
    private ObservableCollection<FileItemViewModel> _localFiles = new();
    private ObservableCollection<FileItemViewModel> _remoteFiles = new();
    private CancellationTokenSource? _cts;
    private HostManager _hostManager;

    public MainWindow()
    {
        InitializeComponent();
        lstLocal.ItemsSource = _localFiles;
        lstRemote.ItemsSource = _remoteFiles;

        _hostManager = new HostManager();
        LoadHostConfigurations();
    }

    private void LoadHostConfigurations()
    {
        cmbHosts.ItemsSource = _hostManager.Hosts;

        var lastUsed = _hostManager.GetLastUsed();
        if (lastUsed != null)
        {
            cmbHosts.SelectedItem = lastUsed;
        }
    }

    private void CmbHosts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbHosts.SelectedItem is HostConfiguration host)
        {
            txtConnectionInfo.Text = $"{host.Host}:{host.Port}";

            // Apply default local path if specified and not connected
            if (_client?.IsConnected != true && !string.IsNullOrWhiteSpace(host.LocalDefaultPath) && Directory.Exists(host.LocalDefaultPath))
            {
                _currentLocalPath = host.LocalDefaultPath;
                RefreshLocalFiles();
            }
        }
    }

    private void BtnManageHosts_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HostManagerDialog(_hostManager);
        dialog.Owner = this;
        dialog.ShowDialog();

        if (dialog.ConfigurationsChanged)
        {
            LoadHostConfigurations();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _currentLocalPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        RefreshLocalFiles();
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (_client?.IsConnected == true)
        {
            _client?.Dispose();
            _client = null;
            btnConnect.Content = "Connect";
            lstRemote.IsEnabled = false;
            btnRemoteRefresh.IsEnabled = false;
            btnReset.IsEnabled = false;
            txtStatus.Text = "Disconnected";
            txtConnectionInfo.Text = "";
            _remoteFiles.Clear();
            return;
        }

        if (cmbHosts.SelectedItem is not HostConfiguration selectedHost)
        {
            MessageBox.Show("Please select a host configuration", "No Host Selected", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            btnConnect.IsEnabled = false;
            cmbHosts.IsEnabled = false;
            btnManageHosts.IsEnabled = false;
            txtStatus.Text = "Connecting...";

            _client = new WebReplClient();

            await _client.ConnectAsync(selectedHost.Host, selectedHost.Port, selectedHost.Password);

            _hostManager.UpdateLastUsed(selectedHost.Name);

            btnConnect.Content = "Disconnect";
            lstRemote.IsEnabled = true;
            btnRemoteRefresh.IsEnabled = true;
            btnReset.IsEnabled = true;
            txtStatus.Text = $"Connected - Version: {_client.RemoteVersion}";
            txtConnectionInfo.Text = $"✓ {selectedHost.Name}";

            // Apply default remote path if specified
            if (!string.IsNullOrWhiteSpace(selectedHost.RemoteDefaultPath))
            {
                _currentRemotePath = selectedHost.RemoteDefaultPath;
            }

            await RefreshRemoteFiles();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Connection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            txtStatus.Text = "Connection failed";
            txtConnectionInfo.Text = "";
            _client?.Dispose();
            _client = null;
        }
        finally
        {
            btnConnect.IsEnabled = true;
            cmbHosts.IsEnabled = true;
            btnManageHosts.IsEnabled = true;
        }
    }

    private void RefreshLocalFiles()
    {
        _localFiles.Clear();
        txtLocalPath.Text = _currentLocalPath;

        try
        {
            var dirInfo = new DirectoryInfo(_currentLocalPath);

            if (dirInfo.Parent != null)
            {
                _localFiles.Add(new FileItemViewModel("..", true, 0));
            }

            foreach (var dir in dirInfo.GetDirectories())
            {
                _localFiles.Add(new FileItemViewModel(dir.Name, true, 0));
            }

            foreach (var file in dirInfo.GetFiles())
            {
                _localFiles.Add(new FileItemViewModel(file.Name, false, file.Length));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading local directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RefreshRemoteFiles()
    {
        if (_client == null || !_client.IsConnected) return;

        try
        {
            txtStatus.Text = "Reading remote directory...";
            _remoteFiles.Clear();
            txtRemotePath.Text = _currentRemotePath;

            var ws = _client.GetWebSocket();

            if (_currentRemotePath != "/")
            {
                _remoteFiles.Add(new FileItemViewModel("..", true, 0));
            }

            var files = await RemoteCommands.RemoteLsAsync(ws, _currentRemotePath == "/" ? null : _currentRemotePath);

            foreach (var file in files.OrderBy(f => !f.IsDirectory).ThenBy(f => f.Name))
            {
                _remoteFiles.Add(new FileItemViewModel(file.Name, file.IsDirectory, file.Size));
            }

            txtStatus.Text = $"Remote: {files.Count} items";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading remote directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            txtStatus.Text = "Error reading remote directory";
        }
    }

    private void BtnLocalRefresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshLocalFiles();
    }

    private async void BtnRemoteRefresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshRemoteFiles();
    }

    private void LstLocal_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (lstLocal.SelectedItem is FileItemViewModel item && item.IsDirectory)
        {
            NavigateLocalDirectory(item.Name);
        }
    }

    private async void LstRemote_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (lstRemote.SelectedItem is FileItemViewModel item && item.IsDirectory)
        {
            await NavigateRemoteDirectory(item.Name);
        }
    }

    private void LstLocal_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && lstLocal.SelectedItem is FileItemViewModel item && item.IsDirectory)
        {
            NavigateLocalDirectory(item.Name);
        }
    }

    private async void LstRemote_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && lstRemote.SelectedItem is FileItemViewModel item && item.IsDirectory)
        {
            await NavigateRemoteDirectory(item.Name);
        }
    }

    private void NavigateLocalDirectory(string dirName)
    {
        if (dirName == "..")
        {
            var parent = Directory.GetParent(_currentLocalPath);
            if (parent != null)
                _currentLocalPath = parent.FullName;
        }
        else
        {
            _currentLocalPath = Path.Combine(_currentLocalPath, dirName);
        }
        RefreshLocalFiles();
    }

    private async Task NavigateRemoteDirectory(string dirName)
    {
        if (_client == null || !_client.IsConnected) return;

        try
        {
            var ws = _client.GetWebSocket();

            if (dirName == "..")
            {
                if (_currentRemotePath != "/")
                {
                    var lastSlash = _currentRemotePath.TrimEnd('/').LastIndexOf('/');
                    _currentRemotePath = lastSlash <= 0 ? "/" : _currentRemotePath.Substring(0, lastSlash);
                }
            }
            else
            {
                _currentRemotePath = _currentRemotePath == "/" 
                    ? $"/{dirName}" 
                    : $"{_currentRemotePath}/{dirName}";
            }

            await RemoteCommands.RemoteCdAsync(ws, _currentRemotePath);
            await RefreshRemoteFiles();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error navigating remote directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (lstLocal.IsFocused && lstLocal.SelectedItems.Count > 0)
        {
            await CopyLocalToRemote();
        }
        else if (lstRemote.IsFocused && lstRemote.SelectedItems.Count > 0)
        {
            await CopyRemoteToLocal();
        }
        else
        {
            MessageBox.Show("Select files in either Local or Remote panel to copy", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task CopyLocalToRemote()
    {
        if (_client == null || !_client.IsConnected)
        {
            MessageBox.Show("Not connected to remote device", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var selectedFiles = lstLocal.SelectedItems.Cast<FileItemViewModel>()
            .Where(f => !f.IsDirectory && f.Name != "..")
            .ToList();

        if (selectedFiles.Count == 0)
        {
            MessageBox.Show("Select files (not directories) to copy", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _cts = new CancellationTokenSource();
            progressBar.Maximum = selectedFiles.Count;
            progressBar.Value = 0;

            foreach (var file in selectedFiles)
            {
                var localPath = Path.Combine(_currentLocalPath, file.Name);
                var remotePath = _currentRemotePath == "/" ? $"/{file.Name}" : $"{_currentRemotePath}/{file.Name}";

                txtStatus.Text = $"Uploading {file.Name}...";

                var progress = new Progress<FileTransferProgress>(p =>
                {
                    txtProgress.Text = $"{p.PercentComplete:F1}% ({p.BytesTransferred:N0} / {p.TotalBytes:N0} bytes)";
                });

                await _client.PutFileAsync(localPath, remotePath, progress, _cts.Token);
                progressBar.Value++;
            }

            txtStatus.Text = $"Uploaded {selectedFiles.Count} file(s)";
            txtProgress.Text = "";
            progressBar.Value = 0;

            await RefreshRemoteFiles();
        }
        catch (OperationCanceledException)
        {
            txtStatus.Text = "Upload cancelled";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error uploading files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task CopyRemoteToLocal()
    {
        if (_client == null || !_client.IsConnected)
        {
            MessageBox.Show("Not connected to remote device", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var selectedFiles = lstRemote.SelectedItems.Cast<FileItemViewModel>()
            .Where(f => !f.IsDirectory && f.Name != "..")
            .ToList();

        if (selectedFiles.Count == 0)
        {
            MessageBox.Show("Select files (not directories) to copy", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _cts = new CancellationTokenSource();
            progressBar.Maximum = selectedFiles.Count;
            progressBar.Value = 0;

            foreach (var file in selectedFiles)
            {
                var remotePath = _currentRemotePath == "/" ? $"/{file.Name}" : $"{_currentRemotePath}/{file.Name}";
                var localPath = Path.Combine(_currentLocalPath, file.Name);

                txtStatus.Text = $"Downloading {file.Name}...";

                var progress = new Progress<FileTransferProgress>(p =>
                {
                    txtProgress.Text = $"{p.BytesTransferred:N0} bytes";
                });

                await _client.GetFileAsync(remotePath, localPath, progress, _cts.Token);
                progressBar.Value++;
            }

            txtStatus.Text = $"Downloaded {selectedFiles.Count} file(s)";
            txtProgress.Text = "";
            progressBar.Value = 0;

            RefreshLocalFiles();
        }
        catch (OperationCanceledException)
        {
            txtStatus.Text = "Download cancelled";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error downloading files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (lstLocal.IsFocused && lstLocal.SelectedItems.Count > 0)
        {
            DeleteLocalFiles();
        }
        else if (lstRemote.IsFocused && lstRemote.SelectedItems.Count > 0)
        {
            await DeleteRemoteFiles();
        }
        else
        {
            MessageBox.Show("Select files to delete", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void DeleteLocalFiles()
    {
        var selectedItems = lstLocal.SelectedItems.Cast<FileItemViewModel>()
            .Where(f => f.Name != "..")
            .ToList();

        if (selectedItems.Count == 0) return;

        var result = MessageBox.Show(
            $"Delete {selectedItems.Count} item(s)?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            foreach (var item in selectedItems)
            {
                var path = Path.Combine(_currentLocalPath, item.Name);

                if (item.IsDirectory)
                {
                    Directory.Delete(path, true);
                }
                else
                {
                    File.Delete(path);
                }
            }

            txtStatus.Text = $"Deleted {selectedItems.Count} item(s)";
            RefreshLocalFiles();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error deleting files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteRemoteFiles()
    {
        if (_client == null || !_client.IsConnected)
        {
            MessageBox.Show("Not connected to remote device", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var selectedItems = lstRemote.SelectedItems.Cast<FileItemViewModel>()
            .Where(f => f.Name != "..")
            .ToList();

        if (selectedItems.Count == 0) return;

        var result = MessageBox.Show(
            $"Delete {selectedItems.Count} item(s) from remote device?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var ws = _client.GetWebSocket();

            foreach (var item in selectedItems)
            {
                var path = _currentRemotePath == "/" ? $"/{item.Name}" : $"{_currentRemotePath}/{item.Name}";

                if (item.IsDirectory)
                {
                    await RemoteCommands.RemoteRmdirAsync(ws, path);
                }
                else
                {
                    await RemoteCommands.RemoteDeleteAsync(ws, path);
                }
            }

            txtStatus.Text = $"Deleted {selectedItems.Count} item(s)";
            await RefreshRemoteFiles();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error deleting remote files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnMkDir_Click(object sender, RoutedEventArgs e)
    {
        if (lstLocal.IsFocused)
        {
            CreateLocalDirectory();
        }
        else if (lstRemote.IsFocused)
        {
            await CreateRemoteDirectory();
        }
        else
        {
            MessageBox.Show("Focus Local or Remote panel to create directory", "Create Directory", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CreateLocalDirectory()
    {
        var dialog = new InputDialog("Create Local Directory", "Enter directory name:");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
        {
            try
            {
                var path = Path.Combine(_currentLocalPath, dialog.ResponseText);
                Directory.CreateDirectory(path);
                txtStatus.Text = $"Created directory: {dialog.ResponseText}";
                RefreshLocalFiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task CreateRemoteDirectory()
    {
        if (_client == null || !_client.IsConnected)
        {
            MessageBox.Show("Not connected to remote device", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dialog = new InputDialog("Create Remote Directory", "Enter directory name:");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
        {
            try
            {
                var ws = _client.GetWebSocket();
                var path = _currentRemotePath == "/" ? $"/{dialog.ResponseText}" : $"{_currentRemotePath}/{dialog.ResponseText}";
                await RemoteCommands.RemoteMkdirAsync(ws, path);
                txtStatus.Text = $"Created remote directory: {dialog.ResponseText}";
                await RefreshRemoteFiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating remote directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnView_Click(object sender, RoutedEventArgs e)
    {
        if (lstLocal.SelectedItem is FileItemViewModel localItem && !localItem.IsDirectory)
        {
            ViewLocalFile(localItem.Name);
        }
        else if (lstRemote.SelectedItem is FileItemViewModel remoteItem && !remoteItem.IsDirectory)
        {
            MessageBox.Show("Remote file viewing not yet implemented. Download the file first.", "View", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ViewLocalFile(string fileName)
    {
        try
        {
            var path = Path.Combine(_currentLocalPath, fileName);
            var content = File.ReadAllText(path);

            var viewer = new TextViewerDialog(fileName, content);
            viewer.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error viewing file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (lstLocal.SelectedItem is FileItemViewModel item && !item.IsDirectory)
        {
            try
            {
                var path = Path.Combine(_currentLocalPath, item.Name);
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening editor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnExec_Click(object sender, RoutedEventArgs e)
    {
        if (_client == null || !_client.IsConnected)
        {
            MessageBox.Show("Not connected to remote device", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dialog = new InputDialog("Execute Python Code", "Enter Python expression:");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
        {
            try
            {
                txtStatus.Text = "Executing...";
                var result = await _client.ExecuteAsync(dialog.ResponseText);

                var viewer = new TextViewerDialog("Execution Result", result);
                viewer.ShowDialog();

                txtStatus.Text = "Execution complete";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing code: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (_client == null || !_client.IsConnected)
        {
            MessageBox.Show("Not connected to remote device", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var terminal = new TerminalDialog(_client);
        terminal.ShowDialog();

        await RefreshRemoteFiles();
    }

    private async void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        if (_client == null || !_client.IsConnected)
        {
            MessageBox.Show("Not connected to remote device", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var result = MessageBox.Show(
            "Reset the remote device?\nHard reset will power cycle, Soft reset will reload Python.",
            "Reset Device",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes || result == MessageBoxResult.No)
        {
            try
            {
                var ws = _client.GetWebSocket();
                await RemoteCommands.RemoteResetAsync(ws, result == MessageBoxResult.Yes);

                txtStatus.Text = "Device reset";

                _client?.Dispose();
                _client = null;
                btnConnect.Content = "Connect";
                lstRemote.IsEnabled = false;
                btnRemoteRefresh.IsEnabled = false;
                btnReset.IsEnabled = false;
                _remoteFiles.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting device: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnMove_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Move functionality not yet implemented. Use Copy + Delete instead.", "Move", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnQuit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _cts?.Cancel();
        _client?.Dispose();
        base.OnClosing(e);
    }

    private void lstRemote_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }
}

public class FileItemViewModel
{
    public string Name { get; }
    public bool IsDirectory { get; }
    public long Size { get; }

    public string DisplayName => IsDirectory ? $"[{Name}]" : Name;
    public string DisplaySize => IsDirectory ? "<DIR>" : Size.ToString("N0");

    public FileItemViewModel(string name, bool isDirectory, long size)
    {
        Name = name;
        IsDirectory = isDirectory;
        Size = size;
    }
}