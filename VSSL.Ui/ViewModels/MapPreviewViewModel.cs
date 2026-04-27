using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

public partial class MapPreviewViewModel : ViewModelBase
{
    private readonly IInstanceProfileService? _instanceProfileService;
    private readonly IInstanceSaveService? _instanceSaveService;
    private readonly IMapPreviewService? _mapPreviewService;

    [ObservableProperty] private InstanceProfile? _selectedProfile;
    [ObservableProperty] private SaveFileEntry? _selectedSave;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMapPreview))]
    [NotifyPropertyChangedFor(nameof(IsNoPreviewVisible))]
    [NotifyPropertyChangedFor(nameof(MapPreviewSummary))]
    private Bitmap? _mapPreviewColorImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMapPreview))]
    [NotifyPropertyChangedFor(nameof(IsNoPreviewVisible))]
    [NotifyPropertyChangedFor(nameof(MapPreviewSummary))]
    private Bitmap? _mapPreviewGrayscaleImage;

    [ObservableProperty] private string _mapPreviewSourcePath = string.Empty;

    private int _mapPreviewChunkCount;
    private int _mapPreviewChunkSize;
    private int _mapPreviewDimension;
    private int _mapPreviewMinChunkX;
    private int _mapPreviewMaxChunkX;
    private int _mapPreviewMinChunkZ;
    private int _mapPreviewMaxChunkZ;
    private int _mapPreviewSamplingStep = 1;
    private int _mapPreviewWidth;
    private int _mapPreviewHeight;
    private int _mapPreviewMapSizeX;
    private int _mapPreviewMapSizeZ;
    private ushort _mapPreviewMinTerrainHeight;
    private ushort _mapPreviewMaxTerrainHeight;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];

    public ObservableCollection<SaveFileEntry> Saves { get; } = [];

    public bool HasMapPreview => MapPreviewColorImage is not null && MapPreviewGrayscaleImage is not null;
    public bool IsNoPreviewVisible => !HasMapPreview;

    public int MapPreviewDimension => _mapPreviewDimension;

    public int MapPreviewMinChunkX => _mapPreviewMinChunkX;

    public int MapPreviewMinChunkZ => _mapPreviewMinChunkZ;

    public int MapPreviewSamplingStep => _mapPreviewSamplingStep;

    public int MapPreviewWidth => _mapPreviewWidth;

    public int MapPreviewHeight => _mapPreviewHeight;

    public int MapPreviewMapSizeX => _mapPreviewMapSizeX;

    public int MapPreviewMapSizeZ => _mapPreviewMapSizeZ;

    public string MapPreviewSummary
    {
        get
        {
            if (!HasMapPreview)
                return L("MapPreviewSummaryEmpty");

            var chunkCenterOffsetX = _mapPreviewMapSizeX > 0 && _mapPreviewChunkSize > 0
                ? _mapPreviewMapSizeX / (_mapPreviewChunkSize * 2)
                : 0;
            var chunkCenterOffsetZ = _mapPreviewMapSizeZ > 0 && _mapPreviewChunkSize > 0
                ? _mapPreviewMapSizeZ / (_mapPreviewChunkSize * 2)
                : 0;
            var worldMinChunkX = _mapPreviewMinChunkX - chunkCenterOffsetX;
            var worldMaxChunkX = _mapPreviewMaxChunkX - chunkCenterOffsetX;
            var worldMinChunkZ = _mapPreviewMinChunkZ - chunkCenterOffsetZ;
            var worldMaxChunkZ = _mapPreviewMaxChunkZ - chunkCenterOffsetZ;

            return LF(
                "MapPreviewSummaryFormat",
                _mapPreviewDimension,
                _mapPreviewChunkCount,
                worldMinChunkX,
                worldMaxChunkX,
                worldMinChunkZ,
                worldMaxChunkZ,
                _mapPreviewWidth,
                _mapPreviewHeight,
                _mapPreviewMinTerrainHeight,
                _mapPreviewMaxTerrainHeight,
                _mapPreviewSamplingStep);
        }
    }

    public string MapPreviewSourcePathLine => string.IsNullOrWhiteSpace(MapPreviewSourcePath)
        ? string.Empty
        : LF("MapPreviewSourcePathFormat", MapPreviewSourcePath);

    public string CoordinatePlaceholderText => L("MapPreviewCoordinatePlaceholder");

    public string NoPreviewText => L("MapPreviewNoPreviewText");

    partial void OnSelectedProfileChanged(InstanceProfile? value)
    {
        SelectedSave = null;
        ClearMapPreview();
        _ = RefreshSavesCoreAsync(value);
    }

    partial void OnMapPreviewSourcePathChanged(string value)
    {
        OnPropertyChanged(nameof(MapPreviewSourcePathLine));
    }

    [RelayCommand]
    private async Task RefreshProfilesAsync()
    {
        if (_instanceProfileService is null) return;

        try
        {
            IsBusy = true;
            var oldSelectedId = SelectedProfile?.Id;
            var profiles = _instanceProfileService.GetProfiles();

            Profiles.Clear();
            foreach (var profile in profiles)
                Profiles.Add(profile);

            if (Profiles.Count == 0)
            {
                SelectedProfile = null;
                Saves.Clear();
                StatusMessage = L("MapPreviewStatusNoProfile");
                return;
            }

            SelectedProfile = Profiles.FirstOrDefault(profile =>
                                  !string.IsNullOrWhiteSpace(oldSelectedId) &&
                                  profile.Id.Equals(oldSelectedId, StringComparison.OrdinalIgnoreCase))
                              ?? Profiles[0];

            StatusMessage = LF("MapPreviewStatusLoadedProfilesFormat", Profiles.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("MapPreviewStatusRefreshProfilesFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshSavesAsync()
    {
        await RefreshSavesCoreAsync(SelectedProfile);
    }

    [RelayCommand]
    private async Task LoadMapPreviewAsync()
    {
        if (_mapPreviewService is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("MapPreviewStatusNoProfile");
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = L("MapPreviewStatusLoading");

            var preferredSavePath = ResolvePreferredSavePath(profile);
            var preview = await _mapPreviewService.LoadMapPreviewAsync(profile, preferredSavePath);

            var colorImage = CreateBitmap(preview.ColorPixelsBgra32, preview.Width, preview.Height);
            var grayscaleImage = CreateBitmap(preview.GrayscalePixelsBgra32, preview.Width, preview.Height);
            if (colorImage is null || grayscaleImage is null)
                throw new InvalidOperationException(L("MapPreviewStatusInvalidPixels"));

            MapPreviewColorImage = colorImage;
            MapPreviewGrayscaleImage = grayscaleImage;
            MapPreviewSourcePath = preview.SaveFilePath;

            _mapPreviewChunkCount = preview.ChunkCount;
            _mapPreviewChunkSize = preview.ChunkSize;
            _mapPreviewDimension = preview.Dimension;
            _mapPreviewMinChunkX = preview.MinChunkX;
            _mapPreviewMaxChunkX = preview.MaxChunkX;
            _mapPreviewMinChunkZ = preview.MinChunkZ;
            _mapPreviewMaxChunkZ = preview.MaxChunkZ;
            _mapPreviewSamplingStep = Math.Max(1, preview.SamplingStep);
            _mapPreviewWidth = preview.Width;
            _mapPreviewHeight = preview.Height;
            _mapPreviewMapSizeX = Math.Max(0, preview.MapSizeX);
            _mapPreviewMapSizeZ = Math.Max(0, preview.MapSizeZ);
            _mapPreviewMinTerrainHeight = preview.MinTerrainHeight;
            _mapPreviewMaxTerrainHeight = preview.MaxTerrainHeight;

            OnPropertyChanged(nameof(MapPreviewSummary));
            StatusMessage = L("MapPreviewStatusLoaded");
        }
        catch (Exception ex)
        {
            ClearMapPreview();
            StatusMessage = LF("MapPreviewStatusLoadFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public string BuildCoordinateText(int worldBlockX, int worldBlockZ, int chunkX, int chunkZ)
    {
        return LF("MapPreviewCoordinateFormat", MapPreviewDimension, worldBlockX, worldBlockZ, chunkX, chunkZ);
    }

    private async Task RefreshSavesCoreAsync(InstanceProfile? profile)
    {
        if (_instanceSaveService is null)
        {
            Saves.Clear();
            return;
        }

        if (profile is null)
        {
            Saves.Clear();
            return;
        }

        try
        {
            IsBusy = true;
            var oldSavePath = SelectedSave?.FullPath;
            var saveEntries = await _instanceSaveService.GetSavesAsync(profile);

            Saves.Clear();
            foreach (var save in saveEntries)
                Saves.Add(save);

            SelectedSave = Saves.FirstOrDefault(save =>
                               !string.IsNullOrWhiteSpace(oldSavePath) &&
                               save.FullPath.Equals(oldSavePath, StringComparison.OrdinalIgnoreCase))
                           ?? Saves.FirstOrDefault(save =>
                               save.FullPath.Equals(profile.ActiveSaveFile, StringComparison.OrdinalIgnoreCase))
                           ?? Saves.FirstOrDefault();

            StatusMessage = LF("MapPreviewStatusLoadedSavesFormat", Saves.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("MapPreviewStatusRefreshSavesFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string ResolvePreferredSavePath(InstanceProfile profile)
    {
        if (SelectedSave is not null && File.Exists(SelectedSave.FullPath))
            return SelectedSave.FullPath;
        if (!string.IsNullOrWhiteSpace(profile.ActiveSaveFile) && File.Exists(profile.ActiveSaveFile))
            return profile.ActiveSaveFile;

        return profile.ActiveSaveFile;
    }

    private void ClearMapPreview()
    {
        MapPreviewColorImage = null;
        MapPreviewGrayscaleImage = null;
        MapPreviewSourcePath = string.Empty;

        _mapPreviewChunkCount = 0;
        _mapPreviewChunkSize = 0;
        _mapPreviewDimension = 0;
        _mapPreviewMinChunkX = 0;
        _mapPreviewMaxChunkX = 0;
        _mapPreviewMinChunkZ = 0;
        _mapPreviewMaxChunkZ = 0;
        _mapPreviewSamplingStep = 1;
        _mapPreviewWidth = 0;
        _mapPreviewHeight = 0;
        _mapPreviewMapSizeX = 0;
        _mapPreviewMapSizeZ = 0;
        _mapPreviewMinTerrainHeight = 0;
        _mapPreviewMaxTerrainHeight = 0;

        OnPropertyChanged(nameof(MapPreviewSummary));
        OnPropertyChanged(nameof(MapPreviewSourcePathLine));
    }

    private static Bitmap? CreateBitmap(byte[] pixels, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return null;

        var expectedLength = checked(width * height * 4);
        if (pixels.Length < expectedLength)
            return null;

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Opaque);
        using var locked = bitmap.Lock();
        Marshal.Copy(pixels, 0, locked.Address, expectedLength);
        return bitmap;
    }

    #region Constructors

    public MapPreviewViewModel()
    {
        StatusMessage = L("MapPreviewSummaryEmpty");
    }

    public MapPreviewViewModel(
        IInstanceProfileService instanceProfileService,
        IInstanceSaveService instanceSaveService,
        IMapPreviewService mapPreviewService)
    {
        _instanceProfileService = instanceProfileService;
        _instanceSaveService = instanceSaveService;
        _mapPreviewService = mapPreviewService;
        StatusMessage = L("MapPreviewSummaryEmpty");
        _ = RefreshProfilesAsync();
    }

    #endregion
}
