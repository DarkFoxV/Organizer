using Microsoft.EntityFrameworkCore;
using Organize.Organizer.Core;

namespace Organize.Organizer.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ImageTag> ImageTags => Set<ImageTag>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── Card ─────────────────────────────────────────────────────────────
        mb.Entity<Card>(e =>
        {
            e.HasKey(c => c.Id);

            e.Property(c => c.Title)
                .IsRequired()
                .HasMaxLength(200);

            e.Property(c => c.CardType)
                .HasConversion<string>() // salva "Single" / "Group" no banco
                .IsRequired();

            // Capa: relação opcional para uma das imagens do card
            e.HasOne(c => c.CoverImage)
                .WithMany()
                .HasForeignKey(c => c.CoverImageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Image ─────────────────────────────────────────────────────────────
        mb.Entity<Image>(e =>
        {
            e.HasKey(i => i.Id);

            // Um card tem muitas imagens; deletar card deleta imagens
            e.HasOne(i => i.Card)
                .WithMany(c => c.Images)
                .HasForeignKey(i => i.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            // Posição única por card
            e.HasIndex(i => new { i.CardId, i.Position })
                .IsUnique();

            e.Property(i => i.Filename).IsRequired().HasMaxLength(500);
            e.Property(i => i.MimeType).HasMaxLength(100);
            e.Property(i => i.Description).HasMaxLength(2000);
        });

        // ── Tag ───────────────────────────────────────────────────────────────
        mb.Entity<Tag>(e =>
        {
            e.HasKey(t => t.Id);

            e.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(100);
            
            e.Property(c => c.Color)
                .HasConversion<string>()
                .IsRequired();
            
            e.HasIndex(t => t.Name).IsUnique();
            

        });

        // ── ImageTag (N:N) ────────────────────────────────────────────────────
        mb.Entity<ImageTag>(e =>
        {
            e.HasKey(it => new { it.ImageId, it.TagId });

            e.HasOne(it => it.Image)
                .WithMany(i => i.ImageTags)
                .HasForeignKey(it => it.ImageId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(it => it.Tag)
                .WithMany(t => t.ImageTags)
                .HasForeignKey(it => it.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}