using FitAdvisor.Core;

namespace FitAdvisor.Mobile.Services;

public interface IBackupDocuments
{
    Task<bool> SaveAsync(byte[] contents);
    Task<byte[]?> OpenAsync();
}
public interface IHealthSessionReader
{
    Task<IReadOnlyList<WorkoutRecord>> ReadAsync(DateOnly from, DateOnly through);
}
