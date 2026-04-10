using System.Windows;

namespace WebREPL_Commander;

public partial class InputDialog : Window
{
    public string ResponseText => txtInput.Text;

    public InputDialog(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        txtPrompt.Text = prompt;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
