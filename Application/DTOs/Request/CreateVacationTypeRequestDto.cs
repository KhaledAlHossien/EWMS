using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Request
{
    public class CreateVacationTypeRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
