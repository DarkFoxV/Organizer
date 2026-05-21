using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Organize.Organizer.Core;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Helpers;
using Organize.Organizer.Core.Interfaces;

namespace Organizer.Application.Services;

public class TagService(AppDbContextFactory dbFactory) : ITagService
{
    public async Task<Tag> CreateAsync(string name, TagColor color)
    {
        await using var db = dbFactory.Create();

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

        return tag;
    }

    public async Task<Tag?> GetByIdAsync(int id)
    {
        await using var db = dbFactory.Create();

        return await db.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Tag?> GetByNameAsync(string name)
    {
        await using var db = dbFactory.Create();

        name = TagNormalizer.Normalize(name);

        return await db.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == name);
    }

    public async Task<List<Tag>> GetAllAsync()
    {
        await using var db = dbFactory.Create();

        return await db.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<Tag> RenameAsync(int id, string newName)
    {
        await using var db = dbFactory.Create();

        newName = TagNormalizer.Normalize(newName);

        var tag = await db.Tags
                      .FirstOrDefaultAsync(t => t.Id == id)
                  ?? throw new KeyNotFoundException("Tag not found.");

        var exists = await db.Tags
            .AnyAsync(t => t.Id != id && t.Name == newName);

        if (exists)
            throw new InvalidOperationException($"Tag '{newName}' already exists.");

        tag.Name = newName;

        await db.SaveChangesAsync();

        return tag;
    }

    public async Task<Tag> ChangeColorAsync(int id, TagColor color)
    {
        await using var db = dbFactory.Create();

        var tag = await db.Tags
                      .FirstOrDefaultAsync(t => t.Id == id)
                  ?? throw new KeyNotFoundException("Tag not found.");

        tag.Color = color;

        await db.SaveChangesAsync();

        return tag;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = dbFactory.Create();

        var tag = await db.Tags
                      .FirstOrDefaultAsync(t => t.Id == id)
                  ?? throw new KeyNotFoundException("Tag not found.");

        db.Tags.Remove(tag);

        await db.SaveChangesAsync();
    }
}