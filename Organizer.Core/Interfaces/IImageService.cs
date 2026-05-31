using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Organizer.Application.ViewModels.Components;

namespace Organize.Organizer.Core.Interfaces;

public interface IImageService
{
    Task<Image> CreateAsync(
        int cardId,
        byte[] data,
        string filename,
        string? mimeType = null,
        string? description = null);

    Task<Image> CreateAsync(
        int cardId,
        byte[] data,
        byte[]? thumbnail,
        string filename,
        string? mimeType = null,
        string? description = null);

    Task<Image> CreateAsync(
        int cardId,
        Stream dataStream,
        string filename,
        string? mimeType = null,
        string? description = null);

    Task<Image> CreateAsync(
        int cardId,
        Stream dataStream,
        byte[]? thumbnail,
        string filename,
        string? mimeType = null,
        string? description = null);

    Task<(List<SearchCardResult> Cards, int TotalCount)> SearchCardsAsync(
        string query,
        IReadOnlyCollection<int> tagIds,
        SortOrder sort,
        int page,
        int pageSize);

    Task<List<int>> GetIdsByCardAsync(int cardId);

    Task<List<GroupImageSummary>> GetGroupImageSummariesAsync(int cardId);

    Task<Image?> GetByIdAsync(int id);
    
    Task<Stream?> GetDataAsync(int id);

    Task<List<Image>> GetByCardAsync(int cardId);

    Task<Image> UpdateDescriptionAsync(int imageId, string? description);

    Task<Image> MoveAsync(int imageId, int newPosition);

    Task<Image> MoveToCardAsync(int imageId, int targetCardId);

    Task AddTagAsync(int imageId, int tagId);

    Task RemoveTagAsync(int imageId, int tagId);

    Task AddTagToCardImagesAsync(int cardId, int tagId);

    Task RemoveTagFromCardImagesAsync(int cardId, int tagId);

    Task DeleteAsync(int imageId);
}
