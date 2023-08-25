﻿using MediatR;
using Moonglade.Comments.Moderators;
using Moonglade.Configuration;
using Moonglade.Data.Entities;
using Moonglade.Data.Infrastructure;
using Moonglade.Data.Spec;

namespace Moonglade.Comments;

public class CreateCommentCommand : IRequest<(int Status, CommentDetailedItem Item)>
{
    public CreateCommentCommand(Guid postId, CommentRequest payload, string ipAddress)
    {
        PostId = postId;
        Payload = payload;
        IpAddress = ipAddress;
    }

    public Guid PostId { get; set; }

    public CommentRequest Payload { get; set; }

    public string IpAddress { get; set; }
}

public class CreateCommentCommandHandler : IRequestHandler<CreateCommentCommand, (int Status, CommentDetailedItem Item)>
{
    private readonly IBlogConfig _blogConfig;
    private readonly IRepository<PostEntity> _postRepo;
    private readonly ICommentModerator _moderator;
    private readonly IRepository<CommentEntity> _commentRepo;

    public CreateCommentCommandHandler(
        IBlogConfig blogConfig, IRepository<PostEntity> postRepo, ICommentModerator moderator, IRepository<CommentEntity> commentRepo)
    {
        _blogConfig = blogConfig;
        _postRepo = postRepo;
        _moderator = moderator;
        _commentRepo = commentRepo;
    }

    public async Task<(int Status, CommentDetailedItem Item)> Handle(CreateCommentCommand request, CancellationToken ct)
    {
        if (_blogConfig.ContentSettings.EnableWordFilter)
        {
            switch (_blogConfig.ContentSettings.WordFilterMode)
            {
                case WordFilterMode.Mask:
                    request.Payload.Username = await _moderator.ModerateContent(request.Payload.Username);
                    request.Payload.Content = await _moderator.ModerateContent(request.Payload.Content);
                    break;
                case WordFilterMode.Block:
                    if (await _moderator.HasBadWord(request.Payload.Username, request.Payload.Content))
                    {
                        await Task.CompletedTask;
                        return (-1, null);
                    }
                    break;
            }
        }

        var spec = new PostSpec(request.PostId, false);
        var postInfo = await _postRepo.FirstOrDefaultAsync(spec, p => new
        {
            p.Title,
            p.PubDateUtc
        });

        if (_blogConfig.ContentSettings.CloseCommentAfterDays > 0)
        {
            var days = DateTime.UtcNow.Date.Subtract(postInfo.PubDateUtc.GetValueOrDefault()).Days;
            if (days > _blogConfig.ContentSettings.CloseCommentAfterDays)
            {
                return (-2, null);
            }
        }

        var model = new CommentEntity
        {
            Id = Guid.NewGuid(),
            Username = request.Payload.Username,
            CommentContent = request.Payload.Content,
            PostId = request.PostId,
            CreateTimeUtc = DateTime.UtcNow,
            Email = request.Payload.Email,
            IPAddress = request.IpAddress,
            IsApproved = !_blogConfig.ContentSettings.RequireCommentReview
        };

        await _commentRepo.AddAsync(model, ct);

        var item = new CommentDetailedItem
        {
            Id = model.Id,
            CommentContent = model.CommentContent,
            CreateTimeUtc = model.CreateTimeUtc,
            Email = model.Email,
            IpAddress = model.IPAddress,
            IsApproved = model.IsApproved,
            PostTitle = postInfo.Title,
            Username = model.Username
        };

        return (0, item);
    }
}