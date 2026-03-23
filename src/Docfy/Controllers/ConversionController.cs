using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Docfy.DTOs;
using Docfy.Hubs;
using Docfy.Core.Models;
using Docfy.Core.Services;
using System.Collections.Concurrent;

namespace Docfy.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversionController : ControllerBase
{
    private readonly IHubContext<ConversionHub> _hubContext;
    private readonly IDocumentExtractionFactory _extractionFactory;
    private readonly ILlmVisionService _llmService;
    private readonly MarkdownBuilderService _markdownBuilder;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ConversionController> _logger;
    private readonly int _maxParallelProcessing;

    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx"
    };

    public ConversionController(
        IHubContext<ConversionHub> hubContext,
        IDocumentExtractionFactory extractionFactory,
        ILlmVisionService llmService,
        MarkdownBuilderService markdownBuilder,
        ISettingsService settingsService,
        ILogger<ConversionController> logger,
        IConfiguration configuration)
    {
        _hubContext = hubContext;
        _extractionFactory = extractionFactory;
        _llmService = llmService;
        _markdownBuilder = markdownBuilder;
        _settingsService = settingsService;
        _logger = logger;
        _maxParallelProcessing = configuration.GetValue<int>("LlmVision:MaxParallelImageProcessing", 2);

        if (_maxParallelProcessing < 1) _maxParallelProcessing = 2;
        if (_maxParallelProcessing > 10) _maxParallelProcessing = 10;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadPdf([FromForm] IFormFile file, [FromForm] string connectionId)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { Error = "Nenhum arquivo enviado" });

        var extension = Path.GetExtension(file.FileName);
        if (!SupportedFormats.Contains(extension))
            return BadRequest(new { Error = $"Formato não suportado. Formatos aceitos: {string.Join(", ", SupportedFormats)}" });

        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var pdfBytes = ms.ToArray();

            _logger.LogInformation("Arquivo {FileName} recebido ({Size} bytes) do cliente {ConnectionId}",
                file.FileName, pdfBytes.Length, connectionId);

            _ = Task.Run(async () =>
            {
                try
                {
                    await ConvertPdfWithProgress(connectionId, file.FileName, pdfBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro na conversão em background");
                    await _hubContext.Clients.Client(connectionId).SendAsync("ConversionError", new
                    {
                        Success = false,
                        Error = ex.Message,
                        Details = ex.InnerException?.Message
                    });
                }
            });

            return Ok(new
            {
                Success = true,
                Message = "Arquivo recebido. Processamento iniciado.",
                FileName = file.FileName,
                Size = pdfBytes.Length,
                ConnectionId = connectionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no upload do arquivo");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    private async Task ConvertPdfWithProgress1(string connectionId, string fileName, byte[] pdfBytes)
    {
        var client = _hubContext.Clients.Client(connectionId);

        await SendProgress(client, 10, "Extraindo conteúdo do documento...");

        var extractor = _extractionFactory.GetExtractor(fileName);
        var extractedPages = await extractor.ExtractContentAsync(pdfBytes);
        var totalPages = extractedPages.Count;

        await SendProgress(client, 20, $"PDF analisado: {totalPages} páginas encontradas");

        // Estruturas thread-safe para controle
        var analyzedImages = new ConcurrentBag<ImageAnalysisResult>();
        var processedImageHashes = new ConcurrentDictionary<string, byte>();
        var pageImageCounts = new ConcurrentDictionary<int, int>(); // Página -> Contador de processadas
        var totalImageCount = extractedPages.Sum(p => p.Images.Count);
        var overallProcessedCount = 0;

        if (!extractedPages.SelectMany(p => p.Images).Any())
        {
            _logger.LogInformation("Nenhuma imagem encontrada no PDF");
            await BuildAndSendResult(client, fileName, extractedPages, new List<ImageAnalysisResult>(), totalPages);
            return;
        }

        using var semaphore = new SemaphoreSlim(_maxParallelProcessing);
        var progressLock = new object();
        var lastReportedProgress = 20;

        // Agrupar imagens por página para processamento organizado
        var imagesByPage = extractedPages
            .SelectMany(p => p.Images, (page, img) => new { Page = page, Image = img })
            .GroupBy(x => x.Page.PageNumber)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var pageGroup in imagesByPage)
        {
            var pageNum = pageGroup.Key;
            var pageImages = pageGroup.ToList();
            var imagesInThisPage = pageImages.Count;

            // Notificar início da página
            await client.SendAsync("PageProgress", new PageProgressEvent
            {
                PageNumber = pageNum,
                TotalPages = totalPages,
                Status = "Started",
                TotalImagesInPage = imagesInThisPage,
                ProcessedImagesInPage = 0,
                Message = $"Iniciando processamento da página {pageNum}"
            });

            _logger.LogInformation("Página {Page}: {Count} imagens para processar", pageNum, imagesInThisPage);

            // Criar tasks para todas as imagens desta página
            var pageTasks = pageImages.Select(async (item, idx) =>
            {
                await semaphore.WaitAsync();

                try
                {
                    var image = item.Image;
                    var imageHash = Convert.ToHexString(image.Hash);

                    // Notificar início do processamento da imagem
                    await client.SendAsync("ImageProgress", new ImageProgressEvent
                    {
                        PageNumber = pageNum,
                        ImageIndex = image.ImageIndex,
                        TotalImagesInPage = imagesInThisPage,
                        Status = "Started",
                        Message = $"Analisando imagem {image.ImageIndex + 1} da página {pageNum}..."
                    });

                    // Verificar duplicata
                    if (processedImageHashes.ContainsKey(imageHash))
                    {
                        var existingAnalysis = analyzedImages
                            .FirstOrDefault(a => Convert.ToHexString(a.ImageHash) == imageHash);

                        var duplicateResult = new ImageAnalysisResult
                        {
                            PageNumber = pageNum,
                            ImageIndex = image.ImageIndex,
                            ImageBase64 = existingAnalysis?.ImageBase64 ?? Convert.ToBase64String(image.Data),
                            MimeType = existingAnalysis?.MimeType ?? image.MimeType,
                            Width = existingAnalysis?.Width ?? (int)image.Width,
                            Height = existingAnalysis?.Height ?? (int)image.Height,
                            Size = existingAnalysis?.Size ?? image.Data.Length,
                            Description = existingAnalysis?.Description ?? "[Duplicata]",
                            IsDecorative = existingAnalysis?.IsDecorative ?? false,
                            IsDuplicate = true,
                            ContentType = existingAnalysis?.ContentType ?? "Duplicate",
                            CodeLanguage = existingAnalysis?.CodeLanguage,
                            Confidence = existingAnalysis?.Confidence ?? 0,
                            ImageHash = image.Hash
                        };

                        analyzedImages.Add(duplicateResult);

                        // Atualizar contador da página
                        var processedInPage = pageImageCounts.AddOrUpdate(pageNum, 1, (key, old) => old + 1);
                        var currentOverall = Interlocked.Increment(ref overallProcessedCount);

                        // Notificar conclusão da imagem (duplicata)
                        await client.SendAsync("ImageProgress", new ImageProgressEvent
                        {
                            PageNumber = pageNum,
                            ImageIndex = image.ImageIndex,
                            TotalImagesInPage = imagesInThisPage,
                            Status = "Completed",
                            IsDuplicate = true,
                            ContentType = duplicateResult.ContentType,
                            Message = $"Imagem {image.ImageIndex + 1} da página {pageNum} é duplicata",
                            Confidence = duplicateResult.Confidence
                        });

                        // Atualizar progresso da página
                        await client.SendAsync("PageProgress", new PageProgressEvent
                        {
                            PageNumber = pageNum,
                            TotalPages = totalPages,
                            Status = "Processing",
                            TotalImagesInPage = imagesInThisPage,
                            ProcessedImagesInPage = processedInPage,
                            Message = $"Página {pageNum}: {processedInPage}/{imagesInThisPage} imagens processadas"
                        });

                        // Progresso geral
                        UpdateOverallProgress(client, currentOverall, totalImageCount, ref lastReportedProgress, progressLock);

                        return;
                    }

                    // Processar imagem na LLM
                    var analysis = await _llmService.AnalyzeImageAsync(image);
                    analysis.PageNumber = pageNum; // Garantir que está correta
                    analyzedImages.Add(analysis);

                    // Marcar como processada se não for decorativa
                    if (!analysis.IsDecorative && !analysis.IsDuplicate)
                    {
                        processedImageHashes.TryAdd(imageHash, 0);
                    }

                    // Atualizar contadores
                    var processedInPageAfter = pageImageCounts.AddOrUpdate(pageNum, 1, (key, old) => old + 1);
                    var currentOverallAfter = Interlocked.Increment(ref overallProcessedCount);

                    // Notificar conclusão da imagem
                    await client.SendAsync("ImageProgress", new ImageProgressEvent
                    {
                        PageNumber = pageNum,
                        ImageIndex = image.ImageIndex,
                        TotalImagesInPage = imagesInThisPage,
                        Status = analysis.ContentType == "Error" ? "Error" : "Completed",
                        IsDecorative = analysis.IsDecorative,
                        IsDuplicate = analysis.IsDuplicate,
                        ContentType = analysis.ContentType,
                        Message = $"Imagem {image.ImageIndex + 1} da página {pageNum} concluída ({analysis.ContentType})",
                        Confidence = analysis.Confidence
                    });

                    // Atualizar progresso da página
                    await client.SendAsync("PageProgress", new PageProgressEvent
                    {
                        PageNumber = pageNum,
                        TotalPages = totalPages,
                        Status = "Processing",
                        TotalImagesInPage = imagesInThisPage,
                        ProcessedImagesInPage = processedInPageAfter,
                        Message = $"Página {pageNum}: {processedInPageAfter}/{imagesInThisPage} imagens processadas"
                    });

                    // Progresso geral
                    UpdateOverallProgress(client, currentOverallAfter, totalImageCount, ref lastReportedProgress, progressLock);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar imagem {Index} da página {Page}",
                        item.Image.ImageIndex, pageNum);

                    // Notificar erro da imagem
                    await client.SendAsync("ImageProgress", new ImageProgressEvent
                    {
                        PageNumber = pageNum,
                        ImageIndex = item.Image.ImageIndex,
                        TotalImagesInPage = imagesInThisPage,
                        Status = "Error",
                        Message = $"Erro na imagem {item.Image.ImageIndex + 1} da página {pageNum}: {ex.Message}"
                    });

                    // Adicionar resultado de erro
                    analyzedImages.Add(new ImageAnalysisResult
                    {
                        PageNumber = pageNum,
                        ImageIndex = item.Image.ImageIndex,
                        ImageHash = item.Image.Hash,
                        Description = $"[Erro: {ex.Message}]",
                        ContentType = "Error",
                        IsDecorative = false,
                        Confidence = 0
                    });

                    // Atualizar contadores mesmo em erro
                    var processedInPageError = pageImageCounts.AddOrUpdate(pageNum, 1, (key, old) => old + 1);
                    var currentOverallError = Interlocked.Increment(ref overallProcessedCount);

                    UpdateOverallProgress(client, currentOverallError, totalImageCount, ref lastReportedProgress, progressLock);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            // Aguardar todas as imagens da página completarem
            await Task.WhenAll(pageTasks);

            // Notificar conclusão da página
            var finalPageCount = pageImageCounts.TryGetValue(pageNum, out var count) ? count : 0;
            await client.SendAsync("PageProgress", new PageProgressEvent
            {
                PageNumber = pageNum,
                TotalPages = totalPages,
                Status = "Completed",
                TotalImagesInPage = imagesInThisPage,
                ProcessedImagesInPage = finalPageCount,
                Message = $"Página {pageNum} concluída ({finalPageCount} imagens processadas)"
            });
        }

        // Ordenar resultados
        var orderedResults = analyzedImages
            .OrderBy(a => a.PageNumber)
            .ThenBy(a => a.ImageIndex)
            .ToList();

        _logger.LogInformation("Processamento concluído. {Count} imagens analisadas", orderedResults.Count);

        await BuildAndSendResult(client, fileName, extractedPages, orderedResults, totalPages);
    }

    private void UpdateOverallProgress(IClientProxy client, int currentProcessed, int total, ref int lastReported, object lockObj)
    {
        var progress = 20 + (int)((double)currentProcessed / total * 50);

        lock (lockObj)
        {
            if (progress > lastReported || currentProcessed == total)
            {
                lastReported = progress;
                _ = SendProgress(client, progress,
                    $"Processando imagens... {currentProcessed}/{total} ({_maxParallelProcessing} paralelos)");
            }
        }
    }

    private async Task ConvertPdfWithProgress(string connectionId, string fileName, byte[] pdfBytes)
    {
        var client = _hubContext.Clients.Client(connectionId);

        await SendProgress(client, 5, "Extraindo conteúdo do documento...");

        var extractor = _extractionFactory.GetExtractor(fileName);
        var extractedPages = await extractor.ExtractContentAsync(pdfBytes);
        var totalPages = extractedPages.Count;

        if (totalPages == 0)
        {
            await BuildAndSendResult(client, fileName, extractedPages, new List<ImageAnalysisResult>(), 0);
            return;
        }

        await SendProgress(client, 10, $"PDF analisado: {totalPages} páginas encontradas");

        // Estruturas thread-safe globais
        var analyzedImages = new ConcurrentBag<ImageAnalysisResult>();
        var processedImageHashes = new ConcurrentDictionary<string, byte>();

        // SEMAFORO GLOBAL para controlar threads simultâneas entre TODAS as páginas
        using var globalSemaphore = new SemaphoreSlim(_maxParallelProcessing);

        // Controle de progresso usando AtomicInteger (evita ref em async)
        var totalImagesGlobal = extractedPages.Sum(p => p.Images?.Count ?? 0);
        var processedImagesGlobal = 0;
        var lastReportedProgress = 10;

        // Usar ConcurrentDictionary para compartilhar estado mutável entre threads
        var progressState = new ConcurrentDictionary<string, object>
        {
            ["lastReported"] = 10,
            ["lock"] = new object()
        };

        // Lista para armazenar as tasks de cada página
        var pageTasks = new List<Task>();

        // Iniciar processamento de TODAS as páginas simultaneamente (limitado pelo semáforo global)
        foreach (var currentPage in extractedPages.OrderBy(p => p.PageNumber))
        {
            var page = currentPage; // Captura para closure

            // Criar task para esta página específica
            var pageTask = ProcessPageAsync(
                client,
                page,
                totalPages,
                globalSemaphore,
                analyzedImages,
                processedImageHashes,
                () => Interlocked.Increment(ref processedImagesGlobal),
                totalImagesGlobal,
                progressState
            );

            pageTasks.Add(pageTask);
        }

        // Aguardar TODAS as páginas terminarem
        await Task.WhenAll(pageTasks);

        // Finalizar e enviar resultado
        var orderedResults = analyzedImages
            .OrderBy(a => a.PageNumber)
            .ThenBy(a => a.ImageIndex)
            .ToList();

        _logger.LogInformation("Processamento concluído. {Count} imagens analisadas em {Pages} páginas",
            orderedResults.Count, totalPages);

        await BuildAndSendResult(client, fileName, extractedPages, orderedResults, totalPages);
    }

    // Método auxiliar para processar uma página individual
    private async Task ProcessPageAsync(
        IClientProxy client,
        ExtractedPage page,
        int totalPages,
        SemaphoreSlim globalSemaphore,
        ConcurrentBag<ImageAnalysisResult> analyzedImages,
        ConcurrentDictionary<string, byte> processedImageHashes,
        Func<int> incrementGlobalCounter,
        int totalImagesGlobal,
        ConcurrentDictionary<string, object> progressState)
    {
        var pageNum = page.PageNumber;
        var pageImages = page.Images?.ToList() ?? new List<ExtractedImage>();
        var imagesInThisPage = pageImages.Count;

        // 1. NOTIFICAR INÍCIO DA PÁGINA
        await client.SendAsync("PageProgress", new PageProgressEvent
        {
            PageNumber = pageNum,
            TotalPages = totalPages,
            Status = "Started",
            TotalImagesInPage = imagesInThisPage,
            ProcessedImagesInPage = 0,
            Message = imagesInThisPage > 0
                ? $"Iniciando página {pageNum} ({imagesInThisPage} imagens)"
                : $"Processando página {pageNum} (sem imagens)"
        });

        if (imagesInThisPage == 0)
        {
            await client.SendAsync("PageProgress", new PageProgressEvent
            {
                PageNumber = pageNum,
                TotalPages = totalPages,
                Status = "Completed",
                TotalImagesInPage = 0,
                ProcessedImagesInPage = 0,
                Message = $"Página {pageNum} concluída (sem imagens)"
            });
            return;
        }

        var imageTasks = new List<Task>();
        var pageProcessedCount = 0;

        foreach (var image in pageImages)
        {
            var currentImage = image;

            var task = Task.Run(async () =>
            {
                await globalSemaphore.WaitAsync();

                try
                {
                    var imageIndex = currentImage.ImageIndex;
                    var imageHash = Convert.ToHexString(currentImage.Hash);

                    await client.SendAsync("ImageProgress", new ImageProgressEvent
                    {
                        PageNumber = pageNum,
                        ImageIndex = imageIndex,
                        TotalImagesInPage = imagesInThisPage,
                        Status = "Processing",
                        Message = $"Analisando imagem {imageIndex + 1} da página {pageNum}..."
                    });

                    ImageAnalysisResult result;

                    if (processedImageHashes.ContainsKey(imageHash))
                    {
                        var existing = analyzedImages.FirstOrDefault(a =>
                            Convert.ToHexString(a.ImageHash) == imageHash);

                        result = new ImageAnalysisResult
                        {
                            PageNumber = pageNum,
                            ImageIndex = imageIndex,
                            ImageBase64 = existing?.ImageBase64 ?? Convert.ToBase64String(currentImage.Data),
                            MimeType = existing?.MimeType ?? currentImage.MimeType,
                            Width = existing?.Width ?? (int)currentImage.Width,
                            Height = existing?.Height ?? (int)currentImage.Height,
                            Size = existing?.Size ?? currentImage.Data.Length,
                            Description = existing?.Description ?? "[Duplicata]",
                            IsDecorative = existing?.IsDecorative ?? false,
                            IsDuplicate = true,
                            ContentType = existing?.ContentType ?? "Duplicate",
                            Confidence = existing?.Confidence ?? 0,
                            ImageHash = currentImage.Hash
                        };

                        analyzedImages.Add(result);
                    }
                    else
                    {
                        result = await _llmService.AnalyzeImageAsync(currentImage);
                        result.PageNumber = pageNum;
                        result.ImageIndex = imageIndex;
                        analyzedImages.Add(result);

                        if (!result.IsDecorative)
                        {
                            processedImageHashes.TryAdd(imageHash, 0);
                        }
                    }

                    await client.SendAsync("ImageProgress", new ImageProgressEvent
                    {
                        PageNumber = pageNum,
                        ImageIndex = imageIndex,
                        TotalImagesInPage = imagesInThisPage,
                        Status = result.ContentType == "Error" ? "Error" : "Completed",
                        IsDecorative = result.IsDecorative,
                        IsDuplicate = result.IsDuplicate,
                        ContentType = result.ContentType,
                        Message = result.IsDuplicate
                            ? $"Imagem {imageIndex + 1} da página {pageNum} é duplicata"
                            : $"Imagem {imageIndex + 1} da página {pageNum} concluída ({result.ContentType})",
                        Confidence = result.Confidence
                    });

                    var currentPageProcessed = Interlocked.Increment(ref pageProcessedCount);
                    var currentGlobalProcessed = incrementGlobalCounter();

                    await client.SendAsync("PageProgress", new PageProgressEvent
                    {
                        PageNumber = pageNum,
                        TotalPages = totalPages,
                        Status = "Processing",
                        TotalImagesInPage = imagesInThisPage,
                        ProcessedImagesInPage = currentPageProcessed,
                        Message = $"Página {pageNum}: {currentPageProcessed}/{imagesInThisPage} imagens processadas"
                    });

                    UpdateOverallProgress(client, currentGlobalProcessed, totalImagesGlobal, progressState);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar imagem {ImageIndex} da página {PageNum}",
                        currentImage.ImageIndex, pageNum);

                    await client.SendAsync("ImageProgress", new ImageProgressEvent
                    {
                        PageNumber = pageNum,
                        ImageIndex = currentImage.ImageIndex,
                        TotalImagesInPage = imagesInThisPage,
                        Status = "Error",
                        Message = $"Erro na imagem {currentImage.ImageIndex + 1} da página {pageNum}: {ex.Message}"
                    });

                    analyzedImages.Add(new ImageAnalysisResult
                    {
                        PageNumber = pageNum,
                        ImageIndex = currentImage.ImageIndex,
                        ImageHash = currentImage.Hash,
                        Description = $"[Erro: {ex.Message}]",
                        ContentType = "Error",
                        IsDecorative = false,
                        Confidence = 0
                    });

                    var currentPageProcessed = Interlocked.Increment(ref pageProcessedCount);
                    var currentGlobalProcessed = incrementGlobalCounter();

                    await client.SendAsync("PageProgress", new PageProgressEvent
                    {
                        PageNumber = pageNum,
                        TotalPages = totalPages,
                        Status = "Processing",
                        TotalImagesInPage = imagesInThisPage,
                        ProcessedImagesInPage = currentPageProcessed,
                        Message = $"Página {pageNum}: {currentPageProcessed}/{imagesInThisPage} imagens (com erro)"
                    });

                    UpdateOverallProgress(client, currentGlobalProcessed, totalImagesGlobal, progressState);
                }
                finally
                {
                    globalSemaphore.Release();
                }
            });

            imageTasks.Add(task);
        }

        await Task.WhenAll(imageTasks);

        await client.SendAsync("PageProgress", new PageProgressEvent
        {
            PageNumber = pageNum,
            TotalPages = totalPages,
            Status = "Completed",
            TotalImagesInPage = imagesInThisPage,
            ProcessedImagesInPage = imagesInThisPage,
            Message = $"Página {pageNum} concluída ({imagesInThisPage} imagens processadas)"
        });
    }

    private void UpdateOverallProgress(IClientProxy client, int processed, int total,
        ConcurrentDictionary<string, object> state)
    {
        if (total == 0) return;

        var percentage = (int)((processed / (double)total) * 80) + 10;
        var lockObj = (object)state["lock"];
        var lastReported = (int)state["lastReported"];

        lock (lockObj)
        {
            if (percentage > lastReported && percentage <= 90)
            {
                state["lastReported"] = percentage;
                client.SendAsync("ProgressUpdate", new ConversionProgress
                {
                    Percentage = percentage,
                    Message = $"Processando imagens... ({processed}/{total})"
                }).ConfigureAwait(false);
            }
        }
    }
    // Mantenha este método auxiliar se não existir
    //private void UpdateOverallProgress(IClientProxy client, int processed, int total,
    //    ref int lastReported, object lockObj)
    //{
    //    if (total == 0) return;

    //    var percentage = (int)((processed / (double)total) * 80) + 10; // 10-90% reservado para processamento

    //    lock (lockObj)
    //    {
    //        if (percentage > lastReported && percentage <= 90)
    //        {
    //            lastReported = percentage;
    //            client.SendAsync("ProgressUpdate", new ProgressEvent
    //            {
    //                Percentage = percentage,
    //                Message = $"Processando imagens... ({processed}/{total})"
    //            }).ConfigureAwait(false);
    //        }
    //    }
    //}


    private async Task BuildAndSendResult(
        IClientProxy client,
        string fileName,
        List<ExtractedPage> extractedPages,
        List<ImageAnalysisResult> analyzedImages,
        int totalPages)
    {
        await SendProgress(client, 70, "Imagens processadas. Construindo documento...");
        await client.SendAsync("PageProgress", new PageProgressEvent
        {
            PageNumber = 0,
            TotalPages = totalPages,
            Status = "Completed",
            Message = "Todas as páginas processadas. Gerando Markdown..."
        });

        var finalMarkdown = _markdownBuilder.BuildMarkdown(extractedPages, analyzedImages);

        var settings = _settingsService.GetSettings();
        if (settings.ProcessMarkdownWithLlm)
        {
            await SendProgress(client, 80, "Formatando markdown com LLM...");
            _logger.LogInformation("Processando markdown final com LLM");
            
            try
            {
                finalMarkdown = await _settingsService.ProcessMarkdownWithLlmAsync(finalMarkdown);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao processar markdown com LLM, usando versão original");
            }
        }

        await SendProgress(client, 90, "Finalizando formatação...");

        var validation = _markdownBuilder.ValidateMarkdown(finalMarkdown);

        await SendProgress(client, 100, "Conversão concluída!");

        await client.SendAsync("ConversionCompleted", new ConversionResponse
        {
            Success = true,
            FileName = fileName.Replace(".pdf", ".md", StringComparison.OrdinalIgnoreCase),
            Markdown = finalMarkdown,
            Stats = new ConversionStats
            {
                TotalPages = totalPages,
                TotalImages = analyzedImages.Count,
                ProcessedImages = analyzedImages.Count(a => !a.IsDecorative),
                Validation = validation
            },
            Images = analyzedImages.Select(img => new ImageInfo
            {
                PageNumber = img.PageNumber,
                ImageIndex = img.ImageIndex,
                Base64 = img.ImageBase64,
                MimeType = img.MimeType,
                Width = img.Width,
                Height = img.Height,
                Size = img.Size,
                ContentType = img.ContentType,
                Description = img.Description,
                IsDecorative = img.IsDecorative,
                IsDuplicate = img.IsDuplicate,
                CodeLanguage = img.CodeLanguage,
                Confidence = img.Confidence,
                ProcessedScale = img.ProcessedScale
            }).ToList()
        });
    }

    private async Task SendProgress(IClientProxy client, int percentage, string message)
    {
        await client.SendAsync("ProgressUpdate", new ConversionProgress
        {
            Percentage = percentage,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            MaxParallelProcessing = _maxParallelProcessing
        });
    }
}
