using System.Windows;
using System.Windows.Input;
using WebREPL.Core;
using MessageBox = System.Windows.MessageBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WebREPL_Commander;

public partial class TerminalDialog : Window
{
    private readonly WebReplClient _client;

    public TerminalDialog(WebReplClient client)
    {
        InitializeComponent();
        _client = client;
        txtOutput.Text = "WebREPL Terminal - Ready\n";
        txtCommand.Focus();
    }

    private async void BtnSend_Click(object sender, RoutedEventArgs e)
    {
        await SendCommand();
    }

    private async void TxtCommand_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SendCommand();
            e.Handled = true;
        }
    }

    private async Task SendCommand()
    {
        if (string.IsNullOrWhiteSpace(txtCommand.Text)) return;

        var command = txtCommand.Text;
        txtOutput.Text += $">>> {command}\n";
        txtCommand.Clear();

        try
        {
            btnSend.IsEnabled = false;
            txtCommand.IsEnabled = false;

            var result = await _client.ExecuteAsync(command);
            txtOutput.Text += result;
            
            if (!result.EndsWith("\n"))
                txtOutput.Text += "\n";

            scrollViewer.ScrollToEnd();
        }
        catch (Exception ex)
        {
            txtOutput.Text += $"Error: {ex.Message}\n";
        }
        finally
        {
            btnSend.IsEnabled = true;
            txtCommand.IsEnabled = true;
            txtCommand.Focus();
        }
    }

    private async void BtnInterrupt_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _client.InterruptAsync();
            txtOutput.Text += "\n^C Interrupt sent\n";
            scrollViewer.ScrollToEnd();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error sending interrupt: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        txtOutput.Clear();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
