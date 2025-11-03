using Optimizely.Opal.Tools;
using System.ComponentModel;
using System.Text.RegularExpressions;

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

public class SeoCheckerParameters
{
    public string Url { get; set; }
}

// Tool implementations
public class SeoCheckerTool
{
    private static readonly HttpClient _httpClient = new();

    [OpalTool(Name = "seochecker")]
    [Description("Checks a url for SEO statistics")]
    public async Task<object> Check(SeoCheckerParameters parameters)
    {
        try
        {
            // Validate URL
            if (!Uri.TryCreate(parameters.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return new { error = "Invalid URL format. Please provide a valid HTTP or HTTPS URL." };
            }

            // Fetch the webpage content
            var response = await _httpClient.GetAsync(parameters.Url);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();

            // Analyze SEO metrics
            var seoMetrics = new
            {
                url = parameters.Url,
                statusCode = (int)response.StatusCode,
                title = ExtractTitle(html),
                metaDescription = ExtractMetaDescription(html),
                headings = AnalyzeHeadings(html),
                images = AnalyzeImages(html),
                links = AnalyzeLinks(html, uri),
                contentLength = html.Length,
                wordCount = CountWords(html),
                openGraphTags = ExtractOpenGraphTags(html),
                structuredData = CheckStructuredData(html),
                recommendations = GenerateRecommendations(html)
            };

            return seoMetrics;
        }
        catch (HttpRequestException ex)
        {
            return new { error = $"Failed to fetch URL: {ex.Message}" };
        }
        catch (Exception ex)
        {
            return new { error = $"An error occurred: {ex.Message}" };
        }
    }

    private static string ExtractTitle(string html)
    {
        var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var title = match.Success ? match.Groups[1].Value.Trim() : "No title found";

        return title;
    }

    private static string ExtractMetaDescription(string html)
    {
        var match = Regex.Match(html, @"<meta\s+name=[""']description[""']\s+content=[""']([^""']*)[""']",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "No meta description found";
    }

    private static object AnalyzeHeadings(string html)
    {
        var h1Count = Regex.Matches(html, @"<h1[^>]*>", RegexOptions.IgnoreCase).Count;
        var h2Count = Regex.Matches(html, @"<h2[^>]*>", RegexOptions.IgnoreCase).Count;
        var h3Count = Regex.Matches(html, @"<h3[^>]*>", RegexOptions.IgnoreCase).Count;

        var h1Tags = Regex.Matches(html, @"<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();

        return new
        {
            h1Count,
            h2Count,
            h3Count,
            h1Tags = h1Tags.Take(5).ToList() // Return first 5 H1 tags
        };
    }

    private static object AnalyzeImages(string html)
    {
        var imgMatches = Regex.Matches(html, @"<img[^>]*>", RegexOptions.IgnoreCase);
        var totalImages = imgMatches.Count;
        var imagesWithoutAlt = 0;

        foreach (Match match in imgMatches)
        {
            if (!Regex.IsMatch(match.Value, @"alt\s*=\s*[""'][^""']+[""']", RegexOptions.IgnoreCase))
            {
                imagesWithoutAlt++;
            }
        }

        return new
        {
            totalImages,
            imagesWithAlt = totalImages - imagesWithoutAlt,
            imagesWithoutAlt
        };
    }

    private static object AnalyzeLinks(string html, Uri baseUri)
    {
        var linkMatches = Regex.Matches(html, @"<a[^>]+href\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        var internalLinks = 0;
        var externalLinks = 0;

        foreach (Match match in linkMatches)
        {
            var href = match.Groups[1].Value;
            if (Uri.TryCreate(baseUri, href, out var linkUri))
            {
                if (linkUri.Host == baseUri.Host)
                    internalLinks++;
                else
                    externalLinks++;
            }
        }

        return new
        {
            totalLinks = linkMatches.Count,
            internalLinks,
            externalLinks
        };
    }

    private static int CountWords(string html)
    {
        // Remove script and style tags
        var cleanHtml = Regex.Replace(html, @"<script[^>]*>.*?</script>", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        cleanHtml = Regex.Replace(cleanHtml, @"<style[^>]*>.*?</style>", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Remove all HTML tags
        var textOnly = Regex.Replace(cleanHtml, @"<[^>]+>", " ");

        // Count words
        var words = Regex.Matches(textOnly, @"\b\w+\b");
        return words.Count;
    }

    private static object ExtractOpenGraphTags(string html)
    {
        var ogTitle = Regex.Match(html, @"<meta\s+property=[""']og:title[""']\s+content=[""']([^""']*)[""']",
            RegexOptions.IgnoreCase);
        var ogDescription = Regex.Match(html,
            @"<meta\s+property=[""']og:description[""']\s+content=[""']([^""']*)[""']",
            RegexOptions.IgnoreCase);
        var ogImage = Regex.Match(html, @"<meta\s+property=[""']og:image[""']\s+content=[""']([^""']*)[""']",
            RegexOptions.IgnoreCase);

        return new
        {
            hasOgTitle = ogTitle.Success,
            hasOgDescription = ogDescription.Success,
            hasOgImage = ogImage.Success
        };
    }

    private static bool CheckStructuredData(string html)
    {
        return html.Contains("application/ld+json", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("itemtype=", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> GenerateRecommendations(string html)
    {
        var recommendations = new List<string>();

        var title = ExtractTitle(html);
        if (title == "No title found")
            recommendations.Add("Add a <title> tag to your page");
        else if (title.Length > 60)
            recommendations.Add("Title is too long (>60 characters). Consider shortening it.");
        else if (title.Length < 30)
            recommendations.Add("Title is too short (<30 characters). Consider making it more descriptive.");

        var metaDesc = ExtractMetaDescription(html);
        if (metaDesc == "No meta description found")
            recommendations.Add("Add a meta description to improve search engine results");
        else if (metaDesc.Length > 160)
            recommendations.Add("Meta description is too long (>160 characters)");

        var h1Count = Regex.Matches(html, @"<h1[^>]*>", RegexOptions.IgnoreCase).Count;
        if (h1Count == 0)
            recommendations.Add("Add an H1 heading to your page");
        else if (h1Count > 1)
            recommendations.Add("Multiple H1 tags found. Consider using only one H1 per page.");

        var imgMatches = Regex.Matches(html, @"<img[^>]*>", RegexOptions.IgnoreCase);
        var imagesWithoutAlt = 0;
        foreach (Match match in imgMatches)
        {
            if (!Regex.IsMatch(match.Value, @"alt\s*=\s*[""'][^""']+[""']", RegexOptions.IgnoreCase))
                imagesWithoutAlt++;
        }

        if (imagesWithoutAlt > 0)
            recommendations.Add(
                $"{imagesWithoutAlt} image(s) missing alt attributes. Add alt text for accessibility and SEO.");

        if (!CheckStructuredData(html))
            recommendations.Add("Consider adding structured data (JSON-LD or Schema.org markup)");

        if (recommendations.Count == 0)
            recommendations.Add("Great! No major SEO issues detected.");

        return recommendations;
    }
}