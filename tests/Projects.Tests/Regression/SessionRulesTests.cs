using Application.Common;

namespace Projects.Tests.Regression;

/// <summary>تجديد الجلسة (قرار 2026-10-28): يتجدد التوكن ما دامت الجلسة دون حدّها الأقصى، ولا يتجاوز التوكن الجديد ذلك الحد</summary>
public class SessionRulesTests
{
    private static readonly DateTime Start = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Renewal_is_allowed_until_the_session_reaches_its_maximum_age()
    {
        Assert.True(SessionRules.CanRenew(Start.AddHours(11).AddMinutes(59), Start, 12));
        Assert.False(SessionRules.CanRenew(Start.AddHours(12), Start, 12));
        Assert.False(SessionRules.CanRenew(Start.AddHours(30), Start, 12));
    }

    [Fact]
    public void A_renewed_token_lives_the_normal_duration_but_never_past_the_session_end()
    {
        Assert.Equal(Start.AddHours(3), SessionRules.NextExpiry(Start.AddHours(1), 120, Start, 12));
        // قرب النهاية: يُقصّ عند ساعة 12 لا عند +120 دقيقة
        Assert.Equal(Start.AddHours(12), SessionRules.NextExpiry(Start.AddHours(11), 120, Start, 12));
    }

    [Fact]
    public void The_old_token_dies_two_minutes_after_renewal_or_earlier_if_it_was_already_ending()
    {
        var now = Start.AddHours(2);
        Assert.Equal(now.AddSeconds(SessionRules.OldTokenGraceSeconds), SessionRules.OldTokenExpiry(now, now.AddMinutes(90)));
        Assert.Equal(now.AddSeconds(30), SessionRules.OldTokenExpiry(now, now.AddSeconds(30)));
    }
}
