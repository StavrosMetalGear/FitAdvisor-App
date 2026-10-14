using FitAdvisor.Core;

namespace FitAdvisor.Mobile.Services;

public interface IBackupDocuments
{
    Task<bool> SaveAsync(byte[] contents);
    Task<byte[]?> OpenAsync();
}
public interface IHealthSessionReader
{
    Task<HealthSessionReadResult> ReadAsync(DateOnly from, DateOnly through, bool requestHistory = false);
}
public sealed record HealthSessionReadResult(IReadOnlyList<WorkoutRecord> Sessions, string Diagnostics);
