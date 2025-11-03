using Optimizely.Opal.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

// Add the Opal Tools service
builder.Services.AddOpalToolService();

// Register individual tools
builder.Services.AddOpalTool<SeoCheckerTool>();

var app = builder.Build();

// Map the Opal Tools endpoints (creates /discovery and tool-specific endpoints)
app.MapOpalTools();

// Start the app
app.Run();

// Tool implementations
public class SeoCheckerTool
{
    [OpalTool(Name = "seochecker")]
    [Description("Checks a url for SEO statistics")]
    public object Check(string url)
    {
        
    }
}

