using FitAdvisor.Storage;

namespace FitAdvisor.Mobile.Services;

public sealed class AndroidBackupDocuments : IBackupDocuments
{
    private static MainActivity Activity => Platform.CurrentActivity as MainActivity
        ?? throw new ArgumentException("Open FitAdvisor before selecting a backup file.");
    public async Task<bool> SaveAsync(byte[] contents)
    {
        var uri = await Activity.PickDocumentAsync(true);
        if (uri == null) return false;
        using var output = Activity.ContentResolver!.OpenOutputStream(uri, "wt")
            ?? throw new IOException("Unable to write to the selected document.");
        await output.WriteAsync(contents); await output.FlushAsync(); return true;
    }
    public async Task<byte[]?> OpenAsync()
    {
        var uri = await Activity.PickDocumentAsync(false);
        if (uri == null) return null;
        using var input = Activity.ContentResolver!.OpenInputStream(uri)
            ?? throw new IOException("Unable to read the selected document.");
        using var output = new MemoryStream(); var buffer = new byte[81920]; int count;
        while ((count = await input.ReadAsync(buffer)) > 0)
        {
            if (output.Length + count > BackupCodec.MaxFileBytes) throw new ArgumentException("Backup exceeds the 32 MB limit.");
            await output.WriteAsync(buffer.AsMemory(0, count));
        }
        return output.ToArray();
    }
}
