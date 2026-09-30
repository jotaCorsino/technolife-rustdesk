using Technolife.RustDesk.Core.Enums;

namespace Technolife.RustDesk.Core.Models;

public sealed record PlatformInfo(PlatformKind Kind, CpuArchitecture Architecture);
