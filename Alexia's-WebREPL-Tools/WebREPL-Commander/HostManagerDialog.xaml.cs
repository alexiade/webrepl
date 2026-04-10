using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace WebREPL_Commander;

public partial class HostManagerDialog : Window
{
    private readonly HostManager _hostManager;
    private ObservableCollection<HostConfiguration> _hosts;
    private HostConfiguration? _currentHost;
    private string? _originalHostName;
    private bool _isNewHost;

    public bool ConfigurationsChanged { get; private set; }

    public HostManagerDialog(HostManager hostManager)
    {
        InitializeComponent();
        _hostManager = hostManager;
        _hosts = new ObservableCollection<HostConfiguration>(_hostManager.Hosts);
        lstHosts.ItemsSource = _hosts;

        txtConfigPath.Text = $"Config: {_hostManager.GetConfigPath()}";
    }

    private void LstHosts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstHosts.SelectedItem is HostConfiguration host)
        {
            LoadHostConfiguration(host);
            btnDelete.IsEnabled = true;
        }
        else
        {
            ClearForm();
            btnDelete.IsEnabled = false;
        }
    }

    private void LoadHostConfiguration(HostConfiguration host)
    {
        _currentHost = host;
        _originalHostName = host.Name;
        _isNewHost = false;

        txtName.Text = host.Name;
        txtName.IsEnabled = true; // Allow renaming
        txtHost.Text = host.Host;
        txtPort.Text = host.Port.ToString();
        txtPassword.Password = host.Password;
        txtLocalPath.Text = host.LocalDefaultPath;
        txtRemotePath.Text = string.IsNullOrWhiteSpace(host.RemoteDefaultPath) ? "/" : host.RemoteDefaultPath;

        gridDetails.IsEnabled = true;
    }

    private void ClearForm()
    {
        _currentHost = null;
        _originalHostName = null;
        txtName.Text = "";
        txtHost.Text = "";
        txtPort.Text = "8266";
        txtPassword.Password = "";
        txtLocalPath.Text = "";
        txtRemotePath.Text = "/";
        gridDetails.IsEnabled = false;
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        lstHosts.SelectedItem = null;
        _currentHost = null;
        _originalHostName = null;
        _isNewHost = true;

        txtName.Text = "";
        txtName.IsEnabled = true;
        txtHost.Text = "192.168.4.1";
        txtPort.Text = "8266";
        txtPassword.Password = "";
        txtLocalPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        txtRemotePath.Text = "/";

        gridDetails.IsEnabled = true;
        txtName.Focus();
    }

    private void BtnBrowseLocal_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Local Default Directory",
            InitialDirectory = string.IsNullOrWhiteSpace(txtLocalPath.Text) 
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : txtLocalPath.Text
        };

        if (dialog.ShowDialog() == true)
        {
            txtLocalPath.Text = dialog.FolderName;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("Please enter a name for this configuration", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            txtName.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(txtHost.Text))
        {
            MessageBox.Show("Please enter a host address", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            txtHost.Focus();
            return;
        }

        if (!int.TryParse(txtPort.Text, out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show("Please enter a valid port number (1-65535)", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            txtPort.Focus();
            return;
        }

        try
        {
            var localPath = txtLocalPath.Text.Trim();
            var remotePath = txtRemotePath.Text.Trim();

            // Validate local path if specified
            if (!string.IsNullOrWhiteSpace(localPath) && !Directory.Exists(localPath))
            {
                var result = MessageBox.Show(
                    $"Local directory does not exist: {localPath}\n\nDo you want to save anyway?",
                    "Directory Not Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            // Ensure remote path starts with /
            if (!string.IsNullOrWhiteSpace(remotePath) && !remotePath.StartsWith("/"))
            {
                remotePath = "/" + remotePath;
            }

            if (_isNewHost)
            {
                var newHost = new HostConfiguration(
                    txtName.Text.Trim(), 
                    txtHost.Text.Trim(), 
                    port, 
                    txtPassword.Password,
                    localPath,
                    remotePath);
                _hostManager.Add(newHost);
                _hosts.Add(newHost);
                lstHosts.SelectedItem = newHost;
            }
            else if (_currentHost != null)
            {
                _currentHost.Name = txtName.Text.Trim();
                _currentHost.Host = txtHost.Text.Trim();
                _currentHost.Port = port;
                _currentHost.Password = txtPassword.Password;
                _currentHost.LocalDefaultPath = localPath;
                _currentHost.RemoteDefaultPath = remotePath;
                _hostManager.Update(_currentHost, _originalHostName);

                // Refresh the list view
                var index = _hosts.IndexOf(_currentHost);
                if (index >= 0)
                {
                    _hosts[index] = _currentHost;
                }
            }

            ConfigurationsChanged = true;
            ClearForm();

            MessageBox.Show("Configuration saved successfully", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (lstHosts.SelectedItem is HostConfiguration host)
        {
            LoadHostConfiguration(host);
        }
        else
        {
            ClearForm();
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (lstHosts.SelectedItem is not HostConfiguration host)
            return;

        var result = MessageBox.Show(
            $"Delete configuration '{host.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _hostManager.Remove(host.Name);
                _hosts.Remove(host);
                ConfigurationsChanged = true;
                ClearForm();

                MessageBox.Show("Configuration deleted successfully", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting configuration: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
