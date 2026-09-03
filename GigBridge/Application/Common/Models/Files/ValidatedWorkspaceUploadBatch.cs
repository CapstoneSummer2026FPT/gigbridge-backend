using System.Collections;

namespace Application.Common.Models.Files;

public sealed class ValidatedWorkspaceUploadBatch :
    IReadOnlyList<ValidatedWorkspaceUploadFile>,
    IAsyncDisposable
{
    private readonly IReadOnlyList<ValidatedWorkspaceUploadFile> _files;

    public ValidatedWorkspaceUploadBatch(IReadOnlyList<ValidatedWorkspaceUploadFile> files)
    {
        _files = files;
    }

    public int Count => _files.Count;
    public ValidatedWorkspaceUploadFile this[int index] => _files[index];

    public IEnumerator<ValidatedWorkspaceUploadFile> GetEnumerator() => _files.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public async ValueTask DisposeAsync()
    {
        foreach (var file in _files)
        {
            await file.DisposeAsync();
        }
    }
}
