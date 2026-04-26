using System.Collections.ObjectModel;
using System.ComponentModel;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     存档页面视图模型
/// </summary>
public partial class SaveViewModel : ViewModelBase
{
    private readonly IInstanceProfileService? _instanceProfileService;
    private readonly IInstanceSaveService? _instanceSaveService;
    private bool _syncingSelectAll;

    [ObservableProperty] private InstanceProfile? _selectedProfile;
    [ObservableProperty] private string _newSaveName = "default";
    [ObservableProperty] private bool _selectAll;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];

    public ObservableCollection<SaveFileItemViewModel> Saves { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasSaves => Saves.Count > 0;

    public bool HasNoSaves => !HasSaves;

    public bool HasSelectedSaves => Saves.Any(save => save.IsSelected);

    public string CurrentProfileVersion
    {
        get
        {
            var profile = SelectedProfile;
            return profile is null ? "-" : profile.Version;
        }
    }

    partial void OnSelectedProfileChanged(InstanceProfile? value)
    {
        OnPropertyChanged(nameof(CurrentProfileVersion));
        _ = LoadSavesAsync(value);
    }

    partial void OnSelectAllChanged(bool value)
    {
        if (_syncingSelectAll) return;

        _syncingSelectAll = true;
        foreach (var save in Saves)
            save.IsSelected = value;
        _syncingSelectAll = false;

        OnPropertyChanged(nameof(HasSelectedSaves));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RefreshProfilesAsync();
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (_instanceProfileService is null || _instanceSaveService is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = "请先选择档案。";
            return;
        }

        try
        {
            IsBusy = true;
            var savePath = await _instanceSaveService.CreateSaveAsync(profile, NewSaveName);
            profile.ActiveSaveFile = savePath;
            profile.SaveDirectory = Path.GetDirectoryName(savePath) ?? profile.SaveDirectory;
            profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
            _instanceProfileService.UpdateProfile(profile);

            NewSaveName = "default";
            await LoadSavesAsync(profile);
            StatusMessage = $"已创建存档：{Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"创建存档失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (_instanceProfileService is null || _instanceSaveService is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = "请先选择档案。";
            return;
        }

        var selectedPaths = Saves
            .Where(save => save.IsSelected)
            .Select(save => save.FullPath)
            .ToList();
        if (selectedPaths.Count == 0)
        {
            StatusMessage = "请先勾选要删除的存档。";
            return;
        }

        try
        {
            IsBusy = true;
            var deleted = await _instanceSaveService.DeleteSavesAsync(profile, selectedPaths);
            await LoadSavesAsync(profile);

            if (selectedPaths.Any(path => path.Equals(profile.ActiveSaveFile, StringComparison.OrdinalIgnoreCase)))
            {
                var nextActive = Saves.FirstOrDefault()?.FullPath;
                if (!string.IsNullOrWhiteSpace(nextActive))
                {
                    await _instanceSaveService.SetActiveSaveAsync(profile, nextActive);
                    profile.ActiveSaveFile = nextActive;
                    profile.SaveDirectory = Path.GetDirectoryName(nextActive) ?? profile.SaveDirectory;
                }
                else
                {
                    var defaultSave = GetDefaultSaveFile(profile.Id);
                    await _instanceSaveService.SetActiveSaveAsync(profile, defaultSave);
                    profile.ActiveSaveFile = defaultSave;
                    profile.SaveDirectory = Path.GetDirectoryName(defaultSave) ?? profile.SaveDirectory;
                }

                profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
                _instanceProfileService.UpdateProfile(profile);
                await LoadSavesAsync(profile);
            }

            StatusMessage = deleted > 0 ? $"已删除 {deleted} 个存档。" : "没有可删除的存档。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除存档失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SetActiveAsync(SaveFileItemViewModel? saveItem)
    {
        if (_instanceProfileService is null || _instanceSaveService is null || saveItem is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = "请先选择档案。";
            return;
        }

        try
        {
            IsBusy = true;
            await _instanceSaveService.SetActiveSaveAsync(profile, saveItem.FullPath);
            profile.ActiveSaveFile = saveItem.FullPath;
            profile.SaveDirectory = Path.GetDirectoryName(saveItem.FullPath) ?? profile.SaveDirectory;
            profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
            _instanceProfileService.UpdateProfile(profile);

            await LoadSavesAsync(profile);
            StatusMessage = $"已切换当前存档：{saveItem.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"切换存档失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshProfilesAsync()
    {
        if (_instanceProfileService is null) return;

        try
        {
            IsBusy = true;
            var currentSelectedId = SelectedProfile?.Id;
            var profiles = _instanceProfileService.GetProfiles();

            Profiles.Clear();
            foreach (var profile in profiles) Profiles.Add(profile);

            OnPropertyChanged(nameof(HasProfiles));
            OnPropertyChanged(nameof(HasNoProfiles));

            if (Profiles.Count == 0)
            {
                SelectedProfile = null;
                Saves.Clear();
                OnPropertyChanged(nameof(HasSaves));
                OnPropertyChanged(nameof(HasNoSaves));
                StatusMessage = "暂无档案，请先到实例/创建页面创建档案。";
                return;
            }

            var target = Profiles.FirstOrDefault(profile =>
                !string.IsNullOrWhiteSpace(currentSelectedId) &&
                profile.Id.Equals(currentSelectedId, StringComparison.OrdinalIgnoreCase));
            target ??= Profiles[0];

            if (!ReferenceEquals(SelectedProfile, target))
            {
                SelectedProfile = target;
            }
            else
            {
                await LoadSavesAsync(target);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"刷新档案失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSavesAsync(InstanceProfile? profile)
    {
        if (_instanceSaveService is null || profile is null)
        {
            Saves.Clear();
            OnPropertyChanged(nameof(HasSaves));
            OnPropertyChanged(nameof(HasNoSaves));
            OnPropertyChanged(nameof(HasSelectedSaves));
            return;
        }

        try
        {
            var saveEntries = await _instanceSaveService.GetSavesAsync(profile);

            foreach (var save in Saves) save.PropertyChanged -= OnSaveItemPropertyChanged;
            Saves.Clear();

            foreach (var saveEntry in saveEntries)
            {
                var item = new SaveFileItemViewModel
                {
                    FullPath = saveEntry.FullPath,
                    FileName = saveEntry.FileName,
                    LastWriteTimeUtc = saveEntry.LastWriteTimeUtc,
                    IsActive = saveEntry.FullPath.Equals(profile.ActiveSaveFile, StringComparison.OrdinalIgnoreCase)
                };
                item.PropertyChanged += OnSaveItemPropertyChanged;
                Saves.Add(item);
            }

            SyncSelectAllByRows();
            OnPropertyChanged(nameof(HasSaves));
            OnPropertyChanged(nameof(HasNoSaves));
            OnPropertyChanged(nameof(HasSelectedSaves));

            StatusMessage = $"已加载 {Saves.Count} 个存档（档案版本：{profile.Version}）。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"读取存档失败：{ex.Message}";
        }
    }

    private void OnSaveItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SaveFileItemViewModel.IsSelected)) return;
        SyncSelectAllByRows();
        OnPropertyChanged(nameof(HasSelectedSaves));
    }

    private void SyncSelectAllByRows()
    {
        _syncingSelectAll = true;
        SelectAll = Saves.Count > 0 && Saves.All(item => item.IsSelected);
        _syncingSelectAll = false;
    }

    private static string GetDefaultSaveFile(string profileId)
    {
        var workspaceRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VSSL",
            "workspace");
        return Path.Combine(workspaceRoot, "saves", profileId, "default.vcdbs");
    }

    #region Constructors

    public SaveViewModel()
    {
    }

    public SaveViewModel(
        IInstanceProfileService instanceProfileService,
        IInstanceSaveService instanceSaveService)
    {
        _instanceProfileService = instanceProfileService;
        _instanceSaveService = instanceSaveService;
        _ = RefreshProfilesAsync();
    }

    #endregion
}
