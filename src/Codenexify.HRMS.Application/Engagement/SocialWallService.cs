using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Engagement;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Engagement;

public sealed class PostInput
{
    [Required, StringLength(2000, MinimumLength = 2), Display(Name = "What's on your mind?")] public string Body { get; set; } = string.Empty;
}

public sealed class PraiseInput
{
    [Required, MinLength(1, ErrorMessage = "Choose at least one colleague."), Display(Name = "Colleagues")] public List<int> EmployeeIds { get; set; } = [];
    [Required, StringLength(60)] public string Badge { get; set; } = string.Empty;
    [Required, StringLength(1000, MinimumLength = 5), Display(Name = "Why?")] public string Body { get; set; } = string.Empty;
}

public sealed class PollInput
{
    [Required, StringLength(500, MinimumLength = 5)] public string Question { get; set; } = string.Empty;
    public List<string> Options { get; set; } = [];
    [Range(1, 30), Display(Name = "Open for (days)")] public int Days { get; set; } = 3;
}

public sealed class AnnouncementPostInput
{
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string Body { get; set; } = string.Empty;
    [Range(0, 60), Display(Name = "Pin to the top for (days, 0 = don't pin)")] public int PinDays { get; set; } = 7;
    [Display(Name = "Notify everyone")] public bool Notify { get; set; } = true;
    [Display(Name = "Allow comments")] public bool AllowComments { get; set; } = true;
}

public sealed record PollOptionView(int Id, string Text, int Votes, int Percent);
public sealed record PollView(IReadOnlyList<PollOptionView> Options, int TotalVotes, int? MyOptionId, bool Closed, bool ResultsVisible, DateTime? ClosesAtUtc);
public sealed record CommentView(int Id, string AuthorName, string Body, DateTime CreatedAtUtc, bool CanDelete);
public sealed record RecipientView(int EmployeeId, string Name);
public sealed record PostView(int Id, PostKind Kind, int AuthorUserId, string AuthorName, string? Title, string Body, string? Badge,
    IReadOnlyList<RecipientView> Recipients, bool IsPinned, DateTime CreatedAtUtc, int LikeCount, int CommentCount, bool LikedByMe,
    bool CanDelete, bool CanPin, bool CommentsEnabled, PollView? Poll, IReadOnlyList<CommentView> Comments);

public interface ISocialWallService
{
    /// <summary>Badges offered when praising a colleague.</summary>
    IReadOnlyList<string> Badges { get; }
    Task<PagedResult<PostView>> FeedAsync(int page, PostKind? kind, bool canModerate, CancellationToken ct = default);
    Task<PostView?> GetAsync(int postId, bool canModerate, bool allComments, CancellationToken ct = default);
    Task<Result<int>> PostAsync(PostInput input, CancellationToken ct = default);
    Task<Result<int>> PraiseAsync(PraiseInput input, CancellationToken ct = default);
    Task<Result<int>> PollAsync(PollInput input, CancellationToken ct = default);
    Task<Result<int>> AnnounceAsync(AnnouncementPostInput input, bool canModerate, CancellationToken ct = default);
    Task<Result<bool>> ToggleLikeAsync(int postId, CancellationToken ct = default);
    Task<Result> CommentAsync(int postId, string body, CancellationToken ct = default);
    Task<Result> DeleteCommentAsync(int commentId, bool canModerate, CancellationToken ct = default);
    Task<Result> VoteAsync(int postId, int optionId, CancellationToken ct = default);
    Task<Result> DeleteAsync(int postId, bool canModerate, CancellationToken ct = default);
    Task<Result> SetPinnedAsync(int postId, bool pinned, bool canModerate, CancellationToken ct = default);
    Task<IReadOnlyList<PostView>> PinnedAnnouncementsAsync(int take, CancellationToken ct = default);
    Task<IReadOnlyList<Common.LookupOption>> ColleaguesAsync(CancellationToken ct = default);
}

public sealed class SocialWallService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications) : ISocialWallService
{
    private const int PageSize = 15;
    private const int CommentPreview = 3;

    public IReadOnlyList<string> Badges { get; } = ["Team player", "Problem solver", "Customer first", "Above and beyond", "Great mentor", "Innovator", "Thank you"];

