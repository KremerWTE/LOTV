using System.Security.Claims;
using Lotv.Api.Auth;
using Microsoft.AspNetCore.Http;

namespace Lotv.Tests.Domain;

/// <summary>
/// The app runs as ONE organization by default (Chapters:Enabled=false): nobody carries a chapter scope, so every chapter
/// filter is skipped. The integration suite runs with chapters ON (it was written for per-chapter scoping), so these
/// tests flip the shared switch themselves — in a collection that never runs alongside other tests — and always put it back.
/// </summary>
[CollectionDefinition("ChapterMode", DisableParallelization = true)]
public class ChapterModeCollection { }

[Collection("ChapterMode")]
public class OneOrganizationModeTests
{
    private static ChapterContextService ServiceFor(string role, int? chapterId)
    {
        var claims = new List<Claim> { new("role", role), new(ClaimTypes.NameIdentifier, "u1") };
        if (chapterId is not null) claims.Add(new("chapterId", chapterId.Value.ToString()));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return new ChapterContextService(new HttpContextAccessor { HttpContext = http });
    }

    private static void WithMode(bool enabled, Action body)
    {
        var before = ChapterMode.Enabled;
        try { ChapterMode.Enabled = enabled; body(); }
        finally { ChapterMode.Enabled = before; }
    }

    [Fact]
    public void ChaptersOff_NobodyHasAChapterScope_EvenWithAChapterOnTheirToken()
    {
        WithMode(false, () =>
        {
            Assert.Null(ServiceFor("ChapterStaff", 3).ChapterId);   // every chapter filter is guarded by "has a chapter", so none applies
            Assert.Null(ServiceFor("Volunteer", 7).ChapterId);
            Assert.Null(ServiceFor("HQAdmin", null).ChapterId);
        });
    }

    [Fact]
    public void ChaptersOn_TheOldPerChapterScopeStillWorks()
    {
        WithMode(true, () =>
        {
            Assert.Equal(3, ServiceFor("ChapterStaff", 3).ChapterId);
            Assert.Null(ServiceFor("HQAdmin", null).ChapterId);
            Assert.True(ServiceFor("HQAdmin", null).IsHqAdmin);
        });
    }

    [Fact]
    public void ChaptersOff_EveryCaseEventGoesToOneSharedLiveUpdateGroup()
    {
        WithMode(false, () =>
        {
            Assert.Equal(ChapterMode.GroupFor(1), ChapterMode.GroupFor(2));
            Assert.Equal(ChapterMode.GroupFor(0), ChapterMode.GroupFor(99));
        });
        WithMode(true, () =>
        {
            Assert.Equal("chapter-1", ChapterMode.GroupFor(1));
            Assert.NotEqual(ChapterMode.GroupFor(1), ChapterMode.GroupFor(2));
        });
    }
}
