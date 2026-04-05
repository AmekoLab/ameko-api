using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using System.Net;
using System.Text.Json;

namespace FPTU.Capstone.AMKCollective.Api.Middlewares;

public class GlobalExceptionMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            Console.WriteLine("GlobalExceptionMiddleware");
            Console.WriteLine(ex);

            context.Response.ContentType = "application/json";
            var statusCode = HttpStatusCode.InternalServerError;
            var message = "Internal server error occurred.";

            // Handle specific exception types
            switch (ex)
            {
                case UnauthorizedAccessException:
                    statusCode = HttpStatusCode.Forbidden;
                    message = ex.Message;
                    break;
                case KeyNotFoundException:
                    statusCode = HttpStatusCode.NotFound;
                    message = ex.Message;
                    break;
                case InvalidOperationException:
                    statusCode = HttpStatusCode.BadRequest;
                    message = ex.Message;
                    break;
            }

            context.Response.StatusCode = (int)statusCode;

            var response = ApiResponse<object>.ErrorResponse(message);
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            await context.Response.WriteAsync(json);
        }
    }
}