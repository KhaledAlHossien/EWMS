using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.VacationTypes.Commands.Delete
{
    public class DeleteVacationTypeCommand : IRequest<Unit>
    {
        public int Id { get; set; }
        public DeleteVacationTypeCommand(int id) => Id = id;
    }
}
