namespace Technolife.RustDesk.Windows;

public sealed record SetupUiState(
    SetupUiStatus Status,
    string StatusText,
    string Description,
    string? ErrorCode = null);
