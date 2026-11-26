using FluentValidation;

namespace Application.Features.ToDoLists.Commands.Create
{
    public class CreateToDoListCommandValidator : AbstractValidator<CreateToDoListCommand>
    {
        public CreateToDoListCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new ToDoListRequestDtoValidator());
    }
}
