using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Helpers
{
    public static class PaymentUrlHelper
    {
        public static string AttachQuery(string baseUrl, IDictionary<string, string> query)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) return "/";

            var separator = baseUrl.Contains('?') ? "&" : "?";
            var queryString = string.Join("&", query
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));

            return string.IsNullOrWhiteSpace(queryString) ? baseUrl : $"{baseUrl}{separator}{queryString}";
        }
    }
}
