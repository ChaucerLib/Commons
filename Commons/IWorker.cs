using System.Threading;
using System.Threading.Tasks;

namespace Commons
{
    public interface IWorker
    {
        Task DoWorkAsync(CancellationToken ct);
    }
}