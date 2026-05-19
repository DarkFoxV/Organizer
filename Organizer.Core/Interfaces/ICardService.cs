using System.Collections.Generic;
using System.Threading.Tasks;
using Organize.Organizer.Core.Enums;

namespace Organize.Organizer.Core.Interfaces;

public interface ICardService
{
    Task<Card> CreateAsync(string title, CardType cardType);

    Task<Card?> GetByIdAsync(int id);

    Task<List<Card>> GetAllAsync();

    Task<Card> RenameAsync(int id, string title);

    Task<Card> SetCoverAsync(int cardId, int imageId);

    Task DeleteAsync(int id);
}