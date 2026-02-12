using System;
using Microsoft.AspNetCore.Mvc;
using VAIS_Producer.Models;

namespace VAIS_Producer.Controllers
{
    [Route("provider-states")]
    [ApiController]
    public class ProviderStatesController : ControllerBase
    {
        [HttpPost]
        public IActionResult SetProviderState([FromBody] ProviderStateRequest? request)
        {
            Console.WriteLine($"[ProviderStates] Received request: Consumer={request?.Consumer}, State={request?.State}");

            if (request == null || string.IsNullOrEmpty(request.State))
            {
                Console.WriteLine("[ProviderStates] No state provided - returning empty response");
                return Ok(new { });
            }

            // Handle different provider states
            switch (request.State)
            {
                case "Invalid authentication token provided":
                    Console.WriteLine("[ProviderStates] Setting up: Invalid authentication token");
                    // No setup needed - controller will handle invalid token
                    break;

                case "Valid Windows users exist in VAIS":
                    Console.WriteLine("[ProviderStates] Setting up: Valid Windows users exist");
                    // No setup needed - controller returns valid response for valid users
                    break;

                case "Windows user does not exist in domain":
                    Console.WriteLine("[ProviderStates] Setting up: Invalid Windows user");
                    // No setup needed - controller will detect invalid-user and return error
                    break;

                default:
                    Console.WriteLine($"[ProviderStates] Unknown state: {request.State}");
                    break;
            }

            return Ok(new { });
        }
    }
}
