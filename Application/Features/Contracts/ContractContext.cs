using Application.Common;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Features.Schedules;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using static Application.Features.Contracts.ContractSupport;

namespace Application.Features.Contracts
{
    /// <summary>
    /// العقود والمقاولون
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class ContractContext(IContractService contracts, IProjectService projects, IDiwanService diwan,
        IUserService users, IUserPermissionService permissions, INotificationService notifications)
    {
        /// <summary>نافذة التنبيه بقرب انتهاء الضمان (أيام) في العرض؛ تنبيه الإشعارات في 4.3</summary>
        public const int WarrantyWindowDays = 30;

        public IContractService Contracts { get; } = contracts;
        public IProjectService Projects { get; } = projects;
        public IDiwanService Diwan { get; } = diwan;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;
        public INotificationService Notifications { get; } = notifications;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        /// <summary>العقد بكل ما تحته بعد التحقق من أن مشروعه ضمن حدّ المستخدم وأنه يملك صلاحية عقود</summary>
        public async Task<Contract> LoadAsync(Viewer viewer, int id) => Guard(viewer, await Contracts.GetByIdAsync(id));

        public async Task<(Contract Contract, ContractOffer Offer)> LoadOfferAsync(Viewer viewer, int offerId)
        {
            var contract = Guard(viewer, await Contracts.GetByOfferAsync(offerId));
            return (contract, contract.Offers.First(o => o.Id == offerId));
        }

        public async Task<(Contract Contract, Committee Committee)> LoadCommitteeAsync(Viewer viewer, int committeeId)
        {
            var contract = Guard(viewer, await Contracts.GetByCommitteeAsync(committeeId));
            return (contract, contract.Committees.First(c => c.Id == committeeId));
        }

        public async Task<(Contract Contract, RetentionEntry Entry)> LoadRetentionAsync(Viewer viewer, int entryId)
        {
            var contract = Guard(viewer, await Contracts.GetByRetentionEntryAsync(entryId));
            return (contract, contract.RetentionEntries.First(e => e.Id == entryId));
        }

        /// <summary>التقييم مع عقده؛ ومن لا يرى هذا التقييم بصلاحياته يعامَل كأنه غير موجود</summary>
        public async Task<(Contract Contract, ContractorEvaluation Evaluation)> LoadEvaluationAsync(Viewer viewer, int evaluationId)
        {
            var contract = Guard(viewer, await Contracts.GetByEvaluationAsync(evaluationId));
            var evaluation = contract.Evaluations.First(e => e.Id == evaluationId);
            if (!CanSeeEvaluation(viewer, evaluation)) throw new KeyNotFoundException("التقييم غير موجود");
            return (contract, evaluation);
        }

        private static Contract Guard(Viewer viewer, Contract? contract)
        {
            if (contract == null) throw new KeyNotFoundException("العقد غير موجود");
            ContractRules.EnsureCanView(viewer, contract.Project);
            return contract;
        }

        public async Task<ContractDetailDto> DetailAsync(Viewer viewer, int id)
        {
            var contract = await Contracts.GetByIdAsync(id) ?? throw new KeyNotFoundException("العقد غير موجود");
            return ToDetail(contract, viewer, DateTime.Today);
        }

        /// <summary>
        /// فرع العملية على المقاولين ومعايير التقييم: فرع المستخدم، ولنظرة المؤسسة أي فرع. من لا فرع له ولا نظرة مؤسسة لا يعمل فيها.
        /// </summary>
        public static int ResolveBranch(Viewer viewer, int? requested)
        {
            var branch = viewer.IsOrganizationWide ? requested ?? viewer.User.BranchId : viewer.User.BranchId;
            return branch ?? throw new InvalidOperationException(viewer.IsOrganizationWide ? "اختر الفرع" : "حسابك غير مرتبط بفرع");
        }

        /// <summary>يملك أحد صلاحيات إدارة المقاولين (فيرى المعطّلين منهم)</summary>
        public static bool CanManageContractors(Viewer viewer) =>
            viewer.IsSuperAdmin || viewer.Has(AppPermissions.CreateContractor) || viewer.Has(AppPermissions.EditContractor) || viewer.Has(AppPermissions.DeleteContractor);

        public static bool CanManageCriteria(Viewer viewer) =>
            viewer.IsSuperAdmin || viewer.Has(AppPermissions.CreateEvaluationCriterion) || viewer.Has(AppPermissions.EditEvaluationCriterion) || viewer.Has(AppPermissions.DeleteEvaluationCriterion);

        /// <summary>المعاملة الواردة المربوطة (أمر التنفيذ): من فرع المشروع ويراها المستخدم</summary>
        public async Task<Correspondence?> ResolveCorrespondenceAsync(Viewer viewer, int? id, int branchId)
        {
            if (id is not int correspondenceId) return null;
            var c = await Diwan.GetByIdAsync(correspondenceId) ?? throw new KeyNotFoundException("المعاملة غير موجودة");
            Application.Features.Diwan.DiwanRules.EnsureView(viewer, c);
            if (c.BranchId != branchId) throw new InvalidOperationException("المعاملة من فرع آخر غير فرع المشروع");
            if (c.Direction != CorrespondenceDirection.Incoming) throw new InvalidOperationException("يُربط أمر التنفيذ بمعاملة واردة");
            return c;
        }

        /// <summary>المتعاقد معه بعد التحقق: من فرع المشروع ونشط (يُسمح بالمعطَّل إن كان هو نفسه المحفوظ على العقد)</summary>
        public async Task<Contractor?> ResolveContractorAsync(int? id, int branchId, int? currentId = null)
        {
            if (id is not int contractorId) return null;
            var contractor = await Contracts.GetContractorAsync(contractorId) ?? throw new KeyNotFoundException("المقاول غير موجود");
            if (contractorId == currentId && contractor.BranchId == branchId) return contractor;
            ContractRules.EnsureContractorUsable(contractor, branchId);
            return contractor;
        }

        /// <summary>إصدار وثيقة مثبَّت: من إضبارة المشروع نفسه (وإلا خطأ)</summary>
        public async Task<ProjectDocumentVersion?> ResolveVersionAsync(int? versionId, int projectId)
        {
            if (versionId is not int id) return null;
            var version = await Contracts.GetVersionAsync(id) ?? throw new KeyNotFoundException("إصدار الوثيقة غير موجود");
            if (version.Document.ProjectId != projectId) throw new InvalidOperationException("الوثيقة من مشروع آخر");
            return version;
        }

        /// <summary>حاملو الصلاحية في فرع المشروع ممن يرونه بحدّه</summary>
        public Task<List<int>> HoldersAsync(Project project, string permission) =>
            ScheduleNotifier.HoldersAsync(Permissions, project, permission);

        /// <summary>إشعار لكل مستلم (بلا تكرار وبلا المنفِّذ) يفتح العقد من الواجهة</summary>
        public async Task NotifyAsync(IEnumerable<int> userIds, int exceptUserId, Contract contract, NotificationType type, string title, string message)
        {
            var now = DateTime.UtcNow;
            var rows = userIds.Distinct().Where(id => id != exceptUserId).Select(id => new Notification
            {
                UserId = id, Title = title, Message = message, Type = type,
                RelatedEntityType = AuditEntities.Contract, RelatedEntityId = contract.Id, CreatedAt = now
            }).ToList();
            if (rows.Count > 0) await Notifications.AddRangeAsync(rows);
        }

        public static string Label(Contract contract) =>
            $"{ContractRules.Number(contract.Id, contract.CreatedAt)}{(string.IsNullOrWhiteSpace(contract.Number) ? string.Empty : $" ({contract.Number})")}";
    }
}
