using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Community;

public class ProductPreviewDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = new();
    public int Quantity { get; set; }
    public bool IsAvailable => Quantity > 0;

    public int SoldQuantity { get; set; }
}

public class PostFeedResponse
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid? AssembledProductId { get; set; }
    
    public List<string> AttachmentUrls { get; set; } = new();
    public int ReactionCount { get; set; }
    public int CommentCount { get; set; }
    
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    
    public Guid? ShopId { get; set; }
    public string? ShopName { get; set; }
    public string Role { get; set; } = string.Empty;
    
    public ProductPreviewDto? Product { get; set; }
    
    public string? CurrentUserReaction { get; set; }
}

public class CreatePostDto
{
    public string Title { get; set; } = string.Empty;
    public Guid? AssembledProductId { get; set; }
    public List<string>? AttachmentUrls { get; set; }
}

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public Guid? ActorId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
    public string? RedirectUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReactToPostDto
{
    public string Type { get; set; } = "Like";
}

public class UpdatePostDto
{
    public string? Title { get; set; }
    public Guid? AssembledProductId { get; set; }
    public List<string>? AttachmentUrls { get; set; }
}

public class CreateSystemNotificationDto
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Message { get; set; }

    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string? RedirectUrl { get; set; }
}

public class UpdateNotificationDto
{
    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(1000)]
    public string? Message { get; set; }
}

public class BroadcastNotificationDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Message { get; set; }

    public string? RedirectUrl { get; set; }
}

public class PostReactionDetailResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string ReactionType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateCommentDto
{
    public string Content { get; set; } = string.Empty;
}

public class CommentResponse
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    // Edit History fields
    public bool IsEdited { get; set; }
    public List<CommentEditHistory> EditHistory { get; set; } = new();
}

public class UpdateCommentDto
{
    public string Content { get; set; } = string.Empty;
}

public class CommentEditHistory
{
    public string Content { get; set; } = string.Empty;
    public DateTime EditedAt { get; set; }
}

public class CommentInternalData
{
    public string CurrentContent { get; set; } = string.Empty;
    public List<CommentEditHistory> History { get; set; } = new();
}
