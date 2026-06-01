using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace JellybeansPicUploader.Views.Dialogs;

public partial class TextPromptDialog : Window
{
    public TextPromptDialog()
    {
        InitializeComponent();
    }

    public TextPromptDialog(string prompt, string defaultText = "") : this()
    {
        PromptTextBlock.Text = prompt;
        InputTextBox.Text = defaultText;
        Opened += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    public async Task<string?> ShowDialogAsync(Window owner)
    {
        var result = await ShowDialog<string?>(owner);
        return result;
    }

    private void ConfirmButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        Close(InputTextBox.Text);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        Close(null);
    }

    private void InputTextBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter)
        {
            ConfirmButton_OnClick(sender, eventArgs);
            eventArgs.Handled = true;
        }
    }
}
