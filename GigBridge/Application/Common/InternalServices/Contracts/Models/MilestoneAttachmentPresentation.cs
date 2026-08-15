namespace Application.Common.InternalServices.Contracts.Models;
/// <summary>
/// Maps a MilestoneAttachment's file name/MIME type to display-friendly labels and an icon
/// glyph for the submission email, so the renderer doesn't need to know about storage details.
/// </summary>
public static class MilestoneAttachmentPresentation
{
    public static string TypeLabel(string fileName, string? mimeType)
    {
        var extension = GetExtension(fileName);
        return extension switch
        {
            "pdf" => "PDF",
            "doc" or "docx" => "Word Document",
            "xls" or "xlsx" => "Excel Spreadsheet",
            "ppt" or "pptx" => "PowerPoint Presentation",
            "zip" or "rar" or "7z" => "Archive",
            "png" or "jpg" or "jpeg" or "gif" or "webp" or "svg" => "Image",
            "mp4" or "mov" or "avi" or "webm" => "Video",
            "txt" or "md" => "Text File",
            _ => string.IsNullOrWhiteSpace(extension)
                ? (mimeType ?? "File")
                : extension.ToUpperInvariant()
        };
    }

    public static string IconGlyph(string fileName)
    {
        var extension = GetExtension(fileName);
        return extension switch
        {
            "pdf" => "📄",
            "doc" or "docx" => "📝",
            "xls" or "xlsx" => "📊",
            "ppt" or "pptx" => "📽️",
            "zip" or "rar" or "7z" => "🗜️",
            "png" or "jpg" or "jpeg" or "gif" or "webp" or "svg" => "🖼️",
            "mp4" or "mov" or "avi" or "webm" => "🎬",
            _ => "📎"
        };
    }

    public static string? SizeLabel(long? bytes)
    {
        if (bytes is null or <= 0)
        {
            return null;
        }

        double size = bytes.Value;
        string[] units = ["B", "KB", "MB", "GB"];
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.#} {units[unitIndex]}";
    }

    private static string GetExtension(string fileName)
    {
        var dotIndex = fileName.LastIndexOf('.');
        return dotIndex < 0 || dotIndex == fileName.Length - 1
            ? string.Empty
            : fileName[(dotIndex + 1)..].ToLowerInvariant();
    }
}
