using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Organize.Organizer.Core;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Interfaces;
using Organize.Organizer.Infrastructure.Data;

namespace Organizer.Application.Services;

public class CardService(AppDbContextFactory dbFactory) : ICardService
{
    public async Task<Card> CreateAsync(string title, CardType cardType)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        Card card = new()
        {
            Title = title.Trim(),
            CardType = cardType
        };

        db.Cards.Add(card);

        await db.SaveChangesAsync();

        var result = new Card
        {
            Id = card.Id,
            Title = card.Title,
            CardType = card.CardType,
            CreatedAt = card.CreatedAt,
            CoverImageId = card.CoverImageId
        };

        db.ChangeTracker.Clear();

        return result;
    }

    public async Task<Card?> GetByIdAsync(int id)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        return await db.Cards
            .Where(c => c.Id == id)
            .Select(c => new Card
            {
                Id = c.Id,
                Title = c.Title,
                CardType = c.CardType,
                CreatedAt = c.CreatedAt,
                CoverImageId = c.CoverImageId,
                Images = c.Images
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
                    .ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<Card>> GetAllAsync()
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        return await db.Cards
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new Card
            {
                Id = c.Id,
                Title = c.Title,
                CardType = c.CardType,
                CreatedAt = c.CreatedAt,
                CoverImageId = c.CoverImageId,
                CoverImage = c.CoverImage == null
                    ? null
                    : new Image
                    {
                        Id = c.CoverImage.Id,
                        CardId = c.CoverImage.CardId,
                        Position = c.CoverImage.Position,
                        Filename = c.CoverImage.Filename,
                        MimeType = c.CoverImage.MimeType,
                        Description = c.CoverImage.Description,
                        CreatedAt = c.CoverImage.CreatedAt,
                        Thumbnail = c.CoverImage.Thumbnail
                    }
            })
            .ToListAsync();
    }

    public async Task<Card> RenameAsync(int id, string title)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;
        title = title.Trim();

        var card = await GetCardSummaryAsync(db, id)
                   ?? throw new KeyNotFoundException("Card not found.");

        var updated = await db.Cards
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.Title, title));

        if (updated == 0)
            throw new KeyNotFoundException("Card not found.");

        card.Title = title;
        return card;
    }

    public async Task<Card> SetCoverAsync(int cardId, int imageId)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        var card = await GetCardSummaryAsync(db, cardId)
                   ?? throw new KeyNotFoundException("Card not found.");

        var image = await db.Images
                        .Where(i => i.Id == imageId)
                        .Select(i => new { i.CardId })
                        .FirstOrDefaultAsync()
                    ?? throw new KeyNotFoundException("Image not found.");

        if (image.CardId != cardId)
            throw new InvalidOperationException(
                "Image does not belong to card.");

        var updated = await db.Cards
            .Where(c => c.Id == cardId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.CoverImageId, imageId));

        if (updated == 0)
            throw new KeyNotFoundException("Card not found.");

        card.CoverImageId = imageId;
        return card;
    }

    public async Task DeleteAsync(int id)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        var deleted = await db.Cards
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync();

        if (deleted == 0)
            throw new KeyNotFoundException("Card not found.");
    }

    private static Task<Card?> GetCardSummaryAsync(AppDbContext db, int id)
    {
        return db.Cards
            .Where(c => c.Id == id)
            .Select(c => new Card
            {
                Id = c.Id,
                Title = c.Title,
                CardType = c.CardType,
                CreatedAt = c.CreatedAt,
                CoverImageId = c.CoverImageId
            })
            .FirstOrDefaultAsync();
    }
}
