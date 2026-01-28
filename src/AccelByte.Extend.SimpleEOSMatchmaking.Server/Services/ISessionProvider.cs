using System.Threading.Tasks;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;

/// <summary>
/// APPLICATION-LEVEL CUSTOMIZATION POINT: Interface for getting game sessions for matched players.
/// 
/// See docs/architecture.md for more details.
/// </summary>
public interface ISessionProvider
{
    /// <summary>
    /// Get a session for the matched players.
    /// Implementations may create a new session or find an existing empty session.
    /// </summary>
    /// <param name="match">The match containing the players to get a session for</param>
    /// <returns>Information about the obtained session</returns>
    Task<SessionInfo> GetSessionAsync(Match match);
}