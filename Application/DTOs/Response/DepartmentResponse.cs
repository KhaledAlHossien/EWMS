using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Response
{
    public class DepartmentResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
    }
}
