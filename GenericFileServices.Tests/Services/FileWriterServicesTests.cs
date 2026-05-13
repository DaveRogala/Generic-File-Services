using System.Collections.Generic;
using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using GenericFileServices.Contracts;
using GenericFileServices.Services;
using GenericFileServices.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace GenericFileServices.Tests.Services;

public class FileWriterServicesTests : IDisposable
{
    private readonly Mock<IBlobClientFactory> _factoryMock = new();
    private readonly IFileWriterServices _sut;
    private readonly string _tempDir;

    private const string ConnStr = "connstr";
    private const string Container = "mycontainer";
    private const string BlobPath = "exports/out.csv";

    public FileWriterServicesTests()
    {
        _sut = new FileWriterServices(new Mock<ILogger<FileWriterServices>>().Object, _factoryMock.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), $"gfs_writer_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string[] ReadLines(string fileName) =>
        File.ReadAllLines(Path.Combine(_tempDir, fileName));

    private byte[] ReadBytes(string fileName) =>
        File.ReadAllBytes(Path.Combine(_tempDir, fileName));

    // ── WriteToFile — header and data ────────────────────────────────────────

    [Fact]
    public void WriteToFile_WritesHeaderRowFromPropertyNames()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            Array.Empty<ExportTestDto>(),
            new UTF8Encoding(false));

