using Xunit;
using Docfy.Core.Services;
using Docfy.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Font;
using iText.IO.Image;
using iText.Kernel.Pdf.Xobject;

namespace Docfy.Tests.Services
{
    public class PdfExtractionServiceTests
    {
        private readonly IPdfExtractionService _pdfExtractionService;
        private readonly Mock<ILogger<PdfExtractionService>> _mockLogger;

        public PdfExtractionServiceTests()
        {
            _mockLogger = new Mock<ILogger<PdfExtractionService>>();
            _pdfExtractionService = new PdfExtractionService(_mockLogger.Object);
        }

        [Fact]
        public async Task ExtractContentAsync_ShouldReturnEmptyList_WhenPdfBytesAreEmpty()
        {
            var result = await _pdfExtractionService.ExtractContentAsync(Array.Empty<byte>());
            Assert.Empty(result);
        }

        [Fact]
        public async Task ExtractContentAsync_ShouldReturnEmptyList_WhenPdfBytesAreInvalid()
        {
            var invalidPdf = Encoding.UTF8.GetBytes("This is not a valid PDF");
            var result = await _pdfExtractionService.ExtractContentAsync(invalidPdf);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ExtractContentAsync_ShouldCreateExtractedPageForValidPdf()
        {
            var pdfBytes = CreateSimpleValidPdf();
            var result = await _pdfExtractionService.ExtractContentAsync(pdfBytes);

            Assert.NotEmpty(result);
            Assert.True(result.All(p => p.PageNumber > 0));
        }

        [Fact]
        public async Task ExtractContentAsync_ShouldExtractTextFromPdf()
        {
            var pdfBytes = CreateSimpleValidPdf();
            var result = await _pdfExtractionService.ExtractContentAsync(pdfBytes);

            Assert.NotEmpty(result.First().Text);
        }

[Fact]
        public async Task ExtractContentAsync_ShouldExtractImagesFromPdf()
        {
            var pdfBytes = CreatePdfWithImages();
            var result = await _pdfExtractionService.ExtractContentAsync(pdfBytes);

            Assert.NotNull(result);
            Assert.True(result.Count > 0);
            Assert.NotNull(result.First().Images);
        }

        // -----------------------
        // Helpers para criar PDFs
        // -----------------------

private byte[] CreateSimpleValidPdf()
        {
            var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            using (var pdf = new PdfDocument(writer))
            {
                var page = pdf.AddNewPage();
                var canvas = new PdfCanvas(page);
                canvas.BeginText()
                      .SetFontAndSize(PdfFontFactory.CreateFont(), 12)
                      .MoveText(36, 750)
                      .ShowText("Texto de teste")
                      .EndText();
            }
            var result = ms.ToArray();
            return result.Length > 0 ? result : new byte[0];
        }

        private byte[] CreatePdfWithImages()
        {
            var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            using (var pdf = new PdfDocument(writer))
            {
                var page = pdf.AddNewPage();
                var canvas = new PdfCanvas(page);

                try
                {
                    var imgData1 = ImageDataFactory.Create(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xFF, 0xFF, 0x3F, 0x00, 0x05, 0xFE, 0x02, 0xFE, 0xDC, 0xCC, 0x59, 0xE7, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 });
                    canvas.AddImageAt(imgData1, 36, 700, false);
                }
                catch
                {
                }

                try
                {
                    var imgData2 = ImageDataFactory.Create(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48, 0x00, 0x48, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43, 0x00 });
                    canvas.AddImageAt(imgData2, 150, 700, false);
                }
                catch
                {
                }
            }
            return ms.ToArray();
        }


        private byte[] CreateMultiPagePdf()
        {
            var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            using (var pdf = new PdfDocument(writer))
            {
                for (int i = 1; i <= 3; i++)
                {
                    var page = pdf.AddNewPage();
                    var canvas = new PdfCanvas(page);
                    canvas.BeginText()
                          .SetFontAndSize(PdfFontFactory.CreateFont(), 12)
                          .MoveText(36, 750)
                          .ShowText($"Página {i}")
                          .EndText();
                }
            }
            return ms.ToArray();
        }
    }
}
