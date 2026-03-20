# Docfy AI - PDF to Markdown Converter with Vision Intelligence

[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0+-blue.svg)](https://dotnet.microsoft.com/)
[![SignalR](https://img.shields.io/badge/SignalR-Available-green.svg)]()

A smart PDF converter that transforms documents into well-formatted Markdown using AI-powered image analysis. Extract text, analyze charts, diagrams, code snippets, and UI elements with intelligent classification.

## 🚀 Features

- **PDF to Markdown Conversion**: Converts any PDF document into clean, readable Markdown
- **AI-Powered Image Analysis**: Uses LLM Vision (GPT-4 or compatible) to understand image content
- **Smart Content Detection**: Automatically identifies:
  - Text blocks and paragraphs
  - Charts, graphs, and diagrams
  - Code snippets with language detection
  - UI screenshots and interface elements
  - Decorative images (icons, logos, bullets)
  - Duplicate images
- **Real-time Progress Tracking**: SignalR-based live updates during processing
- **Parallel Processing**: Configurable concurrent image analysis for faster conversion
- **Duplicate Detection**: Smart deduplication using content hashing
- **Markdown Validation**: Built-in validation for proper formatting

## 📋 What It Does

Docfy reads PDF files and performs the following:

1. **Extracts all pages** with text positioning information
2. **Identifies images** on each page (charts, screenshots, code blocks)
3. **Analyzes each image** using AI to understand its content type
4. **Classifies content**:
   - `TEXT`: Extracts readable text from images
   - `CHART`: Describes graphs, tables, diagrams in natural language
   - `CODE`: Extracts code with syntax detection (Python, JavaScript, etc.)
   - `UI`: Captures interface screenshots with descriptions
   - `DECORATIVE`: Marks purely visual elements for exclusion
5. **Builds Markdown**: Reconstructs document flow with proper formatting
6. **Returns results** via SignalR with progress updates and final output

## 🏗️ Architecture

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Frontend  │────▶│  ConversionHub   │◀────│   SignalR Hub   │
│ (React/JS)  │     │  (Real-time UI)  │     │                 │
└─────────────┘     └──────────────────┘     └─────────────────┘
                            │
                            ▼
                    ┌──────────────────┐
                    │  ConversionCtrlr │
                    │   (API Endpoint) │
                    └──────────────────┘
                            │
         ┌──────────────────┼──────────────────┐
         ▼                  ▼                  ▼
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│PdfExtraction│    │  LLMVision  │    │ Markdown    │
│   Service   │    │   Service   │    │ Builder     │
└─────────────┘    └─────────────┘    └─────────────┘
```

## 🛠️ Tech Stack

| Layer | Technology |
|-------|------------|
| Backend | .NET 8.0 / ASP.NET Core |
| PDF Engine | iText7 (PDF parsing) |
| AI Vision | OpenAI GPT-4 Vision or compatible API |
| Real-time | SignalR (WebSocket connections) |
| Image Processing | SixLabors.ImageSharp |
| Frontend | Vanilla JavaScript + Marked.js + Prism.js |

## 📦 Installation & Setup

### Prerequisites

- .NET 8.0 SDK or later
- Node.js (for frontend, if running standalone)
- LLM API Key (OpenAI or compatible endpoint like Ollama/LM Studio)

### Clone the Repository

```bash
git clone https://github.com/yourusername/docfy.ai.git
cd Docfy.AI/src
```

### Configure Environment

Edit `appsettings.json`:

```json
{
  "LlmVision": {
    "Provider": "OpenAI",
    "ApiKey": "sk-your-api-key-here",
    "Model": "gpt-4-vision-preview",
    "Endpoint": "https://api.openai.com/v1/chat/completions",
    "MaxTokens": 16048,
    "MaxParallelImageProcessing": 5,
    "RequestTimeoutSeconds": 1800
  }
}
```

### Build and Run

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run in development mode
dotnet run --project Docfy/Docfy.csproj
```

The application will start at `https://localhost:60848` (or configured URL).

## 🎯 Usage

### Frontend Workflow

1. **Open the web interface** and drag & drop a PDF file or click to upload
2. **Watch real-time progress** as images are analyzed page by page
3. **View results**:
   - Markdown preview with syntax highlighting
   - Raw markdown text editor
   - Split view (side-by-side editor/preview)
4. **Explore analyzed images**: Filter by type, view descriptions and confidence scores

### API Endpoint

```http
POST /api/conversion/upload
Content-Type: multipart/form-data

file: <PDF_FILE>
connectionId: <SIGNALR_CONNECTION_ID>
```

**Response:**

```json
{
  "Success": true,
  "Message": "Arquivo recebido. Processamento iniciado.",
  "FileName": "document.pdf",
  "Size": 1234567,
  "ConnectionId": "abc-123-def"
}
```

### SignalR Events

| Event | Payload | Description |
|-------|---------|-------------|
| `ProgressUpdate` | `{ percentage: int, message: string }` | Overall progress (0-100%) |
| `PageProgress` | `{ pageNumber, status, imagesProcessed }` | Per-page processing status |
| `ImageProgress` | `{ imageIndex, contentType, isDuplicate }` | Individual image analysis |
| `ConversionCompleted` | `{ markdown, stats, images[] }` | Final result with all data |
| `ConversionError` | `{ error: string }` | Error notification |

## 📊 Content Classification Output

The AI analyzes each image and returns one of these types:

| Type | Description | Example |
|------|-------------|---------|
| `TEXT` | Extracted readable text | Legal clauses, paragraphs |
| `CHART` | Graph/diagram description | "Bar chart showing Q4 sales growth" |
| `CODE` | Syntax-recognized code | Python function with indentation |
| `UI` | Interface screenshots | Login form with validation messages |
| `DECORATIVE` | Visual-only elements | Company logo, bullet points |

## ⚙️ Configuration Options

### Parallel Processing

Control concurrent image analysis:

```json
"LlmVision": {
  "MaxParallelImageProcessing": 5
}
```

Valid range: **1-10** (default: 2)

Higher values = faster processing but more API calls in parallel.

### Image Scaling & Retry Logic

Large images are automatically scaled down for analysis:

- Initial scale: **100%** of original size
- First retry: **90%** if too large
- Subsequent retries: **-10%** each time (minimum 10%)
- Maximum retry attempts: **10** per image

### Timeout Settings

```json
"LlmVision": {
  "RequestTimeoutSeconds": 1800
}
```

Default: **30 minutes** per request. Adjust based on your API provider limits.

## 🗂️ Project Structure

```
src/
├── Docfy/
│   ├── Controllers/
│   │   └── ConversionController.cs    # Main API endpoint
│   ├── Hubs/
│   │   └── ConversionHub.cs           # SignalR real-time hub
│   ├── Services/
│   │   ├── PdfExtractionService.cs    # PDF parsing logic
│   │   ├── LlmVisionService.cs        # AI image analysis
│   │   └── MarkdownBuilderService.cs  # Output generation
│   ├── Models/                        # Data models & DTOs
│   ├── wwwroot/                       # Frontend assets
│   │   └── js/app.js                  # Client-side logic
│   └── appsettings.json              # Configuration file
├── Docfy.sln                         # Solution file
└── README.md                         # This file
```

## 🔄 Processing Flow

1. **Upload**: User uploads PDF via HTTP POST with SignalR connection ID
2. **Background Task**: Controller spawns background task for non-blocking response
3. **PDF Parsing**: Extracts all pages + images with positioning data
4. **Parallel Analysis**: Images processed concurrently (semaphore-controlled)
5. **Duplicate Check**: Hash-based deduplication before AI analysis
6. **AI Classification**: Each image sent to LLM Vision API
7. **Markdown Assembly**: Text and analyzed content assembled in document order
8. **Validation**: Markdown syntax checked for proper formatting
9. **SignalR Updates**: Progress events pushed to frontend in real-time

## 🎨 Frontend Features

- Drag & drop file upload with validation
- Live progress bar with percentage updates
- Per-page processing indicators
- Image gallery with filtering (processed/duplicate/decorative)
- Markdown preview with syntax highlighting (Prism.js)
- Raw text editor for manual editing
- Split view mode for side-by-side comparison
- Download as `.md` file
- Copy to clipboard functionality

## 🌐 Supported LLM Providers

While configured for OpenAI, the system works with any compatible endpoint:

| Provider | Endpoint Example |
|----------|------------------|
| OpenAI | `https://api.openai.com/v1/chat/completions` |
| Ollama (local) | `http://localhost:11434/api/generate` |
| LM Studio | `http://127.0.0.1:1234/v1/chat/completions` |
| Azure OpenAI | Custom endpoint with API key header |

## 📈 Performance Tips

- **Reduce parallel processing** if hitting rate limits (lower `MaxParallelImageProcessing`)
- **Increase timeout** for large documents (>50MB PDFs)
- **Cache processed images**: Duplicate detection avoids redundant AI calls
- **Use local LLM** for offline batch processing (Ollama/LM Studio)

## 🐛 Troubleshooting

### Common Issues

| Problem | Solution |
|---------|----------|
| "API too large" error | Enable automatic image scaling in `LlmVisionService.cs` |
| Slow conversion | Reduce parallel threads or use local LLM |
| Missing text extraction | Check PDF font encoding (use embedded fonts) |
| SignalR disconnects | Increase message size limit in Program.cs |

### Debug Mode

Enable detailed logging:

```json
"Logging": {
  "LogLevel": {
    "Default": "Debug",
    "Microsoft.AspNetCore.SignalR": "Information"
  }
}
```

## 📝 License

MIT License - See [LICENSE](LICENSE) file for details.

## 🔗 Links

- **API Documentation**: `/api/conversion/upload` endpoint (Swagger available at `/swagger`)
- **Health Check**: `GET /api/conversion/health` returns system status
- **Source Code**: GitHub repository root directory

## 👥 Contributors

Feel free to submit issues and pull requests!

---

**Made with ❤️ using .NET 8, SignalR, and AI Vision Intelligence**