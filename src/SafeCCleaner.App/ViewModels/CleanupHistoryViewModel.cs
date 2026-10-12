using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.App.ViewModels;

public sealed class CleanupHistoryViewModel
{
    public CleanupHistoryViewModel(CleanupHistoryRecord record)
    {
        Record = record;
    }

    public CleanupHistoryRecord Record { get; }
    public long Id => Record.Id;
    public string Title => Record.OperationName;
    public string ModeText => Record.Mode;
    public string DateText => Record.CompletedAt.LocalDateTime.ToString("dd.MM.yyyy · HH:mm");
    public string FreedText
    {
        get
        {
            var delta = Record.AfterFreeBytes - Record.BeforeFreeBytes;
            return (delta >= 0 ? "+" : "-") + ByteSizeFormatter.Format(Math.Abs(delta));
        }
    }
    public string DetailText => $"Свободно {ByteSizeFormatter.Format(Record.BeforeFreeBytes)} → {ByteSizeFormatter.Format(Record.AfterFreeBytes)} · по правилам {ByteSizeFormatter.Format(Record.ReportedFreedBytes)}";
    public string ResultText => Record.Success ? "УСПЕШНО" : "С ОШИБКАМИ";
    public string ItemCountText => Record.Items.Count switch
    {
        1 => "1 категория",
        < 5 => $"{Record.Items.Count} категории",
        _ => $"{Record.Items.Count} категорий"
    };
}
