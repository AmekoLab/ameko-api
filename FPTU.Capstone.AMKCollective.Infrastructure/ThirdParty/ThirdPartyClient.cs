using System;
using FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty;

namespace FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty
{
    // Simple placeholder for a third-party client (could wrap HttpClient)
    public class ThirdPartyClient : IThirdPartyClient
    {
        public string Ping()
        {
            // In a real implementation, call the external service here
            return "pong";
        }
    }
}
