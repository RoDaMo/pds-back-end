using FluentValidation;
using pds_back_end.Models;

namespace pds_back_end.Controllers.Validations;

public class ChampionshipValidator : AbstractValidator<Championship>
{
    public ChampionshipValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .WithMessage("Campo Nome não pode ser vazio.");
         RuleFor(c => c.Name)
            .Length(4, 50)
            .WithMessage("Campo Nome deve ter entre 4 e 50 caracteres.");
        
        RuleFor(c => c.Prize)
            .NotEmpty()
            .WithMessage("Campo Prêmio não pode ser vazio.");
         RuleFor(c => c.Prize)
            .Length(3, 20)
            .WithMessage("Campo Prêmio deve ter entre 3 e 20 caracteres.");

        RuleFor(c => c.InitialDate)
            .NotEmpty()
            .WithMessage("Campo Data Inicial não pode ser vazio.");
        RuleFor(c => c.InitialDate)
            .GreaterThanOrEqualTo(c => DateTime.UtcNow.Date)
            .WithMessage("Campo Data Inicial não pode ser anterior a hoje.");
        RuleFor(c => c.InitialDate)
            .LessThanOrEqualTo(c => c.FinalDate)
            .WithMessage("Campo Data Inicial não pode ser posterior à data final.");

         RuleFor(c => c.FinalDate)
            .NotEmpty()
            .WithMessage("Campo Data Final não pode ser vazio.");
    }
}