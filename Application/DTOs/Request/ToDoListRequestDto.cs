namespace Application.DTOs.Request
{
    // صاحب القائمة لا يُرسل: يُؤخذ من المستخدم الحالي عند الإنشاء ولا يتغير
    public class ToDoListRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>green | blue | purple | orange | red | gray، أو فارغ</summary>
        public string Color { get; set; } = string.Empty;
        /// <summary>رمز تعبيري واحد (حتى 8 أحرف)، أو فارغ</summary>
        public string Icon { get; set; } = string.Empty;
    }
}
