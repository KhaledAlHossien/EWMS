using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(User user, DateTime? expiresAt = null);
        DateTime GetExpirationDate();
        int? ValidateToken(string token);
    }
}
