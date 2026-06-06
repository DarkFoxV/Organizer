using Organize.Organizer.Core;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Interfaces;
using Organizer.Application.ViewModels.Components;
using Organizer.Core.Helpers;

namespace Organizer.Application.Services;

public sealed class CardItemViewModelFactory(IImageService imageService)
{
    public CardItemViewModel Create(SearchCardResult card)
    {
        var thumbnail = ImageHelper.ToBitmap(card.CoverThumbnail, maxWidth: 204, maxHeight: 164);

        return new CardItemViewModel
        {
            Id = card.CoverImageId ?? 0,
            CardId = card.CardId,
            Thumbnail = thumbnail,
            Filename = card.CoverFilename,
            MimeType = card.CoverMimeType ?? "application/octet-stream",
            Description = card.CoverDescription,
            CreatedAt = card.CreatedAt.ToString("dd/MM/yyyy"),
            LoadImageDataStreamAsync = card.CoverImageId is null
                ? null
                : () => imageService.GetDataAsync(card.CoverImageId.Value),
            IsGroup = card.CardType == CardType.Group,
            ImageCount = card.ImageCount,
            IsLoaded = thumbnail is not null
        };
    }
}
