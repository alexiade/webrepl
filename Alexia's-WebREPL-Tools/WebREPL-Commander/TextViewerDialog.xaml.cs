using System.Windows;

namespace WebREPL_Commander;

public partial class TextViewerDialog : Window
{
    public TextViewerDialog(string title, string content)
    {
        InitializeComponent();
        Title = title;
        txtContent.Text = content;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
