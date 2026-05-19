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

public class CardService(AppDbContext db) : ICardService
{
    public async Task<Card> CreateAsync(string title, CardType cardType)
    {
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
        return await db.Cards
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Card>> GetAllAsync()
    {
        return await db.Cards
            .Include(c => c.CoverImage)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Card> RenameAsync(int id, string title)
    {
        var card = await db.Cards
                       .FirstOrDefaultAsync(c => c.Id == id)
                   ?? throw new KeyNotFoundException("Card not found.");

        card.Title = title.Trim();

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return card;
    }

    public async Task<Card> SetCoverAsync(int cardId, int imageId)
    {
        var card = await db.Cards
                       .FirstOrDefaultAsync(c => c.Id == cardId)
                   ?? throw new KeyNotFoundException("Card not found.");

        var image = await db.Images
                        .FirstOrDefaultAsync(i => i.Id == imageId)
                    ?? throw new KeyNotFoundException("Image not found.");

        if (image.CardId != cardId)
            throw new InvalidOperationException(
                "Image does not belong to card.");

        card.CoverImageId = imageId;

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return card;
    }

    public async Task DeleteAsync(int id)
    {
        var card = await db.Cards
                       .FirstOrDefaultAsync(c => c.Id == id)
                   ?? throw new KeyNotFoundException("Card not found.");

        db.Cards.Remove(card);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
