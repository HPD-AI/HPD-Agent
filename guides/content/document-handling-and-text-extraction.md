# Document Handling And Text Extraction

Document handling lets an agent receive attached content and project extracted text into the model turn.

## Register Document Handling

```csharp
using HPD.Agent;
using HPD.Agent.Middleware.Document;
using Microsoft.Extensions.AI;

var agent = await new AgentBuilder()
    .WithChatClient(chatClient)
    .WithDocumentHandling()
    .BuildAsync();
```

Attach content with `DataContent`:

```csharp
byte[] pdfBytes = File.ReadAllBytes("report.pdf");

var message = new ChatMessage(ChatRole.User,
[
    new TextContent("Summarize this report."),
    new DataContent(pdfBytes, "application/pdf") { Name = "report.pdf" },
]);
```

## Registered Decoder Families

The default text extraction registration includes decoder families for:

| Content | Decoder family |
| --- | --- |
| Plain text | TXT and unknown-extension text fallback |
| Markdown | MD decoder |
| JSON/XML | structured text decoder |
| PDF | PDF decoder |
| Word | DOC/DOCX decoder |
| Excel | XLS/XLSX decoder |
| PowerPoint | PPT/PPTX decoder |
| Images | image decoder |
| Web/HTML | HTML and URL-like text decoder |

Image decoding requires an OCR engine for actual text extraction. No built-in OCR engine is bundled. Without an injected `IOcrEngine`, image extraction should be treated as no text found.

Applications that need image OCR should register their own OCR implementation through the text extraction service registration path, such as `AddTextExtractionWithOcr<T>()`.

## Boundaries

Document handling registration and `DataContent` attachment are straightforward. The table above describes registered decoder families, not a guarantee that every PDF, Office file, HTML page, or image variant extracts useful text. Validate edge-case extraction quality with fixtures that match your document types.
