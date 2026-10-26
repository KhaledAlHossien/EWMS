using System;
using System.Collections.Generic;
using System.Text;
using Domain.Entities.Attendance;

namespace Domain.Entities
{
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>منطقة الدوام التي يتبع لها الفرع (تُحدَّد من صفحة المناطق) — أجهزتها تحمل موظفيه</summary>
        public int? AttendanceAreaId { get; set; }
        public AttendanceArea? AttendanceArea { get; set; }
    }
}
