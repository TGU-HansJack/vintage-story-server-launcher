using System.Collections.ObjectModel;
using System.Globalization;
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

    private static readonly HashSet<string> OnlyDuringWorldCreateRuleKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "worldWidth",
        "worldLength"
    };

    private static readonly IReadOnlyList<(string Value, string LabelZh, string LabelEn)> BuiltInPlayStyleDefinitions =
    [
        ("surviveandbuild", "标准", "Standard"),
        ("exploration", "探索", "Exploration"),
        ("wildernesssurvival", "荒野求生", "Wilderness Survival"),
        ("homosapiens", "智人", "Homo sapiens"),
        ("creativebuilding", "超平坦创造模式", "Creative Building")
    ];

    private static readonly IReadOnlyList<(string Value, string LabelZh, string LabelEn)> BuiltInWorldTypeDefinitions =
    [
        ("standard", "标准地形", "Standard"),
        ("superflat", "超平坦", "Superflat")
    ];

    private readonly IInstanceProfileService? _instanceProfileService;
    private readonly IInstanceServerConfigService? _instanceServerConfigService;
    private readonly IInstanceSaveService? _instanceSaveService;
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
    [ObservableProperty] private string _serverLanguage = ResolveDefaultServerLanguage();
    [ObservableProperty] private string _defaultRoleCode = "suplayer";
    [ObservableProperty] private string _welcomeMessage = string.Empty;

    public ObservableCollection<string> ServerLanguageOptions { get; } = [];

    [ObservableProperty] private string _seed = "123456789";
    [ObservableProperty] private string _worldName = "A new world";
    [ObservableProperty] private SaveFileItemViewModel? _selectedSave;
    [ObservableProperty] private string _saveFileLocation = string.Empty;
    [ObservableProperty] private string _playStyle = "surviveandbuild";
    [ObservableProperty] private string _worldType = "standard";
    [ObservableProperty] private int _worldHeight = 256;
    [ObservableProperty] private ConfigChoiceOptionViewModel? _selectedPlayStyleOption;
    [ObservableProperty] private ConfigChoiceOptionViewModel? _selectedWorldTypeOption;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditWorldGenerationSettings))]
    private bool _isWorldGenerated;

    [ObservableProperty] private string _imageRootPath = string.Empty;
    [ObservableProperty] private string _pendingCoverImportPath = string.Empty;
    [ObservableProperty] private string _pendingShowcaseImportPath = string.Empty;
    [ObservableProperty] private ConfigServerImageItemViewModel? _coverImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedShowcaseImage))]
    private ConfigServerImageItemViewModel? _selectedShowcaseImage;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];

    public ObservableCollection<ConfigWorldRuleItemViewModel> WorldRules { get; } = [];
    public ObservableCollection<SaveFileItemViewModel> Saves { get; } = [];
    public ObservableCollection<ConfigChoiceOptionViewModel> PlayStyleOptions { get; } = [];
    public ObservableCollection<ConfigChoiceOptionViewModel> WorldTypeOptions { get; } = [];

    public ObservableCollection<ConfigServerImageItemViewModel> ShowcaseImages { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasSaves => Saves.Count > 0;

    public bool HasNoSaves => !HasSaves;

    public bool HasCoverImage => CoverImage is not null;

    public bool HasSelectedShowcaseImage => SelectedShowcaseImage is not null;

    public bool HasShowcaseImages => ShowcaseImages.Count > 0;

    public bool HasNoShowcaseImages => !HasShowcaseImages;

    public bool CanEditWorldGenerationSettings => !IsWorldGenerated;

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

    partial void OnSelectedSaveChanged(SaveFileItemViewModel? value)
    {
        if (value is not null)
        {
            SaveFileLocation = value.FullPath;
        }

        UpdateWorldGeneratedState();
    }

    partial void OnSaveFileLocationChanged(string value)
    {
        UpdateWorldGeneratedState();
    }

    partial void OnSelectedPlayStyleOptionChanged(ConfigChoiceOptionViewModel? value)
    {
        if (value is null) return;
        if (value.Value.Equals(PlayStyle, StringComparison.OrdinalIgnoreCase)) return;

        PlayStyle = value.Value;
    }

    partial void OnPlayStyleChanged(string value)
    {
        SyncSelectedPlayStyleOption();
    }

    partial void OnSelectedWorldTypeOptionChanged(ConfigChoiceOptionViewModel? value)
    {
        if (value is null) return;
        if (value.Value.Equals(WorldType, StringComparison.OrdinalIgnoreCase)) return;

        WorldType = value.Value;
    }

    partial void OnWorldTypeChanged(string value)
    {
        SyncSelectedWorldTypeOption();
    }

    partial void OnIsWorldGeneratedChanged(bool value)
    {
        UpdateWorldRuleEditability();
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
        if (_instanceProfileService is null || _instanceServerConfigService is null || _instanceSaveService is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        try
        {
            IsBusy = true;
            var selectedSaveFile = SelectedSave?.FullPath;
            var saveFile = string.IsNullOrWhiteSpace(selectedSaveFile)
                ? (string.IsNullOrWhiteSpace(SaveFileLocation)
                    ? _instanceProfileService.GetDefaultSaveFilePath(profile.Id)
                    : Path.GetFullPath(SaveFileLocation.Trim()))
                : Path.GetFullPath(selectedSaveFile);

            await _instanceSaveService.SetActiveSaveAsync(profile, saveFile);

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
                                     Type = item.Type,
                                     Choices = item.Choices,
                                     ChoiceNames = item.ChoiceNames
                                 };

                return new WorldRuleValue
                {
                    Definition = definition,
                    Value = item.Value
                };
            }).ToList();

            if (IsWorldGenerated)
            {
                var persistedWorldSettings = await _instanceServerConfigService.LoadWorldSettingsAsync(profile);
                var persistedRules = await _instanceServerConfigService.LoadWorldRulesAsync(profile);
                var persistedRuleValues = persistedRules.ToDictionary(
                    rule => rule.Definition.Key,
                    rule => rule.Value ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

                worldSettings.Seed = persistedWorldSettings.Seed;
                worldSettings.PlayStyle = persistedWorldSettings.PlayStyle;
                worldSettings.WorldType = persistedWorldSettings.WorldType;
                worldSettings.WorldHeight = persistedWorldSettings.WorldHeight ?? worldSettings.WorldHeight;

                foreach (var rule in rules)
                {
                    if (!IsOnlyDuringWorldCreateRule(rule.Definition.Key)) continue;
                    if (!persistedRuleValues.TryGetValue(rule.Definition.Key, out var persistedValue)) continue;

                    rule.Value = persistedValue;
                }
            }

            await _instanceServerConfigService.SaveSettingsAsync(profile, serverSettings, worldSettings, rules);

            profile.ActiveSaveFile = saveFile;
            profile.SaveDirectory = Path.GetDirectoryName(saveFile) ?? string.Empty;
            profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
            _instanceProfileService.UpdateProfile(profile);

            SaveFileLocation = saveFile;
            await LoadSavesAsync(profile);
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
            SyncSelectedPlayStyleOption();
            SyncSelectedWorldTypeOption();

            await LoadSavesAsync(profile);
            if (Saves.Count == 0)
            {
                var defaultSavePath = _instanceProfileService.GetDefaultSaveFilePath(profile.Id);
                var defaultDirectory = Path.GetDirectoryName(defaultSavePath);
                Saves.Add(new SaveFileItemViewModel
                {
                    FullPath = defaultSavePath,
                    FileName = Path.GetFileName(defaultSavePath),
                    SizeBytes = 0,
                    LastWriteTimeUtc = profile.LastUpdatedUtc
                });
                profile.ActiveSaveFile = defaultSavePath;
                profile.SaveDirectory = defaultDirectory ?? profile.SaveDirectory;
                profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
                _instanceProfileService.UpdateProfile(profile);
            }

            SelectedSave = Saves.FirstOrDefault(item =>
                               item.FullPath.Equals(worldSettings.SaveFileLocation, StringComparison.OrdinalIgnoreCase))
                           ?? Saves.FirstOrDefault(item =>
                               item.FullPath.Equals(profile.ActiveSaveFile, StringComparison.OrdinalIgnoreCase))
                           ?? Saves.FirstOrDefault();
            SaveFileLocation = SelectedSave?.FullPath ?? worldSettings.SaveFileLocation;

            WorldRules.Clear();
            foreach (var rule in rules)
            {
                var ruleValue = rule.Value ?? string.Empty;
                WorldRules.Add(new ConfigWorldRuleItemViewModel
                {
                    Key = rule.Definition.Key,
                    LabelZh = rule.Definition.LabelZh,
                    LabelEn = rule.Definition.LabelEn,
                    Type = rule.Definition.Type,
                    Choices = rule.Definition.Choices,
                    ChoiceNames = rule.Definition.ChoiceNames,
                    ChoiceOptions = BuildRuleChoiceOptions(rule.Definition, ruleValue),
                    IsOnlyDuringWorldCreate = IsOnlyDuringWorldCreateRule(rule.Definition.Key),
                    CanEdit = true,
                    DescriptionZh = rule.Definition.DescriptionZh,
                    DescriptionEn = rule.Definition.DescriptionEn,
                    Value = ruleValue
                });
            }

            UpdateWorldGeneratedState();

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
        ServerLanguage = ResolveDefaultServerLanguage();
        DefaultRoleCode = "suplayer";
        WelcomeMessage = string.Empty;
        Seed = "123456789";
        WorldName = "A new world";
        SelectedSave = null;
        SaveFileLocation = string.Empty;
        PlayStyle = "surviveandbuild";
        WorldType = "standard";
        WorldHeight = 256;
        SyncSelectedPlayStyleOption();
        SyncSelectedWorldTypeOption();
        IsWorldGenerated = false;
        Saves.Clear();
        OnPropertyChanged(nameof(HasSaves));
        OnPropertyChanged(nameof(HasNoSaves));
        WorldRules.Clear();
        ClearImageForm();
    }

    private async Task LoadSavesAsync(InstanceProfile profile)
    {
        Saves.Clear();

        if (_instanceSaveService is null)
        {
            OnPropertyChanged(nameof(HasSaves));
            OnPropertyChanged(nameof(HasNoSaves));
            UpdateWorldGeneratedState();
            return;
        }

        var saveEntries = await _instanceSaveService.GetSavesAsync(profile);
        foreach (var saveEntry in saveEntries)
        {
            Saves.Add(new SaveFileItemViewModel
            {
                FullPath = saveEntry.FullPath,
                FileName = saveEntry.FileName,
                SizeBytes = saveEntry.SizeBytes,
                LastWriteTimeUtc = saveEntry.LastWriteTimeUtc,
                IsActive = saveEntry.FullPath.Equals(profile.ActiveSaveFile, StringComparison.OrdinalIgnoreCase)
            });
        }

        OnPropertyChanged(nameof(HasSaves));
        OnPropertyChanged(nameof(HasNoSaves));
        UpdateWorldGeneratedState();
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

    private void EnsureWorldSelectionOptions()
    {
        if (PlayStyleOptions.Count == 0)
        {
            foreach (var (value, zh, en) in BuiltInPlayStyleDefinitions)
            {
                PlayStyleOptions.Add(new ConfigChoiceOptionViewModel
                {
                    Value = value,
                    LabelZh = zh,
                    LabelEn = en
                });
            }
        }

        if (WorldTypeOptions.Count == 0)
        {
            foreach (var (value, zh, en) in BuiltInWorldTypeDefinitions)
            {
                WorldTypeOptions.Add(new ConfigChoiceOptionViewModel
                {
                    Value = value,
                    LabelZh = zh,
                    LabelEn = en
                });
            }
        }
    }

    private void SyncSelectedPlayStyleOption()
    {
        EnsureWorldSelectionOptions();
        EnsureChoiceOptionExists(PlayStyleOptions, PlayStyle);
        SelectedPlayStyleOption = PlayStyleOptions.FirstOrDefault(option =>
            option.Value.Equals(PlayStyle, StringComparison.OrdinalIgnoreCase));
    }

    private void SyncSelectedWorldTypeOption()
    {
        EnsureWorldSelectionOptions();
        EnsureChoiceOptionExists(WorldTypeOptions, WorldType);
        SelectedWorldTypeOption = WorldTypeOptions.FirstOrDefault(option =>
            option.Value.Equals(WorldType, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureChoiceOptionExists(ObservableCollection<ConfigChoiceOptionViewModel> options, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var normalized = value.Trim();
        if (options.Any(option => option.Value.Equals(normalized, StringComparison.OrdinalIgnoreCase))) return;

        options.Add(new ConfigChoiceOptionViewModel
        {
            Value = normalized,
            LabelZh = $"自定义：{normalized}",
            LabelEn = $"Custom: {normalized}"
        });
    }

    private IReadOnlyList<ConfigChoiceOptionViewModel> BuildRuleChoiceOptions(WorldRuleDefinition definition, string currentValue)
    {
        if (definition.Choices.Count == 0) return [];

        var options = new List<ConfigChoiceOptionViewModel>(definition.Choices.Count + 1);
        for (var index = 0; index < definition.Choices.Count; index++)
        {
            var value = definition.Choices[index];
            var choiceName = index < definition.ChoiceNames.Count
                ? definition.ChoiceNames[index]
                : value;
            options.Add(CreateRuleChoiceOption(definition.Key, value, choiceName));
        }

        if (!string.IsNullOrWhiteSpace(currentValue) &&
            options.All(option => !option.Value.Equals(currentValue, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new ConfigChoiceOptionViewModel
            {
                Value = currentValue,
                LabelZh = currentValue,
                LabelEn = currentValue
            });
        }

        return options;
    }

    private static ConfigChoiceOptionViewModel CreateRuleChoiceOption(string ruleKey, string value, string name)
    {
        if (TryResolveRuleChoiceLabels(ruleKey, value, name, out var labelZh, out var labelEn))
        {
            return new ConfigChoiceOptionViewModel
            {
                Value = value,
                LabelZh = labelZh,
                LabelEn = labelEn
            };
        }

        return new ConfigChoiceOptionViewModel
        {
            Value = value,
            LabelZh = name,
            LabelEn = name
        };
    }

    private static bool TryResolveRuleChoiceLabels(
        string ruleKey,
        string value,
        string name,
        out string labelZh,
        out string labelEn)
    {
        labelZh = name;
        labelEn = name;
        var normalizedKey = ruleKey.Trim();
        var normalizedValue = value.Trim();
        var normalizedName = name.Trim();

        if (normalizedKey.Equals("gameMode", StringComparison.OrdinalIgnoreCase))
        {
            if (normalizedValue.Equals("survival", StringComparison.OrdinalIgnoreCase))
            {
                labelZh = "生存";
                labelEn = "Survival";
                return true;
            }

            if (normalizedValue.Equals("creative", StringComparison.OrdinalIgnoreCase))
            {
                labelZh = "创造";
                labelEn = "Creative";
                return true;
            }
        }

        if (normalizedKey.Equals("playerlives", StringComparison.OrdinalIgnoreCase))
        {
            if (normalizedValue == "-1")
            {
                labelZh = "无限";
                labelEn = "Infinite";
                return true;
            }

            labelZh = normalizedValue;
            labelEn = normalizedValue;
            return true;
        }

        if (normalizedKey.Equals("creatureHostility", StringComparison.OrdinalIgnoreCase))
        {
            if (normalizedValue.Equals("aggressive", StringComparison.OrdinalIgnoreCase))
            {
                labelZh = "主动";
                labelEn = "Aggressive";
                return true;
            }

            if (normalizedValue.Equals("passive", StringComparison.OrdinalIgnoreCase))
            {
                labelZh = "被动";
                labelEn = "Passive";
                return true;
            }

            if (normalizedValue.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                labelZh = "友好";
                labelEn = "Never hostile";
                return true;
            }
        }

        if (normalizedKey.Equals("temporalStorms", StringComparison.OrdinalIgnoreCase))
        {
            switch (normalizedValue.ToLowerInvariant())
            {
                case "off":
                    labelZh = "关";
                    labelEn = "Off";
                    return true;
                case "veryrare":
                    labelZh = "每30~40天来临一次，每次强度和频率增加2.5%，上限为25%";
                    labelEn = "Approx. every 30-40 days, +2.5% each time, capped at 25%";
                    return true;
                case "rare":
                    labelZh = "每20~30天来临一次，每次强度和频率增加5%，上限为50%";
                    labelEn = "Approx. every 20-30 days, +5% each time, capped at 50%";
                    return true;
                case "sometimes":
                    labelZh = "每10~20天来临一次，每次强度和频率增加10%，上限为100%";
                    labelEn = "Approx. every 10-20 days, +10% each time, capped at 100%";
                    return true;
                case "often":
                    labelZh = "每5~10天来临一次，每次强度和频率增加15%，上限为150%";
                    labelEn = "Approx. every 5-10 days, +15% each time, capped at 150%";
                    return true;
                case "veryoften":
                    labelZh = "每3~6天来临一次，每次强度和频率增加20%，上限为200%";
                    labelEn = "Approx. every 3-6 days, +20% each time, capped at 200%";
                    return true;
            }
        }

        if (normalizedKey.Equals("surfaceCopperDeposits", StringComparison.OrdinalIgnoreCase) ||
            normalizedKey.Equals("surfaceTinDeposits", StringComparison.OrdinalIgnoreCase))
        {
            switch (normalizedName.ToLowerInvariant())
            {
                case "very common":
                    labelZh = "非常常见";
                    labelEn = "Very common";
                    return true;
                case "common":
                    labelZh = "常见";
                    labelEn = "Common";
                    return true;
                case "uncommon":
                    labelZh = "不常见";
                    labelEn = "Uncommon";
                    return true;
                case "rare":
                    labelZh = "稀有";
                    labelEn = "Rare";
                    return true;
                case "very rare":
                    labelZh = "非常稀有";
                    labelEn = "Very Rare";
                    return true;
                case "extremly rare":
                case "extremely rare":
                    labelZh = "极其稀有";
                    labelEn = "Extremely rare";
                    return true;
                case "never":
                    labelZh = "不存在";
                    labelEn = "Never";
                    return true;
            }
        }

        if (normalizedKey.Equals("daysPerMonth", StringComparison.OrdinalIgnoreCase))
        {
            switch (normalizedValue)
            {
                case "30":
                    labelZh = "30天（现实24小时）";
                    labelEn = "30 days (24 real life hours)";
                    return true;
                case "20":
                    labelZh = "20天（现实16小时）";
                    labelEn = "20 days (16 real life hours)";
                    return true;
                case "12":
                    labelZh = "12天（现实9.6小时）";
                    labelEn = "12 days (9.6 real life hours)";
                    return true;
                case "9":
                    labelZh = "9天（现实7.2小时）";
                    labelEn = "9 days (7.2 real life hours)";
                    return true;
                case "6":
                    labelZh = "6天（现实4.8小时）";
                    labelEn = "6 days (4.8 real life hours)";
                    return true;
                case "3":
                    labelZh = "3天（现实2.4小时）";
                    labelEn = "3 days (2.4 real life hours)";
                    return true;
            }
        }

        if (normalizedKey.Equals("worldWidth", StringComparison.OrdinalIgnoreCase) ||
            normalizedKey.Equals("worldLength", StringComparison.OrdinalIgnoreCase))
        {
            if (TryResolveWorldSizeLabelZh(normalizedName, out var sizeLabelZh))
            {
                labelZh = sizeLabelZh;
                labelEn = normalizedName;
                return true;
            }

            if (int.TryParse(normalizedValue, out var worldSize))
            {
                labelZh = $"{worldSize:N0} 格";
                labelEn = normalizedName;
                return true;
            }
        }

        if (normalizedKey.Equals("worldEdge", StringComparison.OrdinalIgnoreCase))
        {
            if (normalizedValue.Equals("blocked", StringComparison.OrdinalIgnoreCase))
            {
                labelZh = "被阻挡";
                labelEn = "Blocked";
                return true;
            }

            if (normalizedValue.Equals("traversable", StringComparison.OrdinalIgnoreCase))
            {
                labelZh = "可越过/可掉落";
                labelEn = "Traversable (Can fall down)";
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveWorldSizeLabelZh(string name, out string labelZh)
    {
        labelZh = name switch
        {
            "8 mil blocks" => "800万个方块",
            "4 mil blocks" => "400万个方块",
            "2 mil blocks" => "200万个方块",
            "1 mil blocks" => "100万个方块",
            "600k blocks" => "60万个方块",
            "512k blocks" => "51.2万个方块",
            "384k blocks" => "38.4万个方块",
            "256k blocks" => "25万个方块",
            "102k blocks" => "10.2万个方块",
            "51k blocks" => "5.1万个方块",
            "25k blocks" => "2.5万个方块",
            "10k blocks" => "1万个方块",
            "5120 blocks" => "5120个方块",
            "1024 blocks" => "1024个方块",
            "512 blocks" => "512个方块",
            "384 blocks" => "384个方块",
            "256 blocks" => "256个方块",
            "128 blocks" => "128个方块",
            "64 blocks" => "64个方块",
            "32 blocks" => "32个方块",
            _ => string.Empty
        };

        return labelZh.Length > 0;
    }

    private void UpdateWorldGeneratedState()
    {
        var path = SelectedSave?.FullPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = SaveFileLocation;
        }

        IsWorldGenerated = IsSaveWorldGenerated(path);
    }

    private static bool IsSaveWorldGenerated(string? savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath)) return false;

        try
        {
            var fullPath = Path.GetFullPath(savePath.Trim());
            if (!File.Exists(fullPath)) return false;

            return new FileInfo(fullPath).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateWorldRuleEditability()
    {
        var lockWorldCreateOnlyRules = IsWorldGenerated;
        foreach (var rule in WorldRules)
        {
            rule.CanEdit = !(lockWorldCreateOnlyRules && rule.IsOnlyDuringWorldCreate);
        }
    }

    private static bool IsOnlyDuringWorldCreateRule(string ruleKey)
    {
        return OnlyDuringWorldCreateRuleKeys.Contains(ruleKey);
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

    private static string ResolveDefaultServerLanguage()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        return culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-cn" : "en";
    }

    #region Constructors

    public ConfigViewModel()
    {
        EnsureServerLanguageOptions();
        EnsureWorldSelectionOptions();
        SyncSelectedPlayStyleOption();
        SyncSelectedWorldTypeOption();
    }

    public ConfigViewModel(
        IInstanceProfileService instanceProfileService,
        IInstanceServerConfigService instanceServerConfigService,
        IInstanceSaveService instanceSaveService,
        IAdvancedJsonDialogService advancedJsonDialogService,
        IFilePickerService filePickerService,
        IServerImageService serverImageService,
        IImagePreviewDialogService imagePreviewDialogService)
    {
        _instanceProfileService = instanceProfileService;
        _instanceServerConfigService = instanceServerConfigService;
        _instanceSaveService = instanceSaveService;
        _advancedJsonDialogService = advancedJsonDialogService;
        _filePickerService = filePickerService;
        _serverImageService = serverImageService;
        _imagePreviewDialogService = imagePreviewDialogService;
        EnsureServerLanguageOptions();
        EnsureWorldSelectionOptions();
        SyncSelectedPlayStyleOption();
        SyncSelectedWorldTypeOption();

        _ = RefreshAsync();
    }

    #endregion
}
