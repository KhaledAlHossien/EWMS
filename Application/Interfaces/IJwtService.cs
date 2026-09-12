using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(User user);
        DateTime GetExpirationDate();
        int? ValidateToken(string token);
    }
}
