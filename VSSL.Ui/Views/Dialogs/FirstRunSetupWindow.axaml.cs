using Avalonia.Controls;

namespace VSSL.Ui.Views.Dialogs;

public partial class FirstRunSetupWindow : Window
{
    private readonly List<OptionItem> _themeOptions =
    [
        new("Dark", "深色 / Dark"),
        new("Light", "浅色 / Light")
    ];

    private readonly List<OptionItem> _languageOptions =
    [
        new("zh-CN", "简体中文"),
        new("en-US", "English")
    ];

    public bool IsDarkMode { get; private set; } = true;

    public string Language { get; private set; } = "en-US";

    public FirstRunSetupWindow() : this(defaultIsDarkMode: true, defaultLanguage: "en-US")
    {
    }

    public FirstRunSetupWindow(bool defaultIsDarkMode, string defaultLanguage)
    {
        InitializeComponent();

        ThemeComboBox.ItemsSource = _themeOptions;
        ThemeComboBox.SelectedItem = _themeOptions.FirstOrDefault(option =>
            string.Equals(option.Value, defaultIsDarkMode ? "Dark" : "Light", StringComparison.OrdinalIgnoreCase))
            ?? _themeOptions[0];

        LanguageComboBox.ItemsSource = _languageOptions;
        LanguageComboBox.SelectedItem = _languageOptions.FirstOrDefault(option =>
            string.Equals(option.Value, defaultLanguage, StringComparison.OrdinalIgnoreCase))
            ?? _languageOptions[0];
    }

    private void OnConfirmClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is OptionItem selectedTheme)
            IsDarkMode = string.Equals(selectedTheme.Value, "Dark", StringComparison.OrdinalIgnoreCase);

        if (LanguageComboBox.SelectedItem is OptionItem selectedLanguage)
            Language = selectedLanguage.Value;

        Close(true);
    }

    private sealed class OptionItem(string value, string text)
    {
        public string Value { get; } = value;

        public string Text { get; } = text;

        public override string ToString()
        {
            return Text;
        }
    }
}
