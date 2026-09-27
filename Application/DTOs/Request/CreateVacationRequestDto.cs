using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Request
{
    public class CreateVacationRequestDto
    {
        public int VacationTypeId { get; set; }
        public string VacReason { get; set; } = string.Empty;
        public DateTime StartVac { get; set; }
        public DateTime EndVac { get; set; }
    }
}
