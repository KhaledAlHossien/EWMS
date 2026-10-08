namespace Application.DTOs.Request
{
    public class AddToDoItemRequestDto
    {
        public string Title { get; set; } = string.Empty;
    }

    /// <summary>تعديل بند: ما يُرسل فقط يتغيّر (العنوان و/أو حالة الإنجاز)</summary>
    public class UpdateToDoItemRequestDto
    {
        public string? Title { get; set; }
        public bool? IsDone { get; set; }
    }

    /// <summary>الترتيب الجديد: كل أرقام بنود القائمة بلا زيادة ولا نقص</summary>
    public class ReorderToDoItemsRequestDto
    {
        public List<int> ItemIds { get; set; } = [];
    }
}
