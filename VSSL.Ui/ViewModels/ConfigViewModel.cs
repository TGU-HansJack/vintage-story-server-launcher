using System.Collections.ObjectModel;
using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.Ui;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     配置页面视图模型
/// </summary>
public partial class ConfigViewModel : ViewModelBase
{
    private static readonly IReadOnlyList<string> ImagePatterns =
    [
        "*.png",
        "*.jpg",
        "*.jpeg",
        "*.webp",
        "*.gif",
        "*.bmp"
    ];

    private readonly IInstanceProfileService? _instanceProfileService;
    private readonly IInstanceServerConfigService? _instanceServerConfigService;
    private readonly IAdvancedJsonDialogService? _advancedJsonDialogService;
    private readonly IFilePickerService? _filePickerService;
    private readonly IServerImageService? _serverImageService;
    private readonly IImagePreviewDialogService? _imagePreviewDialogService;

    [ObservableProperty] private InstanceProfile? _selectedProfile;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _serverName = "Vintage Story Server";
    [ObservableProperty] private string _ip = string.Empty;
    [ObservableProperty] private int _port = 42420;
    [ObservableProperty] private int _maxClients = 16;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _advertiseServer;
    [ObservableProperty] private int _whitelistMode;
    [ObservableProperty] private bool _allowPvP = true;
    [ObservableProperty] private bool _allowFireSpread = true;
    [ObservableProperty] private bool _allowFallingBlocks = true;
    [ObservableProperty] private bool _verifyPlayerAuth = true;
    [ObservableProperty] private string _serverLanguage = "en";
    [ObservableProperty] private string _defaultRoleCode = "suplayer";
    [ObservableProperty] private string _welcomeMessage = string.Empty;

    public ObservableCollection<string> ServerLanguageOptions { get; } = [];

    [ObservableProperty] private string _seed = "123456789";
    [ObservableProperty] private string _worldName = "A new world";
    [ObservableProperty] private string _saveFileLocation = string.Empty;
    [ObservableProperty] private string _playStyle = "surviveandbuild";
    [ObservableProperty] private string _worldType = "standard";
    [ObservableProperty] private int _worldHeight = 256;

