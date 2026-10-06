namespace Application.DTOs.Request
{
    // صاحب القائمة لا يُرسل: يُؤخذ من المستخدم الحالي عند الإنشاء ولا يتغير
    public class ToDoListRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
