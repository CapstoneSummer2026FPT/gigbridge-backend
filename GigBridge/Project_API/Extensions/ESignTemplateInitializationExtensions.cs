using Application.Common.Interfaces.Templates;
using Domain.Entities;
using Domain.Enums.Accounts;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Project_API.Extensions;

public static class ESignTemplateInitializationExtensions
{
    private const string FixedPriceTemplateCode = "CONTRACT_FIXED_PRICE";
    private const string TemplatePath = "ESign/Seed/ContractFixedPriceTemporary.html";

    public static async Task EnsureLocalESignTemplatesAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Local"))
        {
            return;
        }

        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ESignTemplateInitializer");

        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GigbridgeDbContext>();
            var templateReader = scope.ServiceProvider.GetRequiredService<ITemplateReader>();


            var hasActiveFixedPriceTemplate = await context.EsignTemplates
                .AnyAsync(template =>
                    template.TemplateCode == FixedPriceTemplateCode &&
                    template.IsActive);

            if (hasActiveFixedPriceTemplate)
            {
                return;
            }

            var creator = await context.Users
                .Where(user => user.IsActive && user.Role == (int)UserRole.Admin)
                .OrderBy(user => user.CreatedAt)
                .FirstOrDefaultAsync()
                ?? await context.Users
                    .Where(user => user.IsActive)
                    .OrderBy(user => user.CreatedAt)
                    .FirstOrDefaultAsync();

            if (creator is null)
            {
                logger.LogWarning(
                    "Temporary e-sign template {TemplateCode} was not created because no active user exists for CreatedBy.",
                    FixedPriceTemplateCode);
                return;
            }

            var latestVersion = await context.EsignTemplates
                .Where(template => template.TemplateCode == FixedPriceTemplateCode)
                .Select(template => (int?)template.Version)
                .MaxAsync() ?? 0;

            context.EsignTemplates.Add(new EsignTemplate
            {
                EsignTemplatesId = Guid.NewGuid(),
                Name = "Temporary Fixed Price Agreement",
                TemplateCode = FixedPriceTemplateCode,
                HtmlContent = await templateReader.ReadTextAsync(TemplatePath),
                Version = latestVersion + 1,
                PlaceholderSchema = """
                    {
                      "Contract.Title": "string",
                      "Contract.Description": "string",
                      "Contract.TotalBudget": "string",
                      "Contract.StartDate": "string",
                      "Contract.EndDate": "string",
                      "Client.Name": "string",
                      "Client.Email": "string",
                      "Freelancer.Name": "string",
                      "Freelancer.Email": "string",
                      "MilestonesHtml": "html"
                    }
                    """,
                Description = "Temporary fixed-price contract template for local negotiation e-sign testing.",
                IsActive = true,
                CreatedBy = creator.UserId,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();

            logger.LogInformation(
                "Created temporary active e-sign template {TemplateCode}.",
                FixedPriceTemplateCode);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Temporary e-sign template initialization failed.");
        }
    }
}