    [ObservableProperty] private string _imageRootPath = string.Empty;
    [ObservableProperty] private string _pendingCoverImportPath = string.Empty;
    [ObservableProperty] private string _pendingShowcaseImportPath = string.Empty;
    [ObservableProperty] private ConfigServerImageItemViewModel? _coverImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedShowcaseImage))]
    private ConfigServerImageItemViewModel? _selectedShowcaseImage;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];

    public ObservableCollection<ConfigWorldRuleItemViewModel> WorldRules { get; } = [];

    public ObservableCollection<ConfigServerImageItemViewModel> ShowcaseImages { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasCoverImage => CoverImage is not null;

    public bool HasSelectedShowcaseImage => SelectedShowcaseImage is not null;

    public bool HasShowcaseImages => ShowcaseImages.Count > 0;

    public bool HasNoShowcaseImages => !HasShowcaseImages;

    public string CoverImageDisplayText => CoverImage is null
        ? L("ConfigServerImagesNoCoverText")
        : $"{CoverImage.RelativePath} ({CoverImage.SizeLabel})";

    partial void OnCoverImageChanged(ConfigServerImageItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasCoverImage));
        OnPropertyChanged(nameof(CoverImageDisplayText));
    }

    partial void OnSelectedProfileChanged(InstanceProfile? value)
    {
        _ = LoadSelectedProfileAsync(value);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_instanceProfileService is null) return;

        try
        {
            IsBusy = true;

            var oldSelectedId = SelectedProfile?.Id;
            var profiles = _instanceProfileService.GetProfiles();

            Profiles.Clear();
            foreach (var profile in profiles) Profiles.Add(profile);
            OnPropertyChanged(nameof(HasProfiles));
            OnPropertyChanged(nameof(HasNoProfiles));

            if (Profiles.Count == 0)
            {
                SelectedProfile = null;
                StatusMessage = L("StatusNoProfileCreateFirst");
                return;
            }

            var targetProfile = Profiles.FirstOrDefault(profile =>
                !string.IsNullOrWhiteSpace(oldSelectedId) &&
                profile.Id.Equals(oldSelectedId, StringComparison.OrdinalIgnoreCase));
            targetProfile ??= Profiles[0];

            if (!ReferenceEquals(SelectedProfile, targetProfile))
            {
                SelectedProfile = targetProfile;
            }
            else
            {
                await LoadSelectedProfileAsync(SelectedProfile);
            }

            StatusMessage = LF("StatusLoadedProfilesFormat", Profiles.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("StatusRefreshProfilesFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_instanceProfileService is null || _instanceServerConfigService is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        try
        {
            IsBusy = true;
            var saveFile = string.IsNullOrWhiteSpace(SaveFileLocation)
                ? _instanceProfileService.GetDefaultSaveFilePath(profile.Id)
                : Path.GetFullPath(SaveFileLocation.Trim());

            var serverSettings = new ServerCommonSettings
            {
                ServerName = ServerName.Trim(),
                Ip = string.IsNullOrWhiteSpace(Ip) ? null : Ip.Trim(),
                Port = Port,
                MaxClients = MaxClients,
                Password = string.IsNullOrWhiteSpace(Password) ? null : Password.Trim(),
                AdvertiseServer = AdvertiseServer,
                WhitelistMode = WhitelistMode,
                AllowPvP = AllowPvP,
                AllowFireSpread = AllowFireSpread,
                AllowFallingBlocks = AllowFallingBlocks,
                VerifyPlayerAuth = VerifyPlayerAuth,
                ServerLanguage = ServerLanguage.Trim(),
                DefaultRoleCode = DefaultRoleCode.Trim(),
                WelcomeMessage = WelcomeMessage.Trim()
            };

            var worldSettings = new WorldSettings
            {
                Seed = Seed.Trim(),
                WorldName = WorldName.Trim(),
                SaveFileLocation = saveFile,
                PlayStyle = PlayStyle.Trim(),
                WorldType = WorldType.Trim(),
                WorldHeight = WorldHeight
            };

            var rules = WorldRules.Select(item =>
            {
                var definition = WorldRuleCatalog.DefaultRules.FirstOrDefault(rule => rule.Key == item.Key)
                                 ?? new WorldRuleDefinition
                                 {
                                     Key = item.Key,
                                     LabelZh = item.LabelZh,
                                     LabelEn = item.LabelEn,
                                     Type = item.Type
                                 };

                return new WorldRuleValue
                {
                    Definition = definition,
                    Value = item.Value
                };
            }).ToList();

            await _instanceServerConfigService.SaveSettingsAsync(profile, serverSettings, worldSettings, rules);

            profile.ActiveSaveFile = saveFile;
            profile.SaveDirectory = Path.GetDirectoryName(saveFile) ?? string.Empty;
            profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
            _instanceProfileService.UpdateProfile(profile);

            SaveFileLocation = saveFile;
            StatusMessage = L("ConfigStatusSaved");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ConfigStatusSaveFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EditAdvancedJsonAsync()
    {
        if (_instanceServerConfigService is null || _advancedJsonDialogService is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        try
        {
            IsBusy = true;
            var rawJson = await _instanceServerConfigService.LoadRawJsonAsync(profile);
            var editedJson = await _advancedJsonDialogService.ShowEditorAsync(L("ConfigAdvancedJsonDialogTitle"), rawJson);
            if (editedJson is null)
            {
                StatusMessage = L("ConfigStatusAdvancedJsonCanceled");
                return;
            }

            await _instanceServerConfigService.SaveRawJsonAsync(profile, editedJson);
            await LoadSelectedProfileAsync(profile);
            StatusMessage = L("ConfigStatusAdvancedJsonSaved");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ConfigStatusAdvancedJsonSaveFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportServerConfigAsync()
    {
        if (_instanceServerConfigService is null || _filePickerService is null || _instanceProfileService is null)
        {
            return;
        }

        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        var selected = await _filePickerService.PickSingleFileAsync(
            L("ConfigImportDialogTitle"),
            L("ConfigImportFilterName"),
            ["*.json"]);
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _instanceServerConfigService.ImportRawJsonAsync(profile, selected);

            var worldSettings = await _instanceServerConfigService.LoadWorldSettingsAsync(profile);
            profile.ActiveSaveFile = worldSettings.SaveFileLocation;
            profile.SaveDirectory = Path.GetDirectoryName(worldSettings.SaveFileLocation) ?? profile.SaveDirectory;
            profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
            _instanceProfileService.UpdateProfile(profile);

            await LoadSelectedProfileAsync(profile);
            StatusMessage = LF("ConfigStatusImportedFormat", Path.GetFileName(selected));
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ConfigStatusImportFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BrowseCoverImageAsync()
    {
        if (_filePickerService is null)
        {
            return;
        }

        var selected = await _filePickerService.PickSingleFileAsync(
            L("ConfigImageBrowseDialogTitle"),
            L("ConfigImageBrowseFilterName"),
            ImagePatterns);
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        PendingCoverImportPath = selected;
        StatusMessage = LF("ConfigImageStatusSelectedImportFileFormat", Path.GetFileName(selected));
    }

    [RelayCommand]
    private async Task ImportCoverImageAsync()
    {
        if (_serverImageService is null)
        {
            return;
        }

        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        var sourcePath = PendingCoverImportPath;
        if (!TryValidateImportSource(sourcePath, out var validatedSourcePath, out var error))
        {
            if (_filePickerService is null)
            {
                StatusMessage = error;
                return;
            }

            sourcePath = await _filePickerService.PickSingleFileAsync(
                L("ConfigImageBrowseDialogTitle"),
                L("ConfigImageBrowseFilterName"),
                ImagePatterns);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }

            if (!TryValidateImportSource(sourcePath, out validatedSourcePath, out error))
            {
                StatusMessage = error;
                return;
            }
        }

        try
        {
            IsBusy = true;
            var imported = await _serverImageService.ImportImageAsync(profile, validatedSourcePath, ServerImageKind.Cover);
            await ReloadServerImagesAsync(profile);
            PendingCoverImportPath = validatedSourcePath;
            StatusMessage = LF("ConfigImageStatusCoverImportedFormat", imported.FileName);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ConfigStatusSaveFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteCoverImageAsync()
    {
        if (_serverImageService is null)
        {
            return;
        }

        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        if (CoverImage is null)
        {
            StatusMessage = L("ConfigServerImagesNoCoverText");
            return;
        }

        try
        {
            IsBusy = true;
            await _serverImageService.DeleteImageAsync(profile, ToDomainImageInfo(CoverImage, ServerImageKind.Cover));
            await ReloadServerImagesAsync(profile);
            StatusMessage = L("ConfigImageStatusCoverDeleted");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ConfigStatusSaveFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviewCoverImageAsync()
    {
        if (_imagePreviewDialogService is null)
        {
            return;
        }

        if (CoverImage is null || !File.Exists(CoverImage.FullPath))
        {
            StatusMessage = L("ConfigServerImagesNoCoverText");
            return;
        }

        try
        {
            await _imagePreviewDialogService.ShowAsync(
                LF("ConfigImagePreviewDialogTitleFormat", CoverImage.FileName),
                CoverImage.FullPath);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ConfigImageStatusPreviewFailedFormat", ex.Message);
        }
    }

    [RelayCommand]
    private async Task BrowseShowcaseImageAsync()
    {
        if (_filePickerService is null)
        {
            return;
        }

        var selected = await _filePickerService.PickSingleFileAsync(
            L("ConfigImageBrowseDialogTitle"),
            L("ConfigImageBrowseFilterName"),
            ImagePatterns);
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        PendingShowcaseImportPath = selected;
        StatusMessage = LF("ConfigImageStatusSelectedImportFileFormat", Path.GetFileName(selected));
    }

    [RelayCommand]
    private async Task AddShowcaseImageAsync()
    {
        if (_serverImageService is null)
        {
            return;
        }

        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        var sourcePath = PendingShowcaseImportPath;
        if (!TryValidateImportSource(sourcePath, out var validatedSourcePath, out var error))
        {
            if (_filePickerService is null)
            {
                StatusMessage = error;
                return;
            }

            sourcePath = await _filePickerService.PickSingleFileAsync(
                L("ConfigImageBrowseDialogTitle"),
                L("ConfigImageBrowseFilterName"),
                ImagePatterns);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }

            if (!TryValidateImportSource(sourcePath, out validatedSourcePath, out error))
            {
                StatusMessage = error;
                return;
            }
        }

        try
        {
            IsBusy = true;
            var imported = await _serverImageService.ImportImageAsync(profile, validatedSourcePath, ServerImageKind.Showcase);
            await ReloadServerImagesAsync(profile);
            PendingShowcaseImportPath = validatedSourcePath;
            StatusMessage = LF("ConfigImageStatusShowcaseAddedFormat", imported.FileName);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ConfigStatusSaveFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportShowcaseFolderAsync()
    {
        if (_filePickerService is null || _serverImageService is null)
        {
            return;
        }

        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        var folder = await _filePickerService.PickFolderAsync(L("ConfigImageImportFolderDialogTitle"));
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedImagePath)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0)
        {
            StatusMessage = L("ConfigImageStatusNoImportFiles");
            return;
        }

        try
        {
            IsBusy = true;
            var importedCount = 0;
            foreach (var file in files)
            {
                await _serverImageService.ImportImageAsync(profile, file, ServerImageKind.Showcase);
                importedCount++;
            }

            await ReloadServerImagesAsync(profile);
            StatusMessage = LF("ConfigImageStatusShowcaseImportedFormat", importedCount);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ConfigStatusSaveFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteShowcaseImageAsync(ConfigServerImageItemViewModel? image)
    {
        if (_serverImageService is null)
        {
            return;
        }

        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        var selected = image ?? SelectedShowcaseImage;
        if (selected is null)
        {
            StatusMessage = L("ConfigImageStatusSelectShowcaseFirst");
            return;
        }

        try
        {
            IsBusy = true;
            await _serverImageService.DeleteImageAsync(profile, ToDomainImageInfo(selected, ServerImageKind.Showcase));
            await ReloadServerImagesAsync(profile);
            StatusMessage = LF("ConfigImageStatusShowcaseDeletedFormat", selected.FileName);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ConfigStatusSaveFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviewShowcaseImageAsync(ConfigServerImageItemViewModel? image)
    {
        if (_imagePreviewDialogService is null)
        {
            return;
        }

        var selected = image ?? SelectedShowcaseImage;
        if (selected is null || !File.Exists(selected.FullPath))
        {
            StatusMessage = L("ConfigImageStatusSelectShowcaseFirst");
            return;
        }

        try
        {
            await _imagePreviewDialogService.ShowAsync(
                LF("ConfigImagePreviewDialogTitleFormat", selected.FileName),
                selected.FullPath);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ConfigImageStatusPreviewFailedFormat", ex.Message);
        }
    }

    private async Task LoadSelectedProfileAsync(InstanceProfile? selectedProfile)
    {
        if (_instanceServerConfigService is null || _instanceProfileService is null) return;
        if (selectedProfile is null)
        {
            ClearForm();
            return;
        }

        var profile = _instanceProfileService.GetProfileById(selectedProfile.Id) ?? selectedProfile;

        try
        {
            IsBusy = true;
            var serverSettings = await _instanceServerConfigService.LoadServerSettingsAsync(profile);
            var worldSettings = await _instanceServerConfigService.LoadWorldSettingsAsync(profile);
            var rules = await _instanceServerConfigService.LoadWorldRulesAsync(profile);

            ServerName = serverSettings.ServerName;
            Ip = serverSettings.Ip ?? string.Empty;
            Port = serverSettings.Port;
            MaxClients = serverSettings.MaxClients;
            Password = serverSettings.Password ?? string.Empty;
            AdvertiseServer = serverSettings.AdvertiseServer;
            WhitelistMode = serverSettings.WhitelistMode;
            AllowPvP = serverSettings.AllowPvP;
            AllowFireSpread = serverSettings.AllowFireSpread;
            AllowFallingBlocks = serverSettings.AllowFallingBlocks;
            VerifyPlayerAuth = serverSettings.VerifyPlayerAuth;
            ServerLanguage = serverSettings.ServerLanguage;
            DefaultRoleCode = serverSettings.DefaultRoleCode;
            WelcomeMessage = serverSettings.WelcomeMessage;

            Seed = worldSettings.Seed;
            WorldName = worldSettings.WorldName;
            SaveFileLocation = worldSettings.SaveFileLocation;
            PlayStyle = worldSettings.PlayStyle;
            WorldType = worldSettings.WorldType;
            WorldHeight = worldSettings.WorldHeight ?? 256;

            WorldRules.Clear();
            foreach (var rule in rules)
            {
                WorldRules.Add(new ConfigWorldRuleItemViewModel
                {
                    Key = rule.Definition.Key,
                    LabelZh = rule.Definition.LabelZh,
                    LabelEn = rule.Definition.LabelEn,
                    Type = rule.Definition.Type,
                    Choices = rule.Definition.Choices,
                    DescriptionZh = rule.Definition.DescriptionZh,
                    DescriptionEn = rule.Definition.DescriptionEn,
                    Value = rule.Value ?? string.Empty
                });
            }

            await ReloadServerImagesAsync(profile);
            StatusMessage = LF("ConfigStatusLoadedProfileConfigFormat", profile.Name);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ConfigStatusLoadFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadServerImagesAsync(InstanceProfile profile)
    {
        if (_serverImageService is null)
        {
            ClearImageForm();
            return;
        }

        ImageRootPath = _serverImageService.GetImageRootPath(profile);
        var images = await _serverImageService.LoadServerImagesAsync(profile);

        var cover = images.FirstOrDefault(image => image.Kind == ServerImageKind.Cover);
        CoverImage = cover is null ? null : ToViewModel(cover);

        var oldSelectedPath = SelectedShowcaseImage?.FullPath;
        ShowcaseImages.Clear();
        foreach (var info in images.Where(image => image.Kind == ServerImageKind.Showcase))
        {
            ShowcaseImages.Add(ToViewModel(info));
        }

        SelectedShowcaseImage = ShowcaseImages.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(oldSelectedPath) &&
            item.FullPath.Equals(oldSelectedPath, StringComparison.OrdinalIgnoreCase));

        OnPropertyChanged(nameof(HasShowcaseImages));
        OnPropertyChanged(nameof(HasNoShowcaseImages));
    }

    private void ClearForm()
    {
        ServerName = "Vintage Story Server";
        Ip = string.Empty;
        Port = 42420;
        MaxClients = 16;
        Password = string.Empty;
        AdvertiseServer = false;
        WhitelistMode = 0;
        AllowPvP = true;
        AllowFireSpread = true;
        AllowFallingBlocks = true;
        VerifyPlayerAuth = true;
        ServerLanguage = "en";
        DefaultRoleCode = "suplayer";
        WelcomeMessage = string.Empty;
        Seed = "123456789";
        WorldName = "A new world";
        SaveFileLocation = string.Empty;
        PlayStyle = "surviveandbuild";
        WorldType = "standard";
        WorldHeight = 256;
        WorldRules.Clear();
        ClearImageForm();
    }

    private void ClearImageForm()
    {
        ImageRootPath = string.Empty;
        PendingCoverImportPath = string.Empty;
        PendingShowcaseImportPath = string.Empty;
        CoverImage = null;
        ShowcaseImages.Clear();
        SelectedShowcaseImage = null;
        OnPropertyChanged(nameof(HasShowcaseImages));
        OnPropertyChanged(nameof(HasNoShowcaseImages));
    }

    private bool TryValidateImportSource(string? rawPath, out string sourcePath, out string error)
    {
        sourcePath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            error = L("ConfigImageStatusSelectFileFirst");
            return false;
        }

        sourcePath = Path.GetFullPath(rawPath.Trim());
        if (!File.Exists(sourcePath))
        {
            error = L("ConfigImageStatusFileNotFound");
            return false;
        }

        if (!IsSupportedImagePath(sourcePath))
        {
            error = L("ConfigImageStatusUnsupportedFormat");
            return false;
        }

        return true;
    }

    private static bool IsSupportedImagePath(string path)
    {
        var ext = Path.GetExtension(path) ?? string.Empty;
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private static ConfigServerImageItemViewModel ToViewModel(ServerImageFileInfo image)
    {
        return new ConfigServerImageItemViewModel
        {
            Kind = image.Kind,
            FullPath = image.FullPath,
            RelativePath = image.RelativePath,
            FileName = image.FileName,
            SizeBytes = image.SizeBytes
        };
    }

    private static ServerImageFileInfo ToDomainImageInfo(ConfigServerImageItemViewModel image, ServerImageKind kind)
    {
        return new ServerImageFileInfo
        {
            Kind = kind,
            FullPath = image.FullPath,
            RelativePath = image.RelativePath,
            FileName = image.FileName,
            SizeBytes = image.SizeBytes,
            LastWriteUtc = DateTimeOffset.UtcNow
        };
    }

    private void EnsureServerLanguageOptions()
    {
        if (ServerLanguageOptions.Count > 0) return;

        foreach (var code in new[]
                 {
                     "en", "ar", "be", "cs", "da", "de", "es-es", "fr", "hu", "is", "it", "ja", "ko",
                     "nl", "no", "pl", "pt-br", "pt-pt", "ru", "sr", "zh-cn", "zh-tw"
                 })
        {
            ServerLanguageOptions.Add(code);
        }
    }

    #region Constructors

    public ConfigViewModel()
    {
        EnsureServerLanguageOptions();
    }

    public ConfigViewModel(
        IInstanceProfileService instanceProfileService,
        IInstanceServerConfigService instanceServerConfigService,
        IAdvancedJsonDialogService advancedJsonDialogService,
        IFilePickerService filePickerService,
        IServerImageService serverImageService,
        IImagePreviewDialogService imagePreviewDialogService)
    {
        _instanceProfileService = instanceProfileService;
        _instanceServerConfigService = instanceServerConfigService;
        _advancedJsonDialogService = advancedJsonDialogService;
        _filePickerService = filePickerService;
        _serverImageService = serverImageService;
        _imagePreviewDialogService = imagePreviewDialogService;
        EnsureServerLanguageOptions();

        _ = RefreshAsync();
    }

    #endregion
}
