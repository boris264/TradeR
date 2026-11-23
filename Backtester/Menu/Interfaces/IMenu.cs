using System.Threading;
using System.Threading.Tasks;

namespace Backtester.Menu.Interfaces
{
    internal interface IMenu<T>
    {
        Task<T> Prompt(CancellationToken cancellationToken);
    }
}
