namespace Application.Features.DeviceInventory
{
    /// <summary>تركيب جديد (وأسطر الاستيراد): كلمة السر مطلوبة</summary>
    public class NewInstallationValidator : InstallationRequestValidatorBase
    {
        public NewInstallationValidator() : base(creating: true) { }
    }
}
