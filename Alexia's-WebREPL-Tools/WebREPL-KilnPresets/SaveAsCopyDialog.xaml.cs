using System.Windows;
using System.Windows.Input;

namespace WebREPL_KilnPresets;

public partial class SaveAsCopyDialog : Window
{
    public string NewKey { get; private set; } = "";

    public SaveAsCopyDialog(string originalKey)
    {
        InitializeComponent();
        OriginalKeyTextBox.Text = originalKey;
        NewKeyTextBox.Text = originalKey + "_copy";
        NewKeyTextBox.SelectAll();
        NewKeyTextBox.Focus();
    }

    private void NewKeyTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Save_Click(sender, e);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewKeyTextBox.Text))
        {
            MessageBox.Show("New key cannot be empty.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NewKeyTextBox.Focus();
            return;
        }

        NewKey = NewKeyTextBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
