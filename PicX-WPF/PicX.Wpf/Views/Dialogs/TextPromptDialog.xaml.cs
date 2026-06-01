using System.Windows;
using System.Windows.Input;

namespace PicXWpf.Views.Dialogs;

public partial class TextPromptDialog : Window
{
    public TextPromptDialog(string prompt, string defaultText = "")
    {
        InitializeComponent();
        PromptTextBlock.Text = prompt;
        InputTextBox.Text = defaultText;
        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    public string? EnteredText { get; private set; }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        EnteredText = InputTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputTextBox_OnKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter)
        {
            ConfirmButton_OnClick(sender, eventArgs);
            eventArgs.Handled = true;
        }
    }
}
