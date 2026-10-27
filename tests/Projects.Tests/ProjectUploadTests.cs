using System.IO.Compression;
using System.Security.Cryptography;
using Application.Features.MapLayers;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Files;
using Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using static Application.Features.Projects.ProjectUploadRules;

namespace Projects.Tests;

public class ProjectUploadRulesTests
{
    private const long Max = 50L * 1024 * 1024 * 1024, Chunk = 16L * 1024 * 1024;

    [Fact]
    public void Start_rejects_empty_names_empty_files_and_files_over_the_cap()
    {
        Assert.Null(ValidateStart("plan.dwg", 10L * 1024 * 1024 * 1024, Max, usesFileSystem: true));
        Assert.NotNull(ValidateStart("", 10, Max, true));
        Assert.NotNull(ValidateStart("a.pdf", 0, Max, true));
        Assert.NotNull(ValidateStart("a.pdf", Max + 1, Max, true));
        Assert.NotNull(ValidateStart(new string('x', MaxFileNameLength + 1), 10, Max, true));
    }

    [Fact]
    public void Database_mode_asks_for_the_attachment_folder_when_the_file_is_too_big()
    {
        var message = ValidateStart("model.zip", 500L * 1024 * 1024, 100L * 1024 * 1024, usesFileSystem: false);
        Assert.NotNull(message);
        Assert.Contains("FileStorage__Mode=FileSystem", message);
    }

    [Theory]
    [InlineData(0, 100, 0, 40, ChunkDecision.Write)]
    [InlineData(40, 100, 40, 40, ChunkDecision.Write)]
    [InlineData(80, 100, 80, 20, ChunkDecision.Write)]
    [InlineData(40, 100, 0, 40, ChunkDecision.AlreadyReceived)]     // إعادة جزء وصل قبلاً (ضاع الرد)
    [InlineData(40, 100, 20, 20, ChunkDecision.AlreadyReceived)]
    [InlineData(40, 100, 60, 20, ChunkDecision.OutOfOrder)]          // ثغرة قبله
    [InlineData(40, 100, 30, 40, ChunkDecision.OutOfOrder)]          // يتداخل مع المستلم ويتجاوزه
    [InlineData(80, 100, 80, 21, ChunkDecision.BadLength)]           // يتجاوز حجم الملف المعلن
    [InlineData(0, 100, 0, 0, ChunkDecision.BadLength)]
    [InlineData(0, 100, -10, 20, ChunkDecision.BadLength)]
    public void Chunk_decisions(long received, long size, long offset, long length, ChunkDecision expected) =>
        Assert.Equal(expected, DecideChunk(received, size, chunkMax: 50, offset, length));

    [Fact]
    public void A_chunk_longer_than_the_server_chunk_size_is_refused()
    {
        Assert.Equal(ChunkDecision.BadLength, DecideChunk(0, Max, Chunk, 0, Chunk + 1));
        Assert.Equal(ChunkDecision.Write, DecideChunk(0, Max, Chunk, 0, Chunk));
    }

    [Fact]
    public void Complete_needs_every_byte_and_a_receiving_session()
    {
        Assert.Null(CheckComplete(new ProjectUpload { Size = 10, Received = 10, Status = ProjectUploadStatus.Receiving }));
        Assert.NotNull(CheckComplete(new ProjectUpload { Size = 10, Received = 9, Status = ProjectUploadStatus.Receiving }));
        Assert.NotNull(CheckComplete(new ProjectUpload { Size = 10, Received = 10, Status = ProjectUploadStatus.Processing }));
    }

    [Fact]
    public void Every_status_has_an_arabic_name()
    {
        foreach (var status in Enum.GetValues<ProjectUploadStatus>())
            Assert.NotEqual(status.ToString(), StatusAr(status));
    }
}

public class MapFileRulesStreamTests
{
    private static byte[] Zip(params string[] entries)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var name in entries)
                using (var writer = new StreamWriter(zip.CreateEntry(name).Open())) writer.Write("x");
        return memory.ToArray();
    }

    // سجل Detected يقارن مصفوفة الأنواع بالمرجع، فنقارن نصاً مكافئاً
    private static string Describe(MapFileRules.Detected? d) => d == null ? "null" : d.ContentType + ":" + string.Join(",", d.Kinds);

    [Fact]
    public void Stream_detection_matches_the_byte_array_detection()
    {
        var pdf = "%PDF-1.7\nbody"u8.ToArray();
        var docx = Zip("[Content_Types].xml", "word/document.xml");
        var withExe = Zip("readme.txt", "setup.exe");
        var text = "a,b,c\n1,2,3"u8.ToArray();

        foreach (var (data, name) in new[] { (pdf, "a.pdf"), (docx, "a.docx"), (docx, "a.xlsx"), (withExe, "a.zip"), (text, "a.csv"), (text, "a.exe"), (pdf, "a.exe") })
        {
            using var stream = new MemoryStream(data);
            Assert.Equal(Describe(MapFileRules.Detect(data, name)), Describe(MapFileRules.Detect(stream, name)));
        }

        Assert.Equal("application/pdf", MapFileRules.Detect(new MemoryStream(pdf), "a.pdf")!.ContentType);
        Assert.NotNull(MapFileRules.Detect(new MemoryStream(docx), "a.docx"));
        Assert.Null(MapFileRules.Detect(new MemoryStream(docx), "a.xlsx"));        // الامتداد لا يطابق المحتوى
        Assert.Null(MapFileRules.Detect(new MemoryStream(withExe), "a.zip"));       // ملف ممنوع داخل الأرشيف
        Assert.Null(MapFileRules.Detect(new MemoryStream(pdf), "a.exe"));
        Assert.Null(MapFileRules.Detect(new MemoryStream([]), "a.pdf"));
    }
}

