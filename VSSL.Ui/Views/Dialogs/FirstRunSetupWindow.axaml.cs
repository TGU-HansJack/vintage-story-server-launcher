using Avalonia.Controls;
using System.Globalization;

namespace VSSL.Ui.Views.Dialogs;

public partial class FirstRunSetupWindow : Window
{
    private readonly List<OptionItem> _themeOptions;
    private readonly List<OptionItem> _languageOptions;

    public bool IsDarkMode { get; private set; } = true;

    public string Language { get; private set; } = "en-US";

    public FirstRunSetupWindow() : this(defaultIsDarkMode: true, defaultLanguage: "en-US")
    {
    }

    public FirstRunSetupWindow(bool defaultIsDarkMode, string defaultLanguage)
    {
        InitializeComponent();
        var isZh = IsChineseCulture();
        ApplyUiTexts(isZh);

        _themeOptions =
        [
            new("Dark", isZh ? "深色" : "Dark"),
            new("Light", isZh ? "浅色" : "Light")
        ];

        _languageOptions =
        [
            new("zh-CN", isZh ? "简体中文" : "Simplified Chinese"),
            new("en-US", "English")
        ];

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

    private static bool IsChineseCulture()
    {
        return CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyUiTexts(bool isZh)
    {
        Title = isZh ? "首次启动设置" : "First Run Setup";
        TitleTextBlock.Text = isZh ? "欢迎使用 VSSL" : "Welcome to VSSL";
        HintTextBlock.Text = isZh ? "请先选择默认主题和默认语言（仅首次显示）" : "Please choose default theme and language (shown only once).";
        ThemeLabelTextBlock.Text = isZh ? "默认主题" : "Default Theme";
        LanguageLabelTextBlock.Text = isZh ? "默认语言" : "Default Language";
        ConfirmButton.Content = isZh ? "开始使用" : "Get Started";
    }
}
