// Test verisi üretici (ekran görüntüleri / akıcılık testi). Kullanım: dotnet run --project tools/seed -- <data-dir> [adet]
// Gerçekçi içerikli notlar, çeşitli renk/opaklık, bugün/dün/eski tarihler, bir checklist, bir uzun (kaydırma çubuklu) not ve çöpte 3 not yazar.
using StickyNotes.Core;

if (args.Length > 1 && args[0] == "--dump")
{
    // Kayıtlı not opaklıklarını yazdırır (yeniden başlatma doğrulaması): id-kısa | renk | opaklık | başlık
    var r = new NoteRepository(Path.Combine(args[1], "notes.db"));
    foreach (var n in r.ListActive().OrderBy(n => n.CreatedAt))
    {
        var w = r.GetWindow(n.Id);
        var ws = w == null ? "-" : $"open={(w.IsOpen ? 1 : 0)} {w.X:0},{w.Y:0} {w.Width:0}x{w.Height:0}";
        Console.WriteLine($"{n.Id[^6..]} | {n.Type} | {n.Color} | {n.Opacity:0.##} | {ws} | {NoteContent.DisplayTitle(NoteContent.ParseTitle(n.Content), n.CreatedAt)}");
    }
    var s = r.LoadSettings();
    Console.WriteLine("AYAR " + string.Join("; ", s.ToPairs().Select(p => p.Key + "=" + p.Value)));
    return;
}
if (args.Length > 1 && args[0] == "--content")
{
    // En son düzenlenen notun content JSON'unu yazar (UI testleri için).
    Console.WriteLine(new NoteRepository(Path.Combine(args[1], "notes.db")).ListActive().First().Content);
    return;
}
var dir = args.Length > 0 ? args[0] : "seed-data";
var count = args.Length > 1 ? int.Parse(args[1]) : 8;
Directory.CreateDirectory(dir);
foreach (var f in Directory.GetFiles(dir, "notes.db*")) File.Delete(f);

var now = DateTime.Now;
var stamps = new Queue<DateTime>();
var edited = DateTime.UtcNow;
var repo = new NoteRepository(Path.Combine(dir, "notes.db"), () => edited);

string[] colors = { "yellow", "green", "pink", "purple", "blue", "gray", "white", "yellow", "green", "pink", "blue", "purple" };
double[] opac = { 1.0, 0.8, 0.6, 1.0, 0.8, 0.6, 1.0, 0.8, 0.6, 0.9, 0.7, 1.0 };
var texts = new[]
{
    "Toplantı notları\nBütçe taslağı cuma\nTedarikçi dönüşü bekleniyor",
    "Fikirler\nSticky notes için dark tema yok, opaklık var\nSync daha sonra",
    "Doktor randevusu\nSalı 14:30, kimlik götür",
    "Ev işleri\nÇamaşır makinesi, çiçekleri sula, kargo al",
    "Proje bağlantıları\nwiki sayfası, issue listesi, sprint panosu",
    "Telefon\nAnneyi ara, akşam yemeği hakkında konuş",
    "Kitap listesi\nSefiller, Saatleri Ayarlama Enstitüsü, Dune",
    "Hatırlatma\nFaturayı öde, internet sözleşmesini kontrol et",
};
var ids = new List<string>();
for (var i = 0; i < count; i++)
{
    edited = DateTime.UtcNow.AddHours(-(i * 7 + (i > 3 ? 20 : 0)));
    Note n;
    if (i == 1)
    {
        n = repo.Create(NoteType.Text, colors[i], NoteContent.CreateText("- [x] Süt\n- [ ] Ekmek\n- [ ] Yumurta\n- Zeytin", title: "Alışveriş listesi"));
    }
    else if (i == 2)
    {
        var long_ = string.Join("\n", Enumerable.Range(1, 30).Select(k => $"Satır {k}: uzun not içeriği, kaydırma çubuğunu görmek için."));
        n = repo.Create(NoteType.Text, colors[i], NoteContent.CreateText("Uzun not\n" + long_));
    }
    else n = repo.Create(NoteType.Text, colors[i % colors.Length], NoteContent.CreateText(texts[i % texts.Length].Replace("Not", "Not") + (i >= texts.Length ? $"\nKopya {i}" : "")));
    ids.Add(n.Id);
    repo.SetOpacity(n.Id, args.Length > 2 && args[2] == "opaque" ? 1.0 : opac[i % opac.Length]);
    // 4 sütun x n satır düzen (1280x800 ekran için), kartlar üst üste bindirilmesin
    var col = i % 4; var row = i / 4;
    repo.SaveWindow(new NoteWindowState(n.Id, 20 + col * 262, 20 + row * 215, 250, 200, true));
    Thread.Sleep(3);
}
// çöpte 3 not
for (var k = 0; k < 3 && count > 0; k++)
{
    edited = DateTime.UtcNow.AddDays(-k);
    var t = repo.Create(NoteType.Text, "gray", NoteContent.CreateText($"Silinmiş not {k + 1}\nÇöpte duran eski içerik"));
    repo.SoftDelete(t.Id);
}
Console.WriteLine($"{ids.Count} not + 3 çöp: {dir}");
