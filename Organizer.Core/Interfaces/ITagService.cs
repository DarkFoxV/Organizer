using System.Collections.Generic;
using System.Threading.Tasks;
using Organize.Organizer.Core.Enums;

namespace Organize.Organizer.Core.Interfaces;

public interface ITagService
{
    Task<Tag> CreateAsync(string name, TagColor color);

    Task<Tag?> GetByIdAsync(int id);

    Task<Tag?> GetByNameAsync(string name);

    Task<List<Tag>> GetAllAsync();

    Task<Tag> RenameAsync(int id, string newName);

    Task<Tag> ChangeColorAsync(int id, TagColor color);

    Task DeleteAsync(int id);
}