using Microsoft.UI.Xaml;
using SafeCCleaner.Core.Interfaces;
using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.App.ViewModels;

public sealed class CleanupItemViewModel : ObservableObject
{
    private bool _isSelected;
    private long _reclaimableBytes;
    private long _fileCount;
    private string? _warning;

    public CleanupItemViewModel(ICleanupRule rule)
    {
        Rule = rule;
        _isSelected = rule.DefaultSelected;
    }

    public ICleanupRule Rule { get; }
    public string Id => Rule.Id;
    public string Name => Rule.DisplayName;
    public string Description => Rule.Description;
    public string RiskText => Rule.Risk switch
    {
        CleanupRisk.Safe => "SAFE",
        CleanupRisk.Advanced => "ADVANCED",
        _ => "DANGER"
    };
    public string ElevationText => Rule.RequiresElevation ? "UAC" : string.Empty;
    public Visibility ElevationVisibility => Rule.RequiresElevation ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public long ReclaimableBytes
    {
        get => _reclaimableBytes;
        set
        {
            if (SetProperty(ref _reclaimableBytes, value))
            {
                OnPropertyChanged(nameof(SizeText));
                OnPropertyChanged(nameof(CanClean));
            }
        }
    }

    public long FileCount
    {
        get => _fileCount;
        set
        {
            if (SetProperty(ref _fileCount, value))
                OnPropertyChanged(nameof(FileCountText));
        }
    }

    public string? Warning
    {
        get => _warning;
        set
        {
            if (SetProperty(ref _warning, value))
                OnPropertyChanged(nameof(WarningVisibility));
        }
    }

    public bool CanClean => ReclaimableBytes > 0;
    public Visibility WarningVisibility => string.IsNullOrWhiteSpace(Warning) ? Visibility.Collapsed : Visibility.Visible;
    public string SizeText => ByteSizeFormatter.Format(ReclaimableBytes);
    public string FileCountText => FileCount switch
    {
        0 => "нет файлов",
        1 => "1 файл",
        _ => $"{FileCount:N0} файлов"
    };
}
