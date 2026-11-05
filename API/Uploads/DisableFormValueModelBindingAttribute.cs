using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace API.Uploads
{
    /// <summary>
    /// يمنع MVC من قراءة نموذج الطلب (multipart) لملء مزوّدي القيم قبل الـ action — بدونه يقرأ ASP.NET الجسم كله
    /// (ويخزّنه مؤقتاً بسقف 128MB) فلا يبقى شيء لـ MultipartUploadReader. يُستعمل مع الرفع المتدفق للملفات الكبيرة.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var factories = context.ValueProviderFactories;
            factories.RemoveType<FormValueProviderFactory>();
            factories.RemoveType<FormFileValueProviderFactory>();
            factories.RemoveType<JQueryFormValueProviderFactory>();
        }

        public void OnResourceExecuted(ResourceExecutedContext context) { }
    }
}
