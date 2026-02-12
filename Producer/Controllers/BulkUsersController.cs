using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using VAIS_Producer.Models;

namespace VAIS_Producer.Controllers
{
    [Route("BulkUsers")]
    [ApiController]
    public class BulkUsersController : ControllerBase
    {
        [HttpPost]
        public IActionResult PostBulkUsers(
            [FromHeader(Name = "Authorization")] string? authorization,
            [FromBody] List<User>? users)
        {
            Console.WriteLine($"[BulkUsersController] Received Authorization: {authorization}");
            Console.WriteLine($"[BulkUsersController] Received {users?.Count ?? 0} users");

            // Check authorization header
            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            {
                Console.WriteLine("[BulkUsersController] Missing or invalid Authorization header");
                return Unauthorized();
            }

            var token = authorization.Substring("Bearer ".Length).Trim();
            Console.WriteLine($"[BulkUsersController] Token: {token}");

            // Invalid token scenario
            if (token == "invalid-token")
            {
                Console.WriteLine("[BulkUsersController] Invalid token detected - returning 401");
                return Unauthorized();
            }

            // Valid token required for other scenarios
            if (token != "valid-token-from-SF")
            {
                Console.WriteLine("[BulkUsersController] Unexpected token - returning 401");
                return Unauthorized();
            }

            // Empty body is valid
            if (users == null || users.Count == 0)
            {
                Console.WriteLine("[BulkUsersController] Empty user list - returning empty array");
                return Ok(new List<User>());
            }

            // Check for invalid Windows user scenario
            var invalidUser = users.FirstOrDefault(u =>
                u.IdentityProviders.Any(ip =>
                    ip.Provider == "windows" && ip.ProviderId == "invalid-user"));

            if (invalidUser != null)
            {
                Console.WriteLine("[BulkUsersController] Invalid Windows user detected - returning 400");
                return BadRequest(new ErrorResponse
                {
                    Error = "InvalidWindowsUserName",
                    Message = "Invalid UserName. User 'invalid-user' does not exist in Windows."
                });
            }

            // Valid user scenario - return users with subjects
            Console.WriteLine("[BulkUsersController] Valid users - returning with subjects");
            var responseUsers = users.Select(u => new User
            {
                DisplayName = u.DisplayName,
                IdentityProviders = u.IdentityProviders,
                IsAccountDisabled = u.IsAccountDisabled,
                Subject = u.Subject ?? "user-subject-id-123" // Assign subject if null
            }).ToList();

            return Ok(responseUsers);
        }
    }
}
