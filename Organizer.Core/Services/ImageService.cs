using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Organize.Organizer.Core;
using Organize.Organizer.Core.Interfaces;
using Organize.Organizer.Infrastructure.Data;
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.Services;

public class ImageService(AppDbContextFactory dbFactory) : IImageService
{
    public async Task<Image> CreateAsync(
        int cardId,
        byte[] data,
        string filename,
        string? mimeType = null,
        string? description = null)
    {
        return await CreateAsync(
            cardId,
            data,
            thumbnail: null,
            filename,
            mimeType,
            description);
    }

    public async Task<Image> CreateAsync(
        int cardId,
        byte[] data,
        byte[]? thumbnail,
        string filename,
        string? mimeType = null,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        thumbnail ??= ImageThumbnailService.CreateThumbnail(data);
        return await InsertImageAsync(cardId, data, thumbnail, filename, mimeType, description);
    }

    public async Task<Image> CreateAsync(
        int cardId,
        Stream dataStream,
        string filename,
        string? mimeType = null,
        string? description = null)
    {
        return await CreateAsync(
            cardId,
            dataStream,
            thumbnail: null,
            filename,
            mimeType,
            description);
    }

    public async Task<Image> CreateAsync(
        int cardId,
        Stream dataStream,
        byte[]? thumbnail,
        string filename,
        string? mimeType = null,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(dataStream);

        var data = await ReadAllBytesAsync(dataStream);
        thumbnail ??= ImageThumbnailService.CreateThumbnail(data);

        try
        {
            return await InsertImageAsync(cardId, data, thumbnail, filename, mimeType, description);
        }
        finally
        {
            data = [];
            thumbnail = [];
        }
    }

    private async Task<Image> InsertImageAsync(
        int cardId,
        byte[] data,
        byte[] thumbnail,
        string filename,
        string? mimeType,
        string? description)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        var cardExists = await db.Cards
            .AsNoTracking()
            .AnyAsync(c => c.Id == cardId);

        if (!cardExists)
            throw new KeyNotFoundException("Card not found.");

        var nextPosition = await db.Images
            .AsNoTracking()
            .CountAsync(i => i.CardId == cardId);

        Image image = new()
        {
            CardId = cardId,
            Data = data,
            Thumbnail = thumbnail,
            Filename = filename,
            MimeType = mimeType,
            Description = description,
            Position = nextPosition
        };

        db.Images.Add(image);

        await db.SaveChangesAsync();
        var result = new Image
        {
            Id = image.Id,
            CardId = image.CardId,
            Position = image.Position,
            Filename = image.Filename,
            MimeType = image.MimeType,
            Description = image.Description,
            CreatedAt = image.CreatedAt
        };

        image.Data = [];
        image.Thumbnail = [];
        db.ChangeTracker.Clear();

