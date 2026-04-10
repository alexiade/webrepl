using System;
using System.Windows;
using System.Windows.Controls;

namespace WebREPL_KilnPresets;

public partial class HostManagerDialog : Window
{
    private readonly HostManager _hostManager;

    public HostManagerDialog(HostManager hostManager)
    {
        InitializeComponent();
        _hostManager = hostManager;
        LoadHosts();
    }

    private void LoadHosts()
    {
        _hostManager.Load();
        HostsList.ItemsSource = null;
        HostsList.ItemsSource = _hostManager.Hosts;
    }

    private void HostsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = HostsList.SelectedItem != null;
        EditButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HostEditDialog(null);
        if (dialog.ShowDialog() == true && dialog.Host != null)
        {
            try
            {
                _hostManager.Add(dialog.Host);
                LoadHosts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (HostsList.SelectedItem is HostConfiguration host)
        {
            var originalName = host.Name;
            var dialog = new HostEditDialog(host);
            if (dialog.ShowDialog() == true && dialog.Host != null)
            {
                try
                {
                    _hostManager.Update(dialog.Host, originalName);
                    LoadHosts();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (HostsList.SelectedItem is HostConfiguration host)
        {
            var result = MessageBox.Show($"Are you sure you want to delete '{host.Name}'?", 
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _hostManager.Delete(host.Name);
                    LoadHosts();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
