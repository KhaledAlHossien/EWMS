using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Commands.Cancel
{
    public class CancelVacationCommand : IRequest<Unit>
    {
        public int VacationId { get; set; }
        public CancelVacationCommand(int id) => VacationId = id;
    }
}
