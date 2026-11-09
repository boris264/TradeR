using System.Threading;
using System.Threading.Tasks;
using Importer.Files;

namespace Importer.Data
{
    internal interface IFileImporter
    {
        Task<FileImportResult> ImportAsync(ImportFile file, CancellationToken cancellationToken);
    }
}
