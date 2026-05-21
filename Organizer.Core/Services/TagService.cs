using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Organize.Organizer.Core;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Helpers;
using Organize.Organizer.Core.Interfaces;
using Organize.Organizer.Infrastructure.Data;

namespace Organizer.Application.Services;

public class TagService(AppDbContextFactory dbFactory) : ITagService
{
    public async Task<Tag> CreateAsync(string name, TagColor color)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        name = TagNormalizer.Normalize(name);

        var exists = await db.Tags
            .AnyAsync(t => t.Name == name);

        if (exists)
            throw new InvalidOperationException($"Tag '{name}' already exists.");

        Tag tag = new()
        {
            Name = name,
            Color = color
        };

        db.Tags.Add(tag);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new Tag
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color
        };
    }

    public async Task<Tag?> GetByIdAsync(int id)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        return await db.Tags
            .Where(t => t.Id == id)
            .Select(t => new Tag
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color
            })
            .FirstOrDefaultAsync();
    }

    public async Task<Tag?> GetByNameAsync(string name)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        name = TagNormalizer.Normalize(name);

        return await db.Tags
            .Where(t => t.Name == name)
            .Select(t => new Tag
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<Tag>> GetAllAsync()
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        return await db.Tags
            .OrderBy(t => t.Name)
            .Select(t => new Tag
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color
            })
            .ToListAsync();
    }

    public async Task<Tag> RenameAsync(int id, string newName)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        newName = TagNormalizer.Normalize(newName);

        var tag = await GetTagSummaryAsync(db, id)
                  ?? throw new KeyNotFoundException("Tag not found.");

        var exists = await db.Tags
            .AnyAsync(t => t.Id != id && t.Name == newName);

        if (exists)
            throw new InvalidOperationException($"Tag '{newName}' already exists.");

        var updated = await db.Tags
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Name, newName));

        if (updated == 0)
            throw new KeyNotFoundException("Tag not found.");

        tag.Name = newName;
        return tag;
    }

    public async Task<Tag> ChangeColorAsync(int id, TagColor color)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        var tag = await GetTagSummaryAsync(db, id)
                  ?? throw new KeyNotFoundException("Tag not found.");

        var updated = await db.Tags
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Color, color));

        if (updated == 0)
            throw new KeyNotFoundException("Tag not found.");

        tag.Color = color;
        return tag;
    }

    public async Task DeleteAsync(int id)
    {
        await using var lease = await dbFactory.CreateLeaseAsync();
        var db = lease.Context;

        var deleted = await db.Tags
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync();

        if (deleted == 0)
            throw new KeyNotFoundException("Tag not found.");
    }

    private static Task<Tag?> GetTagSummaryAsync(AppDbContext db, int id)
    {
        return db.Tags
            .Where(t => t.Id == id)
            .Select(t => new Tag
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color
            })
            .FirstOrDefaultAsync();
    }
}
