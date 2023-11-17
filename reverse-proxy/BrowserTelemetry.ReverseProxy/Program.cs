using System.Text;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;


// To test this application, configure a front-end application to POST telemetry to /track.
var builder = WebApplication.CreateBuilder(args);

// Grab the reverse-proxy configuration from the configuration system.
// In our case, this is appsettings.json and appsettings.Development.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy")) 
    .AddTransforms(transformBuilder =>
    {
        if (transformBuilder.Route.RouteId == "trackRoute")
        {
            // This replaces the connection string placeholder with the real value that is stored in this secured service.
            transformBuilder.AddAppInsightsReplaceConnectionStringTransform(builder.Configuration.GetConnectionString("ApplicationInsights"));
        }
        // You can add other transforms here if you use YARP to route calls to other services.
    });

var app = builder.Build();
app.UseHttpsRedirection();
app.MapReverseProxy(); // This adds YARP to the request pipeline
app.Run();

public static class YarpTransformExtensions
{
    public static void AddAppInsightsReplaceConnectionStringTransform(this TransformBuilderContext context, string connectionString)
    {
        context.AddRequestTransform(async context =>
        {
            var httpContext = context.HttpContext;

            var requestBody = "";

            // Read the request body so we can modify it later
            using (var sr = new StreamReader(httpContext.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            {
                requestBody = await sr.ReadToEndAsync();
            }

            // This is where the magic happens; the placeholder is replaced with the real value
            var replacedContent = requestBody.Replace("TEMPINSTRUMENTATIONKEY", connectionString);

            // Set the new requestBody in the HTTP Request and recalculate the conten-length.
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(replacedContent));
            context.ProxyRequest.Content!.Headers.ContentLength = httpContext.Request.Body.Length;

            // Required for YARP
            context.ProxyRequest.Headers.Host = null;

            // Remove some private headers that should not be forwarded to Microsoft Servers.
            context.ProxyRequest.Headers.Remove("Authorization");
            context.ProxyRequest.Headers.Remove("Cookie");
        });
    }
}