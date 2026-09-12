using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Request
{
    public class LoginRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }

}