    private int Me => context.UserId is int id && id > 0 ? id : throw new TenantIsolationException("No signed-in user.");

    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == Me).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? throw new TenantIsolationException("Unknown user.");

    private IQueryable<WallPost> Visible => db.WallPosts.AsNoTracking();

    public async Task<PagedResult<PostView>> FeedAsync(int page, PostKind? kind, bool canModerate, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var now = clock.UtcNow;
        var q = Visible;
        if (kind is { } k) q = q.Where(p => p.Kind == k);
        var total = await q.CountAsync(ct);
        var posts = await q
            .OrderByDescending(p => p.IsPinned && (p.PinnedUntilUtc == null || p.PinnedUntilUtc > now))
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Include(p => p.Recipients).Include(p => p.Options)
            .ToListAsync(ct);
        return new PagedResult<PostView>(await ProjectAsync(posts, canModerate, allComments: false, ct), total, page, PageSize);
    }

    public async Task<PostView?> GetAsync(int postId, bool canModerate, bool allComments, CancellationToken ct = default)
    {
        var post = await Visible.Include(p => p.Recipients).Include(p => p.Options).FirstOrDefaultAsync(p => p.Id == postId, ct);
        return post is null ? null : (await ProjectAsync([post], canModerate, allComments, ct))[0];
    }

    public async Task<IReadOnlyList<PostView>> PinnedAnnouncementsAsync(int take, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var posts = await Visible.Where(p => p.Kind == PostKind.Announcement && p.IsPinned && (p.PinnedUntilUtc == null || p.PinnedUntilUtc > now))
            .OrderByDescending(p => p.Id).Take(take).ToListAsync(ct);
        return await ProjectAsync(posts, canModerate: false, allComments: false, ct);
    }

    private async Task<IReadOnlyList<PostView>> ProjectAsync(IReadOnlyList<WallPost> posts, bool canModerate, bool allComments, CancellationToken ct)
    {
        if (posts.Count == 0) return [];
        var me = context.UserId ?? 0;
        var now = clock.UtcNow;
        var ids = posts.Select(p => p.Id).ToList();
        var liked = (await db.WallReactions.AsNoTracking().Where(r => ids.Contains(r.PostId) && r.UserId == me).Select(r => r.PostId).ToListAsync(ct)).ToHashSet();
        var myVotes = await db.PollVotes.AsNoTracking().Where(v => ids.Contains(v.PostId) && v.UserId == me).ToDictionaryAsync(v => v.PostId, v => v.OptionId, ct);
        var comments = await db.WallComments.AsNoTracking().Where(c => ids.Contains(c.PostId)).OrderByDescending(c => c.Id)
            .Select(c => new { c.Id, c.PostId, c.AuthorUserId, c.AuthorName, c.Body, c.CreatedAtUtc }).ToListAsync(ct);

        return posts.Select(p =>
        {
            PollView? poll = null;
            if (p.Kind == PostKind.Poll)
            {
                var closed = p.PollClosesAtUtc is { } c && c <= now;
                int? mine = myVotes.TryGetValue(p.Id, out var o) ? o : null;
                var visible = closed || mine is not null || canModerate;
                var total = p.Options.Sum(x => x.VoteCount);
                poll = new PollView(p.Options.OrderBy(x => x.SortOrder).Select(x => new PollOptionView(x.Id, x.Text,
                    visible ? x.VoteCount : 0, visible && total > 0 ? (int)Math.Round(100.0 * x.VoteCount / total) : 0)).ToList(),
                    visible ? total : 0, mine, closed, visible, p.PollClosesAtUtc);
            }
            var postComments = comments.Where(c => c.PostId == p.Id);
            if (!allComments) postComments = postComments.Take(CommentPreview);
            return new PostView(p.Id, p.Kind, p.AuthorUserId, p.AuthorName, p.Title, p.Body, p.Badge,
                p.Recipients.Select(r => new RecipientView(r.EmployeeId, r.EmployeeName)).ToList(),
                p.IsPinned && (p.PinnedUntilUtc is null || p.PinnedUntilUtc > now), p.CreatedAtUtc, p.LikeCount, p.CommentCount, liked.Contains(p.Id),
                canModerate || p.AuthorUserId == me, canModerate, p.CommentsEnabled, poll,
                postComments.Reverse().Select(c => new CommentView(c.Id, c.AuthorName, c.Body, c.CreatedAtUtc, canModerate || c.AuthorUserId == me)).ToList());
        }).ToList();
    }

    public async Task<Result<int>> PostAsync(PostInput input, CancellationToken ct = default)
    {
        var post = new WallPost { Kind = PostKind.Post, AuthorUserId = Me, AuthorName = await MyNameAsync(ct), Body = input.Body.Trim() };
        db.WallPosts.Add(post);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(post.Id);
    }

    public async Task<Result<int>> PraiseAsync(PraiseInput input, CancellationToken ct = default)
    {
        var ids = input.EmployeeIds.Distinct().ToList();
        if (ids.Count is 0 or > 10) return Result<int>.Fail("Choose between 1 and 10 colleagues.");
        if (!Badges.Contains(input.Badge)) return Result<int>.Fail("Choose a badge.");
        var people = await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id) && e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive)
            .Select(e => new { e.Id, e.FirstName, e.MiddleName, e.LastName, e.UserId }).ToListAsync(ct);
        if (people.Count != ids.Count) return Result<int>.Fail("Choose current colleagues only.");
        if (people.Any(p => p.UserId == Me)) return Result<int>.Fail("You can't praise yourself — but well done!");
        string Name(string f, string? m, string l) => string.Join(' ', new[] { f, m, l }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var author = await MyNameAsync(ct);
        var post = new WallPost
        {
            Kind = PostKind.Praise, AuthorUserId = Me, AuthorName = author, Body = input.Body.Trim(), Badge = input.Badge,
            Recipients = people.Select(p => new WallPostRecipient { EmployeeId = p.Id, EmployeeName = Name(p.FirstName, p.MiddleName, p.LastName) }).ToList()
        };
        db.WallPosts.Add(post);
        await db.SaveChangesAsync(ct);
        notifications.Queue(people.Where(p => p.UserId is not null).Select(p => p.UserId!.Value), $"{author} praised you: {input.Badge}",
            input.Body.Trim(), $"/me/wall/post/{post.Id}");
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(post.Id);
    }

    public async Task<Result<int>> PollAsync(PollInput input, CancellationToken ct = default)
    {
        var options = input.Options.Select(o => o?.Trim() ?? "").Where(o => o.Length > 0).ToList();
        if (options.Count is < 2 or > 6) return Result<int>.Fail("Give between 2 and 6 options.");
        if (options.Any(o => o.Length > 150)) return Result<int>.Fail("Options can be at most 150 characters.");
        if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count) return Result<int>.Fail("Options must be different.");
        var post = new WallPost
        {
            Kind = PostKind.Poll, AuthorUserId = Me, AuthorName = await MyNameAsync(ct), Body = input.Question.Trim(),
            PollClosesAtUtc = clock.UtcNow.AddDays(input.Days),
            Options = options.Select((o, i) => new PollOption { Text = o, SortOrder = i }).ToList()
        };
        db.WallPosts.Add(post);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(post.Id);
    }

    public async Task<Result<int>> AnnounceAsync(AnnouncementPostInput input, bool canModerate, CancellationToken ct = default)
    {
        if (!canModerate) return Result<int>.Fail("Only HR can post announcements.");
        var author = await MyNameAsync(ct);
        var post = new WallPost
        {
            Kind = PostKind.Announcement, AuthorUserId = Me, AuthorName = author, Title = input.Title.Trim(), Body = input.Body.Trim(),
            IsPinned = input.PinDays > 0, PinnedUntilUtc = input.PinDays > 0 ? clock.UtcNow.AddDays(input.PinDays) : null, CommentsEnabled = input.AllowComments
        };
        db.WallPosts.Add(post);
        await db.SaveChangesAsync(ct);
        if (input.Notify)
        {
            var everyone = await db.Users.AsNoTracking().Where(u => u.IsActive && u.Id != Me).Select(u => u.Id).ToListAsync(ct);
            notifications.Queue(everyone, $"Announcement: {post.Title}", post.Body, $"/me/wall/post/{post.Id}");
            await db.SaveChangesAsync(ct);
        }
        return Result<int>.Ok(post.Id);
    }

    public async Task<Result<bool>> ToggleLikeAsync(int postId, CancellationToken ct = default)
    {
        var me = Me;
        var post = await db.WallPosts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return Result<bool>.Fail("Post not found.");
        var existing = await db.WallReactions.FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == me, ct);
        if (existing is null) db.WallReactions.Add(new WallReaction { PostId = postId, UserId = me });
        else db.WallReactions.Remove(existing);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result<bool>.Fail("Please try again."); }
        post.LikeCount = await db.WallReactions.CountAsync(r => r.PostId == postId, ct);
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(existing is null);
    }

    public async Task<Result> CommentAsync(int postId, string body, CancellationToken ct = default)
    {
        body = body?.Trim() ?? string.Empty;
        if (body.Length is 0 or > 1000) return Result.Fail("Comments must be 1–1000 characters.");
        var post = await db.WallPosts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return Result.Fail("Post not found.");
        if (!post.CommentsEnabled) return Result.Fail("Comments are turned off for this post.");
        var author = await MyNameAsync(ct);
        db.WallComments.Add(new WallComment { PostId = postId, AuthorUserId = Me, AuthorName = author, Body = body });
        post.CommentCount++;
        await db.SaveChangesAsync(ct);
        if (post.AuthorUserId != Me)
        {
            notifications.Queue([post.AuthorUserId], $"{author} commented on your post", body, $"/me/wall/post/{postId}");
            await db.SaveChangesAsync(ct);
        }
        return Result.Ok();
    }

    public async Task<Result> DeleteCommentAsync(int commentId, bool canModerate, CancellationToken ct = default)
    {
        var c = await db.WallComments.FirstOrDefaultAsync(x => x.Id == commentId, ct);
        if (c is null) return Result.Fail("Comment not found.");
        if (c.AuthorUserId != Me && !canModerate) return Result.Fail("You can only delete your own comments.");
        db.WallComments.Remove(c);
        var post = await db.WallPosts.FirstOrDefaultAsync(p => p.Id == c.PostId, ct);
        if (post is not null) post.CommentCount = Math.Max(0, post.CommentCount - 1);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> VoteAsync(int postId, int optionId, CancellationToken ct = default)
    {
        var me = Me;
        var post = await db.WallPosts.Include(p => p.Options).FirstOrDefaultAsync(p => p.Id == postId && p.Kind == PostKind.Poll, ct);
        if (post is null) return Result.Fail("Poll not found.");
        if (post.PollClosesAtUtc is { } c && c <= clock.UtcNow) return Result.Fail("This poll has closed.");
        var option = post.Options.FirstOrDefault(o => o.Id == optionId);
        if (option is null) return Result.Fail("Choose an option.");
        if (await db.PollVotes.AnyAsync(v => v.PostId == postId && v.UserId == me, ct)) return Result.Fail("You have already voted.");
        db.PollVotes.Add(new PollVote { PostId = postId, OptionId = optionId, UserId = me });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result.Fail("You have already voted."); }
        option.VoteCount = await db.PollVotes.CountAsync(v => v.OptionId == optionId, ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int postId, bool canModerate, CancellationToken ct = default)
    {
        var post = await db.WallPosts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return Result.Fail("Post not found.");
        if (post.AuthorUserId != Me && !canModerate) return Result.Fail("You can only delete your own posts.");
        db.WallPosts.Remove(post); // soft delete; reactions, votes and comments stay with the hidden post
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetPinnedAsync(int postId, bool pinned, bool canModerate, CancellationToken ct = default)
    {
        if (!canModerate) return Result.Fail("Only HR can pin posts.");
        var post = await db.WallPosts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return Result.Fail("Post not found.");
        post.IsPinned = pinned;
        post.PinnedUntilUtc = null;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<LookupOption>> ColleaguesAsync(CancellationToken ct = default)
    {
        var me = context.UserId ?? 0;
        var rows = await db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive && e.UserId != me)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new { e.Id, e.FirstName, e.MiddleName, e.LastName, Designation = e.Designation != null ? e.Designation.Name : null }).ToListAsync(ct);
        return rows.Select(r => new LookupOption(r.Id, string.Join(' ', new[] { r.FirstName, r.MiddleName, r.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))), r.Designation)).ToList();
    }
}
