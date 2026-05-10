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
    private readonly IInstanceProfileService? _instanceProfileService;
    private readonly IInstanceServerConfigService? _instanceServerConfigService;
    private readonly IAdvancedJsonDialogService? _advancedJsonDialogService;

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

    [ObservableProperty] private string _seed = "123456789";
    [ObservableProperty] private string _worldName = "A new world";
    [ObservableProperty] private string _saveFileLocation = string.Empty;
    [ObservableProperty] private string _playStyle = "surviveandbuild";
    [ObservableProperty] private string _worldType = "standard";
    [ObservableProperty] private int _worldHeight = 256;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];

    public ObservableCollection<ConfigWorldRuleItemViewModel> WorldRules { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

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
                AllowFallingBlocks = AllowFallingBlocks
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
        Seed = "123456789";
        WorldName = "A new world";
        SaveFileLocation = string.Empty;
        PlayStyle = "surviveandbuild";
        WorldType = "standard";
        WorldHeight = 256;
        WorldRules.Clear();
    }

    #region Constructors

    public ConfigViewModel()
    {
    }

    public ConfigViewModel(
        IInstanceProfileService instanceProfileService,
        IInstanceServerConfigService instanceServerConfigService,
        IAdvancedJsonDialogService advancedJsonDialogService)
    {
        _instanceProfileService = instanceProfileService;
        _instanceServerConfigService = instanceServerConfigService;
        _advancedJsonDialogService = advancedJsonDialogService;

        _ = RefreshAsync();
    }

    #endregion
}
