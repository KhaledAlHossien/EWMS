namespace Application.DTOs.Response
{
    public record TaskAttachmentFileDto(string FileName, string ContentType, Stream Content, bool Previewable);
}
