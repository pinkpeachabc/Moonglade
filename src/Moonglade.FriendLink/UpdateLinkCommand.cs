﻿using MediatR;
using Moonglade.Data.Entities;
using Moonglade.Data.Infrastructure;
using Moonglade.Utils;

namespace Moonglade.FriendLink;

public class UpdateLinkCommand : AddLinkCommand
{
    public Guid Id { get; set; }
}

public class UpdateLinkCommandHandler : AsyncRequestHandler<UpdateLinkCommand>
{
    private readonly IRepository<FriendLinkEntity> _repo;

    public UpdateLinkCommandHandler(IRepository<FriendLinkEntity> repo) => _repo = repo;

    protected override async Task Handle(UpdateLinkCommand request, CancellationToken ct)
    {
        if (!Uri.IsWellFormedUriString(request.LinkUrl, UriKind.Absolute))
        {
            throw new InvalidOperationException($"{nameof(request.LinkUrl)} is not a valid url.");
        }

        var link = await _repo.GetAsync(request.Id);
        if (link is not null)
        {
            link.Title = request.Title;
            link.LinkUrl = Helper.SterilizeLink(request.LinkUrl);

            await _repo.UpdateAsync(link, ct);
        }
    }
}