        Assert.Equal("Name,Value", ReadLines("out.csv")[0]);
    }

    [Fact]
    public void WriteToFile_WritesDataRows()
    {
        var records = new[] { new ExportTestDto("Alice", 1), new ExportTestDto("Bob", 2) };
        _sut.WriteToFile(_tempDir, "out.csv", records, new UTF8Encoding(false));

        var lines = ReadLines("out.csv");
        Assert.Equal(3, lines.Length);
        Assert.Equal("Name,Value", lines[0]);
        Assert.Equal("Alice,1", lines[1]);
        Assert.Equal("Bob,2", lines[2]);
    }

    [Fact]
    public void WriteToFile_WritesHeaderOnly_WhenNoRecords()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            Array.Empty<ExportTestDto>(),
            new UTF8Encoding(false));

        var lines = ReadLines("out.csv");
        Assert.Single(lines);
        Assert.Equal("Name,Value", lines[0]);
    }

    // ── WriteToFile — delimiter ──────────────────────────────────────────────

    [Fact]
    public void WriteToFile_UsesCustomDelimiter()
    {
        var records = new[] { new ExportTestDto("Alice", 1) };
        _sut.WriteToFile(_tempDir, "out.tsv", records, new UTF8Encoding(false), delimiter: "\t");

        var lines = ReadLines("out.tsv");
        Assert.Equal("Name\tValue", lines[0]);
        Assert.Equal("Alice\t1", lines[1]);
    }

    [Fact]
    public void WriteToFile_UsesPipeDelimiter_WhenSpecified()
    {
        var records = new[] { new ExportTestDto("Alice", 1) };
        _sut.WriteToFile(_tempDir, "out.psv", records, new UTF8Encoding(false), delimiter: "|");

        var lines = ReadLines("out.psv");
        Assert.Equal("Name|Value", lines[0]);
        Assert.Equal("Alice|1", lines[1]);
    }

    // ── WriteToFile — encoding ───────────────────────────────────────────────

    [Fact]
    public void WriteToFile_WritesWithoutBom_WhenUtf8NoBomEncoding()
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _sut.WriteToFile(_tempDir, "out.csv",
            new[] { new ExportTestDto("Héllo", 1) }, utf8NoBom);

        var bytes = ReadBytes("out.csv");
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Contains("Héllo", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void WriteToFile_WritesBomPrefix_WhenUtf8WithBomEncoding()
    {
        var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        _sut.WriteToFile(_tempDir, "out.csv",
            new[] { new ExportTestDto("hello", 1) }, utf8WithBom);

        var bytes = ReadBytes("out.csv");
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    // ── WriteToFile — write header ──────────────────────────────────────────

    [Fact]
    public void WriteToFile_WritesHeaderRow_ByDefault()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            new[] { new ExportTestDto("Alice", 1) }, new UTF8Encoding(false));

        Assert.Equal("Name,Value", ReadLines("out.csv")[0]);
    }

    [Fact]
    public void WriteToFile_DoesNotWriteHeaderRow_WhenFlagFalse()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            new[] { new ExportTestDto("Alice", 1) }, new UTF8Encoding(false),
            writeHeader: false);

        var lines = ReadLines("out.csv");
        Assert.Single(lines);
        Assert.Equal("Alice,1", lines[0]);
    }

    [Fact]
    public void WriteToFile_WritesOnlyDataRows_WhenMultipleRecordsAndHeaderSuppressed()
    {
        var records = new[] { new ExportTestDto("Alice", 1), new ExportTestDto("Bob", 2) };
        _sut.WriteToFile(_tempDir, "out.csv", records, new UTF8Encoding(false),
            writeHeader: false);

        var lines = ReadLines("out.csv");
        Assert.Equal(2, lines.Length);
        Assert.Equal("Alice,1", lines[0]);
        Assert.Equal("Bob,2", lines[1]);
    }

    [Fact]
    public void WriteToFile_WritesEncodingLineButNoColumnHeader_WhenBothFlagsSet()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            new[] { new ExportTestDto("Alice", 1) }, new UTF8Encoding(false),
            writeHeader: false, writeEncodingHeader: true);

        var lines = ReadLines("out.csv");
        Assert.Equal(2, lines.Length);
        Assert.Equal("utf-8", lines[0]);
        Assert.Equal("Alice,1", lines[1]);
    }

    // ── WriteToFile — encoding header ───────────────────────────────────────

    [Fact]
    public void WriteToFile_DoesNotWriteEncodingHeader_ByDefault()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            new[] { new ExportTestDto("Alice", 1) }, new UTF8Encoding(false));

        var lines = ReadLines("out.csv");
        Assert.Equal("Name,Value", lines[0]);
    }

    [Fact]
    public void WriteToFile_WritesEncodingWebName_AsFirstLine_WhenFlagTrue()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            Array.Empty<ExportTestDto>(), new UTF8Encoding(false),
            writeEncodingHeader: true);

        Assert.Equal("utf-8", ReadLines("out.csv")[0]);
    }

    [Fact]
    public void WriteToFile_WritesEncodingHeaderOverride_AsFirstLine_WhenProvided()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            Array.Empty<ExportTestDto>(), new UTF8Encoding(false),
            writeEncodingHeader: true, encodingHeaderOverride: "windows-1252");

        Assert.Equal("windows-1252", ReadLines("out.csv")[0]);
    }

    [Fact]
    public void WriteToFile_WritesCsvHeaderOnSecondLine_WhenEncodingHeaderEnabled()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            new[] { new ExportTestDto("Alice", 1) }, new UTF8Encoding(false),
            writeEncodingHeader: true);

        var lines = ReadLines("out.csv");
        Assert.Equal("utf-8", lines[0]);
        Assert.Equal("Name,Value", lines[1]);
        Assert.Equal("Alice,1", lines[2]);
    }

    // ── ArchiveExistingFile — no-op ──────────────────────────────────────────

    [Fact]
    public void ArchiveExistingFile_DoesNothing_WhenFileDoesNotExist()
    {
        // Must not throw
        _sut.ArchiveExistingFile(_tempDir, "nonexistent.csv");
    }

    // ── ArchiveExistingFile — default archive path ───────────────────────────

    [Fact]
    public void ArchiveExistingFile_MovesFileToArchiveSubFolder_WhenArchivePathIsNull()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv");

        Assert.False(File.Exists(Path.Combine(_tempDir, "export.csv")));
        Assert.Single(Directory.GetFiles(Path.Combine(_tempDir, "archive"), "export_*.csv"));
    }

    [Fact]
    public void ArchiveExistingFile_CreatesArchiveDirectory_WhenItDoesNotExist()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv");

        Assert.True(Directory.Exists(Path.Combine(_tempDir, "archive")));
    }

    [Fact]
    public void ArchiveExistingFile_PreservesFileContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "original content");

        _sut.ArchiveExistingFile(_tempDir, "export.csv");

        var archived = Directory.GetFiles(Path.Combine(_tempDir, "archive"), "export_*.csv");
        Assert.Equal("original content", File.ReadAllText(archived[0]));
    }

    [Fact]
    public void ArchiveExistingFile_PreservesFileExtension()
    {
        File.WriteAllText(Path.Combine(_tempDir, "report.tsv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "report.tsv");

        Assert.Single(Directory.GetFiles(Path.Combine(_tempDir, "archive"), "report_*.tsv"));
    }

    [Fact]
    public void ArchiveExistingFile_ArchivedNameContainsTimestamp_InExpectedFormat()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");
        var before = DateTime.UtcNow;

        _sut.ArchiveExistingFile(_tempDir, "export.csv");

        var archived = Directory.GetFiles(Path.Combine(_tempDir, "archive"), "export_*.csv");
        var stem = Path.GetFileNameWithoutExtension(archived[0]);
        var timestampPart = stem["export_".Length..];
        Assert.True(DateTime.TryParseExact(
            timestampPart, "yyyyMMddHHmmssfff",
            null, System.Globalization.DateTimeStyles.None, out var parsed));
        Assert.True(parsed >= before.AddSeconds(-1));
    }

    // ── ArchiveExistingFile — absolute archive path ──────────────────────────

    [Fact]
    public void ArchiveExistingFile_UsesAbsoluteArchivePath_WhenProvided()
    {
        var absoluteArchive = Path.Combine(_tempDir, "custom_archive");
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv", absoluteArchive);

        Assert.Single(Directory.GetFiles(absoluteArchive, "export_*.csv"));
    }

    [Fact]
    public void ArchiveExistingFile_CreatesAbsoluteArchiveDirectory_WhenItDoesNotExist()
    {
        var absoluteArchive = Path.Combine(_tempDir, "custom_archive");
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv", absoluteArchive);

        Assert.True(Directory.Exists(absoluteArchive));
    }

    // ── ArchiveExistingFile — relative archive path ──────────────────────────

    [Fact]
    public void ArchiveExistingFile_ResolvesRelativeArchivePath_RelativeToBasePath()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv", "old");

        Assert.Single(Directory.GetFiles(Path.Combine(_tempDir, "old"), "export_*.csv"));
    }

    [Fact]
    public void ArchiveExistingFile_CreatesRelativeArchiveDirectory_WhenItDoesNotExist()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv", "old");

        Assert.True(Directory.Exists(Path.Combine(_tempDir, "old")));
    }

    // ── WriteToBlobAsync ─────────────────────────────────────────────────────

    private (Mock<BlobClient> blob, string captured) SetupWriteBlob()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath))
            .Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) =>
            {
                s.Position = 0;
                captured = new StreamReader(s).ReadToEnd();
            })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());
        return (blobMock, captured);
    }

    [Fact]
    public async Task WriteToBlobAsync_CallsUpload_WithOverwriteTrue()
    {
        var blobMock = new Mock<BlobClient>();
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock.Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            Array.Empty<ExportTestDto>(), new UTF8Encoding(false));

        blobMock.Verify(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteToBlobAsync_ContentContainsHeaderRow()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) => { s.Position = 0; captured = new StreamReader(s).ReadToEnd(); })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            Array.Empty<ExportTestDto>(), new UTF8Encoding(false));

        Assert.Contains("Name,Value", captured);
    }

    [Fact]
    public async Task WriteToBlobAsync_ContentContainsDataRows()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) => { s.Position = 0; captured = new StreamReader(s).ReadToEnd(); })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            new[] { new ExportTestDto("Alice", 1), new ExportTestDto("Bob", 2) }, new UTF8Encoding(false));

        Assert.Contains("Alice,1", captured);
        Assert.Contains("Bob,2", captured);
    }

    [Fact]
    public async Task WriteToBlobAsync_DoesNotWriteHeaderRow_WhenFlagFalse()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) => { s.Position = 0; captured = new StreamReader(s).ReadToEnd(); })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            new[] { new ExportTestDto("Alice", 1) }, new UTF8Encoding(false),
            writeHeader: false);

        var lines = captured.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Contains("Alice,1", lines[0]);
    }

    [Fact]
    public async Task WriteToBlobAsync_WritesEncodingLineButNoColumnHeader_WhenBothFlagsSet()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) => { s.Position = 0; captured = new StreamReader(s).ReadToEnd(); })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            new[] { new ExportTestDto("Alice", 1) }, new UTF8Encoding(false),
            writeHeader: false, writeEncodingHeader: true);

        var lines = captured.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("utf-8", lines[0].Trim());
        Assert.Contains("Alice,1", lines[1]);
    }

    [Fact]
    public async Task WriteToBlobAsync_DoesNotWriteEncodingHeader_ByDefault()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) => { s.Position = 0; captured = new StreamReader(s).ReadToEnd(); })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            new[] { new ExportTestDto("Alice", 1) }, new UTF8Encoding(false));

        Assert.StartsWith("Name,Value", captured.TrimStart('\r', '\n', '﻿'));
    }

    [Fact]
    public async Task WriteToBlobAsync_WritesEncodingWebName_AsFirstLine_WhenFlagTrue()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) => { s.Position = 0; captured = new StreamReader(s).ReadToEnd(); })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            Array.Empty<ExportTestDto>(), new UTF8Encoding(false),
            writeEncodingHeader: true);

        Assert.StartsWith("utf-8", captured);
    }

    [Fact]
    public async Task WriteToBlobAsync_WritesEncodingHeaderOverride_AsFirstLine_WhenProvided()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) => { s.Position = 0; captured = new StreamReader(s).ReadToEnd(); })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            Array.Empty<ExportTestDto>(), new UTF8Encoding(false),
            writeEncodingHeader: true, encodingHeaderOverride: "windows-1252");

        Assert.StartsWith("windows-1252", captured);
    }

    // ── ArchiveExistingBlobAsync ──────────────────────────────────────────────

    private (Mock<BlobClient> source, Mock<BlobClient> archive) SetupArchiveBlob(
        bool exists, string? customArchivePath = null)
    {
        var sourceMock = new Mock<BlobClient>();
        var archiveMock = new Mock<BlobClient>();

        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath))
            .Returns(sourceMock.Object);
        _factoryMock
            .Setup(f => f.GetBlobClient(ConnStr, Container, It.Is<string>(p => p != BlobPath)))
            .Returns(archiveMock.Object);

        sourceMock.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(exists, Mock.Of<Response>()));
        sourceMock.Setup(b => b.DownloadToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());
        sourceMock.Setup(b => b.DeleteAsync(
                It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());
        archiveMock.Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        return (sourceMock, archiveMock);
    }

    [Fact]
    public async Task ArchiveExistingBlobAsync_DoesNothing_WhenBlobDoesNotExist()
    {
        var (sourceMock, archiveMock) = SetupArchiveBlob(exists: false);

        await _sut.ArchiveExistingBlobAsync(ConnStr, Container, BlobPath);

        archiveMock.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        sourceMock.Verify(b => b.DeleteAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveExistingBlobAsync_UsesDefaultArchivePath_WhenArchivePathIsNull()
    {
        SetupArchiveBlob(exists: true);
        string? capturedPath = null;
        _factoryMock
            .Setup(f => f.GetBlobClient(ConnStr, Container, It.Is<string>(p => p != BlobPath)))
            .Callback<string, string, string>((_, _, p) => capturedPath = p)
            .Returns(new Mock<BlobClient>().Object);

        // Re-setup the archive mock return after callback override
        var archiveMock = new Mock<BlobClient>();
        archiveMock.Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());
        _factoryMock
            .Setup(f => f.GetBlobClient(ConnStr, Container, It.Is<string>(p => p != BlobPath)))
            .Callback<string, string, string>((_, _, p) => capturedPath = p)
            .Returns(archiveMock.Object);

        await _sut.ArchiveExistingBlobAsync(ConnStr, Container, BlobPath);

        Assert.NotNull(capturedPath);
        Assert.StartsWith("exports/archive/out_", capturedPath);
        Assert.EndsWith(".csv", capturedPath);
    }

    [Fact]
    public async Task ArchiveExistingBlobAsync_UsesArchivePrefix_WhenBlobIsAtRoot()
    {
        const string rootBlob = "out.csv";
        var rootBlobMock = new Mock<BlobClient>();
        var archiveMock = new Mock<BlobClient>();
        string? capturedPath = null;

        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, rootBlob)).Returns(rootBlobMock.Object);
        _factoryMock
            .Setup(f => f.GetBlobClient(ConnStr, Container, It.Is<string>(p => p != rootBlob)))
            .Callback<string, string, string>((_, _, p) => capturedPath = p)
            .Returns(archiveMock.Object);

        rootBlobMock.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
        rootBlobMock.Setup(b => b.DownloadToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());
        rootBlobMock.Setup(b => b.DeleteAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());
        archiveMock.Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.ArchiveExistingBlobAsync(ConnStr, Container, rootBlob);

        Assert.NotNull(capturedPath);
        Assert.StartsWith("archive/out_", capturedPath);
    }

    [Fact]
    public async Task ArchiveExistingBlobAsync_UsesCustomArchivePath_WhenProvided()
    {
        SetupArchiveBlob(exists: true);
        string? capturedPath = null;
        var archiveMock = new Mock<BlobClient>();
        archiveMock.Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());
        _factoryMock
            .Setup(f => f.GetBlobClient(ConnStr, Container, It.Is<string>(p => p != BlobPath)))
            .Callback<string, string, string>((_, _, p) => capturedPath = p)
            .Returns(archiveMock.Object);

        await _sut.ArchiveExistingBlobAsync(ConnStr, Container, BlobPath, archivePath: "old/exports");

        Assert.NotNull(capturedPath);
        Assert.StartsWith("old/exports/out_", capturedPath);
        Assert.EndsWith(".csv", capturedPath);
    }

    [Fact]
    public async Task ArchiveExistingBlobAsync_ArchivedNameContainsTimestamp_InExpectedFormat()
    {
        SetupArchiveBlob(exists: true);
        string? capturedPath = null;
        var archiveMock = new Mock<BlobClient>();
        archiveMock.Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());
        _factoryMock
            .Setup(f => f.GetBlobClient(ConnStr, Container, It.Is<string>(p => p != BlobPath)))
            .Callback<string, string, string>((_, _, p) => capturedPath = p)
            .Returns(archiveMock.Object);

        var before = DateTime.UtcNow;
        await _sut.ArchiveExistingBlobAsync(ConnStr, Container, BlobPath);

        var fileName = Path.GetFileNameWithoutExtension(capturedPath!);
        var timestampPart = fileName["out_".Length..];
        Assert.True(DateTime.TryParseExact(
            timestampPart, "yyyyMMddHHmmssfff",
            null, System.Globalization.DateTimeStyles.None, out var parsed));
        Assert.True(parsed >= before.AddSeconds(-1));
    }

    [Fact]
    public async Task ArchiveExistingBlobAsync_DeletesOriginalBlob_AfterCopy()
    {
        var (sourceMock, _) = SetupArchiveBlob(exists: true);

        await _sut.ArchiveExistingBlobAsync(ConnStr, Container, BlobPath);

        sourceMock.Verify(b => b.DeleteAsync(
            It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── WriteToFile — metadata header ──────────────────────────────────────────

    [Fact]
    public void WriteToFile_WritesMetadataLines_InKeyOrder()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            Array.Empty<ExportTestDto>(), new UTF8Encoding(false),
            metadataHeader: new Dictionary<int, string> { { 3, "Line3" }, { 1, "Line1" }, { 2, "Line2" } });

        var lines = ReadLines("out.csv");
        Assert.Equal("Line1", lines[0]);
        Assert.Equal("Line2", lines[1]);
        Assert.Equal("Line3", lines[2]);
        Assert.Equal("Name,Value", lines[3]);
    }

    [Fact]
    public void WriteToFile_IgnoresGaps_InMetadataKeys()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            Array.Empty<ExportTestDto>(), new UTF8Encoding(false),
            metadataHeader: new Dictionary<int, string> { { 1, "First" }, { 10, "Tenth" } });

        var lines = ReadLines("out.csv");
        Assert.Equal("First", lines[0]);
        Assert.Equal("Tenth", lines[1]);
        Assert.Equal("Name,Value", lines[2]);
    }

    [Fact]
    public void WriteToFile_WritesMetadataBeforeEncodingHeader()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            Array.Empty<ExportTestDto>(), new UTF8Encoding(false),
            metadataHeader: new Dictionary<int, string> { { 1, "MetaLine" } },
            writeEncodingHeader: true);

        var lines = ReadLines("out.csv");
        Assert.Equal("MetaLine", lines[0]);
        Assert.Equal("utf-8", lines[1]);
        Assert.Equal("Name,Value", lines[2]);
    }

    [Fact]
    public void WriteToFile_DoesNotWriteMetadata_WhenNull()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            Array.Empty<ExportTestDto>(), new UTF8Encoding(false));

        var lines = ReadLines("out.csv");
        Assert.Equal("Name,Value", lines[0]);
    }

    // ── WriteToBlobAsync — metadata header ──────────────────────────────────────

    [Fact]
    public async Task WriteToBlobAsync_WritesMetadataLines_InKeyOrder()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) => { s.Position = 0; captured = new StreamReader(s).ReadToEnd(); })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            new[] { new ExportTestDto("Alice", 1) }, new UTF8Encoding(false),
            metadataHeader: new Dictionary<int, string> { { 2, "B" }, { 1, "A" } });

        var lines = captured.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("A", lines[0].Trim());
        Assert.Equal("B", lines[1].Trim());
        Assert.Equal("Name,Value", lines[2].Trim());
        Assert.Contains("Alice,1", lines[3]);
    }

    [Fact]
    public async Task WriteToBlobAsync_WritesMetadataBeforeEncodingHeader()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) => { s.Position = 0; captured = new StreamReader(s).ReadToEnd(); })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            Array.Empty<ExportTestDto>(), new UTF8Encoding(false),
            metadataHeader: new Dictionary<int, string> { { 1, "MetaLine" } },
            writeEncodingHeader: true);

        var lines = captured.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("MetaLine", lines[0].Trim());
        Assert.Equal("utf-8", lines[1].Trim());
        Assert.Equal("Name,Value", lines[2].Trim());
    }

    // ── WriteToFile (CsvConfiguration overload) ──────────────────────────────

    [Fact]
    public void WriteToFile_CsvConfigOverload_WritesDataRows()
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture);
        var records = new[] { new ExportTestDto("Alice", 1) };

        _sut.WriteToFile(_tempDir, "out.csv", records, new UTF8Encoding(false), config);

        var lines = ReadLines("out.csv");
        Assert.Equal("Name,Value", lines[0]);
        Assert.Equal("Alice,1", lines[1]);
    }

    [Fact]
    public void WriteToFile_CsvConfigOverload_RespectsCustomDelimiter()
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "|" };
        var records = new[] { new ExportTestDto("Alice", 1) };

        _sut.WriteToFile(_tempDir, "out.psv", records, new UTF8Encoding(false), config);

        var lines = ReadLines("out.psv");
        Assert.Equal("Name|Value", lines[0]);
        Assert.Equal("Alice|1", lines[1]);
    }

    [Fact]
    public void WriteToFile_CsvConfigOverload_SuppressesHeader_WhenHasHeaderRecordFalse()
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
        var records = new[] { new ExportTestDto("Alice", 1) };

        _sut.WriteToFile(_tempDir, "out.csv", records, new UTF8Encoding(false), config);

        var lines = ReadLines("out.csv");
        Assert.Single(lines);
        Assert.Equal("Alice,1", lines[0]);
    }

    [Fact]
    public void WriteToFile_CsvConfigOverload_QuotesField_WhenShouldQuoteReturnsTrue()
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            ShouldQuote = _ => true
        };
        var records = new[] { new ExportTestDto("Alice", 1) };

        _sut.WriteToFile(_tempDir, "out.csv", records, new UTF8Encoding(false), config);

        var lines = ReadLines("out.csv");
        Assert.Contains("\"Alice\"", lines[1]);
        Assert.Contains("\"1\"", lines[1]);
    }

    [Fact]
    public void WriteToFile_CsvConfigOverload_WritesMetadataBeforeCsvContent()
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture);
        var records = new[] { new ExportTestDto("Alice", 1) };
        var metadata = new Dictionary<int, string> { { 1, "Source: ERP" } };

        _sut.WriteToFile(_tempDir, "out.csv", records, new UTF8Encoding(false), config,
            metadataHeader: metadata);

        var lines = ReadLines("out.csv");
        Assert.Equal("Source: ERP", lines[0]);
        Assert.Equal("Name,Value", lines[1]);
        Assert.Equal("Alice,1", lines[2]);
    }

    // ── WriteToBlobAsync (CsvConfiguration overload) ──────────────────────────

    [Fact]
    public async Task WriteToBlobAsync_CsvConfigOverload_WritesDataRows()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) => { s.Position = 0; captured = new StreamReader(s).ReadToEnd(); })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        var config = new CsvConfiguration(CultureInfo.InvariantCulture);
        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            new[] { new ExportTestDto("Alice", 1) }, new UTF8Encoding(false), config);

        Assert.Contains("Name,Value", captured);
        Assert.Contains("Alice,1", captured);
    }

    [Fact]
    public async Task WriteToBlobAsync_CsvConfigOverload_QuotesField_WhenShouldQuoteReturnsTrue()
    {
        var blobMock = new Mock<BlobClient>();
        var captured = "";
        _factoryMock.Setup(f => f.GetBlobClient(ConnStr, Container, BlobPath)).Returns(blobMock.Object);
        blobMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) => { s.Position = 0; captured = new StreamReader(s).ReadToEnd(); })
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { ShouldQuote = _ => true };
        await _sut.WriteToBlobAsync(ConnStr, Container, BlobPath,
            new[] { new ExportTestDto("Alice", 1) }, new UTF8Encoding(false), config);

        var lines = captured.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("\"Alice\"", lines[1]);
    }
}
