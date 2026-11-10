# Document Handling And Text Extraction

HPD Agent can carry document bytes as attachments, but document text extraction is not
built into `HPD-Agent.Framework` `0.9.5`. Applications that need extracted text in model
context use `HPD-Extract` or another extractor before submission, or add an
application-owned middleware that performs extraction before the model iteration.

Use HPD Agent content handling for attachment persistence and provider materialization.
Use HPD Extract for decoding the document.

## Packages

```bash
dotnet add package HPD-Agent.Framework --version 0.9.5
dotnet add package HPD-Extract --version 0.1.0
```

The current published HPD Extract package line is `0.1.0`; it is versioned independently
from HPD Agent.

## Extract Text Before A Run

```csharp
using HPD.Agent;
using HPD.Extract;
using Microsoft.Extensions.AI;

using var extractor = new TextExtractionUtility();
var extraction = await extractor.ExtractTextAsync("report.pdf");

if (!extraction.IsSuccess ||
    string.IsNullOrWhiteSpace(extraction.ExtractedText))
{
    throw new InvalidOperationException(extraction.ErrorMessage);
}

var document = await DocumentContent.FromFileAsync("report.pdf");

var agent = await new AgentBuilder()
    .WithChatClient(chatClient)
    .WithContentStore(new InMemoryContentStore())
    .BuildAsync();

await agent.RunAsync(new UserMessagesInputEvent
{
    Messages =
    [
        new ChatMessage(ChatRole.User,
        [
            new TextContent(
                $"Summarize this report.\n\n{extraction.ExtractedText}"),
            document
        ])
    ],
    SessionId = "document-session",
    ThreadId = "main"
});
```

The `TextContent` makes extracted text available to the model. `DocumentContent` preserves
the original bytes as an attachment. `WithContentStore(...)` enables local content
lifecycle persistence and provider-boundary materialization. If the selected provider can
consume the original document directly, keep the attachment. Otherwise, pass only the
extracted text.

## Use HPD Extract Directly

HPD Extract accepts files, byte arrays, and URLs. The result exposes:

- `IsSuccess`
- `ExtractedText`
- `FileName`
- `MimeType`
- `ProcessingTime`
- `ErrorMessage`
- richer `Extraction` sections, pages, text items, assets, diagnostics, and metadata

Registered decoder families include text, Markdown, JSON/XML, PDF, Open XML Word/Excel/
PowerPoint, HTML/web URLs, and images.

## OCR

Image decoding requires an injected OCR engine. HPD Extract does not bundle an OCR engine.
Register an application-owned implementation through the extractor's DI path:

```csharp
using HPD.Extract;

builder.Services.AddTextExtractionWithOcr<MyOcrEngine>();
```

Without an OCR engine, treat image extraction as producing no text. PDF OCR planning can
also use a configured OCR executor for scanned or low-quality pages.

## Boundaries

- Registered decoder families do not guarantee useful output for every file variant.
- HPD Agent `0.9.5` has no `WithDocumentHandling()` middleware or automatic
  `DocumentProcessedEvent` injection into model context.
- Validate the exact PDF, Office, HTML, and image fixtures used by the application.
