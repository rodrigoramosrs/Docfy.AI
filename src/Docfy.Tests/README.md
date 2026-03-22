# Docfy.Tests

Este diretório contém os testes unitários para o projeto Docfy.Core.

## Estrutura

```
Docfy.Tests/
├── Models/
│   └── ModelTests.cs          # Testes para todos os modelos
├── Services/
│   ├── PdfExtractionServiceTests.cs      # Testes para PdfExtractionService
│   ├── LlmVisionServiceTests.cs          # Testes para LlmVisionService
│   └── MarkdownBuilderServiceTests.cs    # Testes para MarkdownBuilderService
└── README.md
```

## Testes Implementados

### ModelTests
Teste todas as classes de modelo do Docfy.Core:
- ConversionRequest
- ConversionProgress
- ExtractedPage
- ExtractedImage
- TextChunk
- ImageAnalysisResult
- PageProgressEvent
- ImageProgressEvent

### PdfExtractionServiceTests
Teste a extração de PDF:
- Extração de texto
- Extração de imagens
- Detecção de formato de imagem (PNG, JPEG, GIF, BMP, WebP, TIFF)
- Cálculo de hash SHA256 das imagens
- Filtragem de imagens pequenas (< 20x20 pixels)
- Tratamento de múltiplas páginas
- Extração de text chunks

### LlmVisionServiceTests
Teste a análise de imagens via LLM:
- Análise de imagens com diferentes formatos (JPEG, PNG, GIF, WebP, BMP)
- Identificação de imagens decorativas
- Identificação de imagens com texto
- Identificação de gráficos, diagramas e código
- Processamento de imagens grandes
- Cálculo de hash de imagens
- Incluindo base64 das imagens

### MarkdownBuilderServiceTests
Teste a construção de Markdown:
- Processamento de hierarquia de títulos
- Formatação de blocos de código
- Inserção de imagens com diferentes tipos de conteúdo
- Tratamento de imagens decorativas e duplicadas
- Separação entre páginas
- Validação do Markdown gerado
- Detecção de blocos de código não fechados
- Pós-processamento do Markdown

## Como Executar os Testes

### Usando Visual Studio
1. Abra o arquivo `Docfy.sln`
2. Selecione "Test" > "Run All Tests"
3. Ou clique no botão de testes na barra de ferramentas

### Usando linha de comando (dotnet test)
```bash
dotnet test Docfy.Tests
```

### Usando Xunit
Os testes foram escritos usando o framework **xUnit**, que é o padrão recomendado para .NET Core.

## Executando testes específicos
```bash
# Executar todos os testes
dotnet test

# Executar testes de um projeto específico
dotnet test Docfy.Tests

# Executar testes com cobertura de código
dotnet test Docfy.Tests --collect:"XPlat Code Coverage"
```

## Próximos Passos

- Adicionar testes de integração com PDFs reais
- Implementar testes de performance
- Adicionar testes de simulação de APIs externas
- Implementar testes de UI se aplicável

## Dependências

- xunit
- Microsoft.Extensions.Logging
- iText 8
- SixLabors.ImageSharp