/// <summary>الأجزاء على القرص فعلاً (مجلد مؤقت) ثم الإنهاء: البصمة، كشف النوع، النقل إلى المخزن أو إلى القاعدة</summary>
public sealed class ProjectUploadFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ewms-test-" + Guid.NewGuid().ToString("N"));

    public ProjectUploadFilesTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private (ProjectUploadFiles Files, ProjectFileSettings Settings) Create(bool fileSystem)
    {
        var values = new Dictionary<string, string?>();
        if (fileSystem)
        {
            Directory.CreateDirectory(Path.Combine(_root, "store"));
            values["FileStorage:Mode"] = "FileSystem";
            values["FileStorage:Root"] = Path.Combine(_root, "store");
        }
        else values["Projects:TempFolder"] = Path.Combine(_root, "temp");

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var storage = new FileStorageSettings(configuration);
        var settings = new ProjectFileSettings(configuration, storage);
        return (new ProjectUploadFiles(settings, new FileStore(storage)), settings);
    }

    private static byte[] Pdf(int size)
    {
        var data = new byte[size];
        new Random(7).NextBytes(data);
        "%PDF-1.7\n"u8.CopyTo(data);
        return data;
    }

    private static async Task SendAsync(ProjectUploadFiles files, Guid id, byte[] data, long chunk)
    {
        for (long offset = 0; offset < data.Length; offset += chunk)
        {
            var length = Math.Min(chunk, data.Length - offset);
            await files.WriteChunkAsync(id, offset, new MemoryStream(data, (int)offset, (int)length), length, CancellationToken.None);
        }
    }

    [Fact]
    public async Task Chunks_assemble_into_the_original_file()
    {
        var (files, _) = Create(fileSystem: false);
        var id = Guid.NewGuid();
        var data = Pdf(1_000_003);

        await SendAsync(files, id, data, chunk: 300_000);

        Assert.Equal(data.Length, files.LengthOf(id));
        await using var part = files.OpenPart(id);
        Assert.Equal(SHA256.HashData(data), await SHA256.HashDataAsync(part));
    }

    [Fact]
    public async Task A_chunk_cut_off_in_the_middle_leaves_nothing_and_can_be_resent()
    {
        var (files, _) = Create(fileSystem: false);
        var id = Guid.NewGuid();
        var data = Pdf(100_000);
        await files.WriteChunkAsync(id, 0, new MemoryStream(data, 0, 40_000), 40_000, CancellationToken.None);

        // الجزء الثاني يعد بـ 40,000 بايت ويصل منه 10,000 فقط (انقطع الاتصال)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            files.WriteChunkAsync(id, 40_000, new MemoryStream(data, 40_000, 10_000), 40_000, CancellationToken.None));
        Assert.Equal(40_000, files.LengthOf(id));

        await files.WriteChunkAsync(id, 40_000, new MemoryStream(data, 40_000, 40_000), 40_000, CancellationToken.None);
        await files.WriteChunkAsync(id, 80_000, new MemoryStream(data, 80_000, 20_000), 20_000, CancellationToken.None);
        await using var part = files.OpenPart(id);
        Assert.Equal(SHA256.HashData(data), await SHA256.HashDataAsync(part));
    }

    [Fact]
    public async Task Resending_a_chunk_overwrites_it_instead_of_appending()
    {
        var (files, _) = Create(fileSystem: false);
        var id = Guid.NewGuid();
        var first = new byte[1000];
        var second = Enumerable.Repeat((byte)9, 1000).ToArray();

        await files.WriteChunkAsync(id, 0, new MemoryStream(first), 1000, CancellationToken.None);
        await files.WriteChunkAsync(id, 0, new MemoryStream(second), 1000, CancellationToken.None);

        Assert.Equal(1000, files.LengthOf(id));
        await using var part = files.OpenPart(id);
        Assert.Equal(SHA256.HashData(second), await SHA256.HashDataAsync(part));
    }

    [Fact]
    public async Task Delete_removes_the_part_file()
    {
        var (files, _) = Create(fileSystem: false);
        var id = Guid.NewGuid();
        await files.WriteChunkAsync(id, 0, new MemoryStream(new byte[10]), 10, CancellationToken.None);
        files.Delete(id);
        Assert.Equal(0, files.LengthOf(id));
    }

    private static (ProjectUploadFinalizer Finalizer, StubUploads Uploads, StubDocuments Documents) FinalizerFor(
        ProjectUploadFiles files, ProjectFileSettings settings, Project project)
    {
        var uploads = new StubUploads();
        var documents = new StubDocuments();
        return (new ProjectUploadFinalizer(uploads, files, new StubProjects(project), documents, settings), uploads, documents);
    }

    private static ProjectUpload UploadOf(Guid id, string name, long size) => new()
    {
        Id = id, ProjectId = 5, FileName = name, Size = size, Received = size, Title = "مخطط", Status = ProjectUploadStatus.Processing
    };

    [Fact]
    public async Task Finalizing_in_folder_mode_moves_the_file_to_the_store_with_its_hash()
    {
        var (files, settings) = Create(fileSystem: true);
        var (finalizer, uploads, documents) = FinalizerFor(files, settings, new Project { Id = 5, Stage = ProjectStage.Study });
        var id = Guid.NewGuid();
        var data = Pdf(700_000);
        await SendAsync(files, id, data, 250_000);
        await uploads.AddAsync(UploadOf(id, "plan.pdf", data.Length));

        var rejection = await finalizer.FinalizeAsync(id, CancellationToken.None);

        Assert.Null(rejection);
        Assert.NotNull(documents.Added);
        Assert.Null(documents.AddedContent);   // المحتوى في المجلد لا في القاعدة
        Assert.Equal("application/pdf", documents.Added!.ContentType);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(), documents.Added.Sha256);
        Assert.Matches(@"^projects/\d{4}/\d{2}/[0-9a-f]{32}$", documents.Added.StorageKey!);

        var stored = Path.Combine(_root, "store", documents.Added.StorageKey!.Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal(data, await File.ReadAllBytesAsync(stored));
        Assert.Equal(0, files.LengthOf(id));   // الملف المؤقت نُقل
    }

    [Fact]
    public async Task Finalizing_in_database_mode_hands_the_bytes_to_the_service()
    {
        var (files, settings) = Create(fileSystem: false);
        var (finalizer, uploads, documents) = FinalizerFor(files, settings, new Project { Id = 5, Stage = ProjectStage.Execution });
        var id = Guid.NewGuid();
        var data = Pdf(50_000);
        await SendAsync(files, id, data, 20_000);
        await uploads.AddAsync(UploadOf(id, "plan.pdf", data.Length));

        Assert.Null(await finalizer.FinalizeAsync(id, CancellationToken.None));
        Assert.Null(documents.Added!.StorageKey);
        Assert.Equal(data, documents.AddedContent);
        Assert.Equal(0, files.LengthOf(id));
    }

    [Fact]
    public async Task A_forbidden_file_type_is_rejected_by_content_not_by_name()
    {
        var (files, settings) = Create(fileSystem: false);
        var (finalizer, uploads, documents) = FinalizerFor(files, settings, new Project { Id = 5, Stage = ProjectStage.Study });
        var id = Guid.NewGuid();
        var notPdf = new byte[5000];                       // لا بصمة معروفة، والاسم يقول PDF
        await SendAsync(files, id, notPdf, 5000);
        await uploads.AddAsync(UploadOf(id, "fake.pdf", notPdf.Length));

        var rejection = await finalizer.FinalizeAsync(id, CancellationToken.None);

        Assert.NotNull(rejection);
        Assert.Null(documents.Added);
    }

    [Fact]
    public async Task A_size_mismatch_or_a_finished_project_stops_the_finalization()
    {
        var (files, settings) = Create(fileSystem: false);
        var id = Guid.NewGuid();
        var data = Pdf(1000);
        await SendAsync(files, id, data, 1000);

        var (finalizer, uploads, documents) = FinalizerFor(files, settings, new Project { Id = 5, Stage = ProjectStage.Study });
        await uploads.AddAsync(UploadOf(id, "a.pdf", size: 999));
        Assert.NotNull(await finalizer.FinalizeAsync(id, CancellationToken.None));

        var (closed, closedUploads, closedDocs) = FinalizerFor(files, settings, new Project { Id = 5, Stage = ProjectStage.Closed });
        await closedUploads.AddAsync(UploadOf(id, "a.pdf", data.Length));
        Assert.NotNull(await closed.FinalizeAsync(id, CancellationToken.None));
        Assert.Null(documents.Added);
        Assert.Null(closedDocs.Added);
    }
}
