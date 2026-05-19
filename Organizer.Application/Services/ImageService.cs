using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using Organize.Organizer.Core;
using Organize.Organizer.Core.Interfaces;
using Organize.Organizer.Infrastructure.Data;
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.Services;

public class ImageService(AppDbContext db) : IImageService
{
    private const int ThumbnailWidth  = 220;
    private const int ThumbnailHeight = 300;
    
    public async Task<Image> CreateAsync(
        int cardId,
        byte[] data,
        string filename,
        string? mimeType = null,
        string? description = null)
    {
        var thumbnail = await Task.Run(() => CreateThumbnail(data));
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
            CardId      = cardId,
            Data        = data,
            Thumbnail   = thumbnail,
            Filename    = filename,
            MimeType    = mimeType,
            Description = description,
            Position    = nextPosition
        };

        db.Images.Add(image);

        await db.SaveChangesAsync();
        var result = new Image
        {
            Id          = image.Id,
            CardId      = image.CardId,
            Position    = image.Position,
            Filename    = image.Filename,
            MimeType    = image.MimeType,
            Description = image.Description,
            CreatedAt   = image.CreatedAt
        };

        db.ChangeTracker.Clear();

        return result;
    }

    public static byte[] CreateThumbnail(byte[] data)
    {
        using var input = new MemoryStream(data);

        var full = new Bitmap(input);

        var ratio = Math.Min(
            ThumbnailWidth / (double)full.PixelSize.Width,
            ThumbnailHeight / (double)full.PixelSize.Height);

        var width  = (int)(full.PixelSize.Width * ratio);
        var height = (int)(full.PixelSize.Height * ratio);

        var thumb = full.CreateScaledBitmap(
            new Avalonia.PixelSize(width, height));

        using var output = new MemoryStream();

        thumb.Save(output);

        return output.ToArray();
    }


    public async Task<(List<SearchCardResult> Cards, int TotalCount)> SearchCardsAsync(
        string query,
        IReadOnlyCollection<int> tagIds,
        SortOrder sort,
        int page,
        int pageSize)
    {
        var q = db.Cards
            .AsNoTracking()
            .Include(c => c.CoverImage)
            .ThenInclude(i => i!.ImageTags)
            .ThenInclude(it => it.Tag)
            .Include(c => c.Images)
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
                    (i.Description != null &&
                     i.Description.ToLower().Contains(query))

                    ||

                    i.ImageTags.Any(it =>
                        it.Tag.Name.ToLower().Contains(query))));
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
                CoverDescription = card.CoverImage != null
                    ? card.CoverImage.Description ?? string.Empty
                    : string.Empty
            })
            .ToListAsync();

        return (cards, total);
    }

    public async Task<List<int>> GetIdsByCardAsync(int cardId)
    {
        return await db.Images
            .Where(i => i.CardId == cardId)
            .OrderBy(i => i.Position)
            .Select(i => i.Id)
            .ToListAsync();
    }
    
    public async Task<Image?> GetByIdAsync(int id)
    {
        return await db.Images
            .Include(i => i.ImageTags)
            .ThenInclude(it => it.Tag)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<byte[]?> GetDataAsync(int id)
    {
        return await db.Images
            .Where(i => i.Id == id)
            .Select(i => i.Data)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Image>> GetByCardAsync(int cardId)
    {
        return await db.Images
            .Where(i => i.CardId == cardId)
            .OrderBy(i => i.Position)
            .ToListAsync();
    }

    public async Task<Image> UpdateDescriptionAsync(int imageId, string? description)
    {
        var image = await db.Images
                        .FirstOrDefaultAsync(i => i.Id == imageId)
                    ?? throw new KeyNotFoundException("Image not found.");

        image.Description = description;

        await db.SaveChangesAsync();

        return image;
    }

    public async Task<Image> MoveAsync(int imageId, int newPosition)
    {
        var image = await db.Images
                        .FirstOrDefaultAsync(i => i.Id == imageId)
                    ?? throw new KeyNotFoundException("Image not found.");

        var images = await db.Images
            .Where(i => i.CardId == image.CardId)
            .OrderBy(i => i.Position)
            .ToListAsync();

        if (newPosition < 0 || newPosition >= images.Count)
            throw new ArgumentOutOfRangeException(nameof(newPosition));

        images.Remove(image);

        images.Insert(newPosition, image);

        for (var i = 0; i < images.Count; i++)
            images[i].Position = i;

        await db.SaveChangesAsync();

        return image;
    }

    public async Task<Image> MoveToCardAsync(int imageId, int targetCardId)
    {
        var image = await db.Images
                        .FirstOrDefaultAsync(i => i.Id == imageId)
                    ?? throw new KeyNotFoundException("Image not found.");

        var cardExists = await db.Cards
            .AnyAsync(c => c.Id == targetCardId);

        if (!cardExists)
            throw new KeyNotFoundException("Target card not found.");

        var nextPosition = await db.Images
            .CountAsync(i => i.CardId == targetCardId);

        image.CardId = targetCardId;
        image.Position = nextPosition;

        await db.SaveChangesAsync();

        return image;
    }

    public async Task AddTagAsync(int imageId, int tagId)
    {
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
        var imageTag = await db.ImageTags
            .FirstOrDefaultAsync(it =>
                it.ImageId == imageId &&
                it.TagId == tagId);

        if (imageTag is null)
            return;

        db.ImageTags.Remove(imageTag);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public async Task DeleteAsync(int imageId)
    {
        var image = await db.Images
                        .FirstOrDefaultAsync(i => i.Id == imageId)
                    ?? throw new KeyNotFoundException("Image not found.");

        db.Images.Remove(image);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
