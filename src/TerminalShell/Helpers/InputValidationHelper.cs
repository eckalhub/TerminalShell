using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using TextBox = System.Windows.Controls.TextBox;
using DataObject = System.Windows.DataObject;

namespace TerminalShell.Helpers;

public enum TextBoxValidationMode
{
    None,
    Integer,
    HexColor
}

public static class InputValidationHelper
{
    public static readonly DependencyProperty ValidationModeProperty =
        DependencyProperty.RegisterAttached(
            "ValidationMode",
            typeof(TextBoxValidationMode),
            typeof(InputValidationHelper),
            new PropertyMetadata(TextBoxValidationMode.None, OnValidationModeChanged));

    public static readonly DependencyProperty MinValueProperty =
        DependencyProperty.RegisterAttached(
            "MinValue",
            typeof(double?),
            typeof(InputValidationHelper),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.RegisterAttached(
            "MaxValue",
            typeof(double?),
            typeof(InputValidationHelper),
            new PropertyMetadata(null));

    private static readonly DependencyProperty LastValidValueProperty =
        DependencyProperty.RegisterAttached(
            "LastValidValue",
            typeof(string),
            typeof(InputValidationHelper),
            new PropertyMetadata(string.Empty));

    public static TextBoxValidationMode GetValidationMode(DependencyObject obj)
    {
        return (TextBoxValidationMode)obj.GetValue(ValidationModeProperty);
    }

    public static void SetValidationMode(DependencyObject obj, TextBoxValidationMode value)
    {
        obj.SetValue(ValidationModeProperty, value);
    }

    public static double? GetMinValue(DependencyObject obj)
    {
        return (double?)obj.GetValue(MinValueProperty);
    }

    public static void SetMinValue(DependencyObject obj, double? value)
    {
        obj.SetValue(MinValueProperty, value);
    }

    public static double? GetMaxValue(DependencyObject obj)
    {
        return (double?)obj.GetValue(MaxValueProperty);
    }

    public static void SetMaxValue(DependencyObject obj, double? value)
    {
        obj.SetValue(MaxValueProperty, value);
    }

    private static string GetLastValidValue(DependencyObject obj)
    {
        return (string)obj.GetValue(LastValidValueProperty);
    }

    private static void SetLastValidValue(DependencyObject obj, string value)
    {
        obj.SetValue(LastValidValueProperty, value);
    }

    private static void OnValidationModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        TextBoxValidationMode oldMode = (TextBoxValidationMode)e.OldValue;
        TextBoxValidationMode newMode = (TextBoxValidationMode)e.NewValue;

        if (oldMode != TextBoxValidationMode.None)
        {
            textBox.PreviewTextInput -= OnPreviewTextInput;
            textBox.TextChanged -= OnTextChanged;
            textBox.GotFocus -= OnGotFocus;
            textBox.LostFocus -= OnLostFocus;
            DataObject.RemovePastingHandler(textBox, OnPaste);
        }

        if (newMode == TextBoxValidationMode.None)
        {
            return;
        }

        InputMethod.SetIsInputMethodEnabled(textBox, false);
        textBox.MaxLines = 1;
        textBox.AcceptsReturn = false;

        try
        {
            ModernWpf.Controls.Primitives.TextBoxHelper.SetIsDeleteButtonVisible(textBox, false);
        }
        catch
        {
        }

        textBox.PreviewTextInput += OnPreviewTextInput;
        textBox.TextChanged += OnTextChanged;
        textBox.GotFocus += OnGotFocus;
        textBox.LostFocus += OnLostFocus;
        DataObject.AddPastingHandler(textBox, OnPaste);
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        e.Handled = !IsCharAllowed(e.Text, GetValidationMode(textBox));
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        string cleaned = StripInvalidChars(textBox.Text, GetValidationMode(textBox));
        if (cleaned == textBox.Text)
        {
            return;
        }

        int caretIndex = textBox.CaretIndex;
        textBox.Text = cleaned;
        textBox.CaretIndex = Math.Min(caretIndex, cleaned.Length);
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        string pastedText = (string)e.DataObject.GetData(typeof(string));
        string currentText = textBox.Text;
        int selectionStart = textBox.SelectionStart;
        int selectionLength = textBox.SelectionLength;
        string resultText = currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, pastedText);

        if (!IsValidInput(resultText, GetValidationMode(textBox)))
        {
            e.CancelCommand();
        }
    }

    private static void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            SetLastValidValue(textBox, textBox.Text);
        }
    }

    private static void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        TextBoxValidationMode mode = GetValidationMode(textBox);
        string text = textBox.Text.Trim();

        if (mode == TextBoxValidationMode.HexColor && !string.IsNullOrEmpty(text) && !text.StartsWith("#", StringComparison.Ordinal))
        {
            text = "#" + text;
        }

        if (!IsValidFinalValue(text, mode, textBox))
        {
            textBox.Text = GetLastValidValue(textBox);
            return;
        }

        if (!string.Equals(textBox.Text, text, StringComparison.Ordinal))
        {
            textBox.Text = text;
        }

        SetLastValidValue(textBox, textBox.Text);
    }

    private static bool IsCharAllowed(string input, TextBoxValidationMode mode)
    {
        return mode switch
        {
            TextBoxValidationMode.Integer => Regex.IsMatch(input, @"^[0-9-]$"),
            TextBoxValidationMode.HexColor => Regex.IsMatch(input, @"^[0-9A-Fa-f#]$"),
            _ => true
        };
    }

    private static string StripInvalidChars(string text, TextBoxValidationMode mode)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return mode switch
        {
            TextBoxValidationMode.Integer => NormalizeIntermediateInteger(text),
            TextBoxValidationMode.HexColor => NormalizeIntermediateHex(text),
            _ => text
        };
    }

    private static string NormalizeIntermediateInteger(string text)
    {
        string cleaned = Regex.Replace(text, @"[^0-9-]", string.Empty);
        bool isNegative = cleaned.StartsWith("-", StringComparison.Ordinal);
        string digits = cleaned.Replace("-", string.Empty);
        return isNegative ? "-" + digits : digits;
    }

    private static string NormalizeIntermediateHex(string text)
    {
        string cleaned = Regex.Replace(text, @"[^0-9A-Fa-f#]", string.Empty);
        bool hadHash = cleaned.Contains('#');
        cleaned = cleaned.Replace("#", string.Empty);
        if (cleaned.Length > 8)
        {
            cleaned = cleaned[..8];
        }

        return hadHash ? "#" + cleaned : cleaned;
    }

    private static bool IsValidInput(string text, TextBoxValidationMode mode)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        return mode switch
        {
            TextBoxValidationMode.Integer => Regex.IsMatch(text, @"^-?\d*$"),
            TextBoxValidationMode.HexColor => Regex.IsMatch(text, @"^#?[0-9A-Fa-f]{0,8}$"),
            _ => true
        };
    }

    private static bool IsValidFinalValue(string text, TextBoxValidationMode mode, TextBox textBox)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        return mode switch
        {
            TextBoxValidationMode.Integer => int.TryParse(text, out int intValue) && CheckRange(intValue, textBox),
            TextBoxValidationMode.HexColor => Regex.IsMatch(text, @"^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$"),
            _ => true
        };
    }

    private static bool CheckRange(double value, TextBox textBox)
    {
        double? min = GetMinValue(textBox);
        double? max = GetMaxValue(textBox);

        if (min.HasValue && value < min.Value)
        {
            return false;
        }

        if (max.HasValue && value > max.Value)
        {
            return false;
        }

        return true;
    }
}
