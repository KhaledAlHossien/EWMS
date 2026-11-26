using FluentValidation;

namespace Application.Features.ToDoLists.Commands.Update
{
    public class UpdateToDoListCommandValidator : AbstractValidator<UpdateToDoListCommand>
    {
        public UpdateToDoListCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new ToDoListRequestDtoValidator());
    }
}
