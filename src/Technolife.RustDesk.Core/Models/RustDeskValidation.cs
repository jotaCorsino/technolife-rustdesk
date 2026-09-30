using Technolife.RustDesk.Core.Enums;

namespace Technolife.RustDesk.Core.Models;

public sealed record RustDeskValidation
{
    public RustDeskValidation(RustDeskValidationStatus status, string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);

        Status = status;
        Evidence = evidence;
    }

    public RustDeskValidationStatus Status { get; }

    public string Evidence { get; }

    public override string ToString() => $"RustDesk validation status: {Status}.";
}
