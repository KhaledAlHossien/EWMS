using Application.Common;
using Application.DTOs.Request;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Features.Contracts
{
    /// <summary>
    /// أعضاء اللجان (الفنية والمالية للعقد، ولجنة الاستلام): التحقق المشترك من الأعضاء والتشكيل، وتحميل الموظفين بعد التحقق.
    /// </summary>
    public static class CommitteeInput
    {
        /// <summary>
        /// الأعضاء بعد التحقق: الموظفون نشطون ومن فرع المشروع، والرئيس واحد، والتاريخ معقول — وتُحمَّل أسماؤهم لعرضها.
        /// </summary>
        public static async Task<List<CommitteeMember>> ResolveMembersAsync(IUserService users, int branchId, CommitteeRequestDto dto)
        {
            var inputs = dto.Members.Select(m => new ContractRules.MemberInput(m.UserId, m.ExternalName, m.Capacity, m.IsChair)).ToList();
            if (ContractRules.ValidateMembers(inputs) is string members) throw new InvalidOperationException(members);
            if (ContractRules.ValidateCommittee(dto.DecisionNumber, dto.Recommendations, dto.FormedAt, DateTime.Today) is string committee) throw new InvalidOperationException(committee);

            var result = new List<CommitteeMember>();
            foreach (var m in dto.Members)
            {
                User? user = null;
                if (m.UserId is int id)
                {
                    user = await users.GetByIdAsync(id) ?? throw new KeyNotFoundException("موظف غير موجود");
                    if (!user.IsActive) throw new InvalidOperationException($"حساب «{user.FullName}» معطّل");
                    if (!OrganizationRole.InBranch(user, branchId)) throw new InvalidOperationException($"«{user.FullName}» من فرع آخر");
                }
                result.Add(new CommitteeMember
                {
                    UserId = user?.Id, User = user, ExternalName = user == null ? m.ExternalName!.Trim() : null, Capacity = m.Capacity.Trim(), IsChair = m.IsChair
                });
            }
            return result;
        }

        public static string Describe(IEnumerable<CommitteeMember> members) =>
            string.Join("، ", members.OrderByDescending(m => m.IsChair).Select(m => $"{(m.User?.FullName ?? m.ExternalName)} ({m.Capacity}{(m.IsChair ? "، رئيس" : string.Empty)})"));

        public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