        return result;
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream dataStream)
    {
        if (!dataStream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(dataStream));

        if (dataStream.CanSeek)
        {
            dataStream.Position = 0;

            if (dataStream.Length > int.MaxValue)
                throw new InvalidOperationException("Image is too large.");

            var data = new byte[dataStream.Length];
            var offset = 0;

            while (offset < data.Length)
            {
                var read = await dataStream.ReadAsync(data.AsMemory(offset));
                if (read == 0)
                    break;

                offset += read;
            }

            if (offset != data.Length)
                Array.Resize(ref data, offset);

            return data;
        }

        using var buffer = new MemoryStream();
        await dataStream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    public async Task<(List<SearchCardResult> Cards, int TotalCount)> SearchCardsAsync(
        string query,
        IReadOnlyCollection<int> tagIds,
        SortOrder sort,
        int page,
        int pageSize)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        var q = db.Cards
            .AsQueryable();

        if (tagIds.Count > 0)
        {
            q = q.Where(c =>
                c.Images.Any(i =>
                    i.ImageTags.Any(it => tagIds.Contains(it.TagId))));
        }

        // Busca
        if (!string.IsNullOrWhiteSpace(query))
        {
            query = query.Trim().ToLower();

            q = q.Where(c =>
                c.Images.Any(i =>
                    i.Description != null &&
                    i.Description.ToLower().Contains(query)));
        }

        // Ordenação
        q = sort switch
        {
            SortOrder.MaisAntigo =>
                q.OrderBy(c => c.CreatedAt),

            SortOrder.AlfabeticoAZ =>
                q.OrderBy(c => c.Title),

            SortOrder.AlfabeticoZA =>
                q.OrderByDescending(c => c.Title),

            _ =>
                q.OrderByDescending(c => c.CreatedAt)
        };

        var total = await q.CountAsync();

        var cards = await q
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(card => new SearchCardResult
            {
                CardId = card.Id,
                Title = card.Title,
                CardType = card.CardType,
                CreatedAt = card.CreatedAt,
                ImageCount = card.Images.Count,
                CoverImageId = card.CoverImageId,
                CoverThumbnail = card.CoverImage != null ? card.CoverImage.Thumbnail : null,
                CoverFilename = card.CoverImage != null
                    ? card.CoverImage.Filename
                    : card.Title,
                CoverMimeType = card.CoverImage != null
                    ? card.CoverImage.MimeType
                    : null,
                CoverDescription = card.CoverImage != null
                    ? card.CoverImage.Description ?? string.Empty
                    : string.Empty
            })
            .ToListAsync();

        return (cards, total);
    }

    public async Task<List<int>> GetIdsByCardAsync(int cardId)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        return await db.Images
            .Where(i => i.CardId == cardId)
            .OrderBy(i => i.Position)
            .Select(i => i.Id)
            .ToListAsync();
    }

    public async Task<List<GroupImageSummary>> GetGroupImageSummariesAsync(int cardId)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        return await db.Images
            .Where(i => i.CardId == cardId)
            .OrderBy(i => i.Position)
            .Select(i => new GroupImageSummary
            {
                Id = i.Id,
                Position = i.Position,
                Thumbnail = i.Thumbnail,
                Filename = i.Filename,
                MimeType = i.MimeType,
                Description = i.Description ?? string.Empty
            })
            .ToListAsync();
    }

    public async Task<Image?> GetByIdAsync(int id)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        return await db.Images
            .Where(i => i.Id == id)
            .Select(i => new Image
            {
                Id = i.Id,
                CardId = i.CardId,
                Position = i.Position,
                Filename = i.Filename,
                MimeType = i.MimeType,
                Description = i.Description,
                CreatedAt = i.CreatedAt,
                Thumbnail = i.Thumbnail,
                ImageTags = i.ImageTags
                    .Select(it => new ImageTag
                    {
                        ImageId = it.ImageId,
                        TagId = it.TagId,
                        Tag = new Tag
                        {
                            Id = it.Tag.Id,
                            Name = it.Tag.Name,
                            Color = it.Tag.Color
                        }
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();
    }
    
    public async Task<Stream?> GetDataAsync(int id)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        var data = await db.Images
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => i.Data)
            .FirstOrDefaultAsync();

        if (data is null)
            return null;

        return new MemoryStream(data, writable: false);
    }

    public async Task<List<Image>> GetByCardAsync(int cardId)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        return await db.Images
            .Where(i => i.CardId == cardId)
            .OrderBy(i => i.Position)
            .Select(i => new Image
            {
                Id = i.Id,
                CardId = i.CardId,
                Position = i.Position,
                Filename = i.Filename,
                MimeType = i.MimeType,
                Description = i.Description,
                CreatedAt = i.CreatedAt,
                Thumbnail = i.Thumbnail
            })
            .ToListAsync();
    }

    public async Task<Image> UpdateDescriptionAsync(int imageId, string? description)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        var image = await GetImageSummaryAsync(db, imageId)
                    ?? throw new KeyNotFoundException("Image not found.");

        var updated = await db.Images
            .Where(i => i.Id == imageId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.Description, description));

        if (updated == 0)
            throw new KeyNotFoundException("Image not found.");

        image.Description = description;
        return image;
    }

    public async Task<Image> MoveAsync(int imageId, int newPosition)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        await using var transaction = await db.Database.BeginTransactionAsync();

        var image = await GetImageSummaryAsync(db, imageId)
                    ?? throw new KeyNotFoundException("Image not found.");

        var imagePositions = await db.Images
            .Where(i => i.CardId == image.CardId)
            .OrderBy(i => i.Position)
            .Select(i => new ImagePosition(i.Id, i.Position))
            .ToListAsync();

        if (newPosition < 0 || newPosition >= imagePositions.Count)
            throw new ArgumentOutOfRangeException(nameof(newPosition));

        var moving = imagePositions.First(i => i.Id == imageId);
        imagePositions.Remove(moving);
        imagePositions.Insert(newPosition, moving);

        await db.Images
            .Where(i => i.CardId == image.CardId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.Position, i => -i.Position - 1));

        for (var i = 0; i < imagePositions.Count; i++)
        {
            var id = imagePositions[i].Id;
            var position = i;

            await db.Images
                .Where(image => image.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(image => image.Position, position));
        }

        await transaction.CommitAsync();

        image.Position = newPosition;
        return image;
    }

    public async Task<Image> MoveToCardAsync(int imageId, int targetCardId)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        await using var transaction = await db.Database.BeginTransactionAsync();

        var image = await GetImageSummaryAsync(db, imageId)
                    ?? throw new KeyNotFoundException("Image not found.");

        var cardExists = await db.Cards
            .AnyAsync(c => c.Id == targetCardId);

        if (!cardExists)
            throw new KeyNotFoundException("Target card not found.");

        var nextPosition = await db.Images
            .CountAsync(i => i.CardId == targetCardId);

        var updated = await db.Images
            .Where(i => i.Id == imageId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.CardId, targetCardId)
                .SetProperty(i => i.Position, nextPosition));

        if (updated == 0)
            throw new KeyNotFoundException("Image not found.");

        await transaction.CommitAsync();

        image.CardId = targetCardId;
        image.Position = nextPosition;
        return image;
    }

    public async Task AddTagAsync(int imageId, int tagId)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        var exists = await db.ImageTags
            .AnyAsync(it => it.ImageId == imageId && it.TagId == tagId);

        if (exists)
            return;

        ImageTag imageTag = new()
        {
            ImageId = imageId,
            TagId = tagId
        };

        db.ImageTags.Add(imageTag);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public async Task RemoveTagAsync(int imageId, int tagId)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        await db.ImageTags
            .Where(it => it.ImageId == imageId && it.TagId == tagId)
            .ExecuteDeleteAsync();
    }

    public async Task AddTagToCardImagesAsync(int cardId, int tagId)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        var imageIdsWithoutTag = await db.Images
            .Where(i => i.CardId == cardId)
            .Where(i => !i.ImageTags.Any(it => it.TagId == tagId))
            .Select(i => i.Id)
            .ToListAsync();

        if (imageIdsWithoutTag.Count == 0)
            return;

        db.ImageTags.AddRange(imageIdsWithoutTag.Select(imageId => new ImageTag
        {
            ImageId = imageId,
            TagId = tagId
        }));

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public async Task RemoveTagFromCardImagesAsync(int cardId, int tagId)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        await db.ImageTags
            .Where(it => it.TagId == tagId && it.Image.CardId == cardId)
            .ExecuteDeleteAsync();
    }

    public async Task DeleteAsync(int imageId)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        var deleted = await db.Images
            .Where(i => i.Id == imageId)
            .ExecuteDeleteAsync();

        if (deleted == 0)
            throw new KeyNotFoundException("Image not found.");
    }

    private static Task<Image?> GetImageSummaryAsync(AppDbContext db, int imageId)
    {
        return db.Images
            .Where(i => i.Id == imageId)
            .Select(i => new Image
            {
                Id = i.Id,
                CardId = i.CardId,
                Position = i.Position,
                Filename = i.Filename,
                MimeType = i.MimeType,
                Description = i.Description,
                CreatedAt = i.CreatedAt
            })
            .FirstOrDefaultAsync();
    }

    private sealed record ImagePosition(int Id, int Position);
}
