using StickyNotes.Core;
using StickyNotes.Core.Sync;

namespace StickyNotes.Core.Tests;

/// <summary>
/// Gerçek Memos sunucusuna karşı uçtan uca tur. MEMOS_TEST_URL, MEMOS_TEST_TOKEN ve MEMOS_TEST_SPACE
/// (spaces/&lt;id&gt;, kullanıcı üye olmalı) tanımlı değilse test atlanır.
/// Token ve adres hiçbir çıktıya yazılmaz. Ortam değişkenleri yoksa test atlanır (Skipped).
/// </summary>
public class MemosLiveTests
{
    [SkippableFact]
    public async Task Live_TwoWaySync_WithDockerMemos()
    {
        var url = Environment.GetEnvironmentVariable("MEMOS_TEST_URL") ?? "";
        var token = Environment.GetEnvironmentVariable("MEMOS_TEST_TOKEN") ?? "";
        var space = Environment.GetEnvironmentVariable("MEMOS_TEST_SPACE") ?? "";
        Skip.If(string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(space),
            "MEMOS_TEST_URL, MEMOS_TEST_TOKEN ve MEMOS_TEST_SPACE tanımlı değil.");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var client = new MemosClient(http, url, token);
        var repo = TestRepo.Create(out var path);
        try
        {
            var settings = new SyncSettings { Enabled = true, Url = url, Space = space, Visibility = MemosVisibility.Space };
            var engine = new SyncEngine(client, repo);

            // Yerel → sunucu
            var local = repo.Create(NoteType.Text, NoteColors.Default, NoteContent.CreateText("canlı test **kalın**"));
            var r1 = await engine.RunOnceAsync(settings);
            Assert.Equal(SyncStatus.Ok, r1.Status);
            var memoName = "memos/" + local.Id.Replace("-", "");
            var remote = await client.GetMemoAsync(memoName);
            Assert.NotNull(remote);
            Assert.Equal("canlı test **kalın**", remote!.Content);

            // Sunucu → yerel
            await client.UpdateMemoAsync(memoName, "sunucudan gelen", "NORMAL");
            var r2 = await engine.RunOnceAsync(settings);
            Assert.Equal(SyncStatus.Ok, r2.Status);
            Assert.Equal("sunucudan gelen", NoteContent.ParseText(repo.Get(local.Id)!.Content));

            // Çöp → ARCHIVED
            repo.SoftDelete(local.Id);
            var r3 = await engine.RunOnceAsync(settings);
            Assert.Equal(SyncStatus.Ok, r3.Status);
            Assert.Equal("ARCHIVED", (await client.GetMemoAsync(memoName))!.State);
        }
        finally
        {
            TestRepo.Cleanup(path);
        }
    }
}
