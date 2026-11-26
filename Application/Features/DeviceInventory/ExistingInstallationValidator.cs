namespace Application.Features.DeviceInventory
{
    /// <summary>تعديل تركيب: كلمة السر الفارغة تُبقي الحالية</summary>
    public class ExistingInstallationValidator : InstallationRequestValidatorBase
    {
        public ExistingInstallationValidator() : base(creating: false) { }
    }
}
