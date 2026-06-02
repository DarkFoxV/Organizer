using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Util.Store;

namespace Organizer.Application.Services;

public interface ITokenStorage : IDataStore
{
    bool HasRefreshToken { get; }
}
