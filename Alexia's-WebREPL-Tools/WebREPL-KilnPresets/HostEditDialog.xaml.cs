using System;
using System.Windows;

namespace WebREPL_KilnPresets;

public partial class HostEditDialog : Window
{
    public HostConfiguration? Host { get; private set; }

    public HostEditDialog(HostConfiguration? host)
    {
        InitializeComponent();

        if (host != null)
        {
            NameTextBox.Text = host.Name;
            HostTextBox.Text = host.Host;
            PortTextBox.Text = host.Port.ToString();
            PasswordBox.Password = host.Password;
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(HostTextBox.Text))
        {
            MessageBox.Show("Host is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(PortTextBox.Text, out var port) || port <= 0 || port > 65535)
        {
            MessageBox.Show("Port must be a valid number between 1 and 65535.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Host = new HostConfiguration(
            NameTextBox.Text.Trim(),
            HostTextBox.Text.Trim(),
            port,
            PasswordBox.Password
        );

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
