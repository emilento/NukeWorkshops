using System.Collections.Generic;
using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using Deployment = Pulumi.Deployment;

return await Deployment.RunAsync(() =>
{
    var resourceGroup = new ResourceGroup($"rg-{Deployment.Instance.ProjectName.ToLowerInvariant()}-{Deployment.Instance.StackName}");

    var appServicePlan = new AppServicePlan(
        $"asp-{Deployment.Instance.ProjectName.ToLowerInvariant()}-{Deployment.Instance.StackName}",
        new AppServicePlanArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Kind = "Windows",
            Sku = new SkuDescriptionArgs
            {
                Tier = "Basic",
                Name = "B1",
            },
        });

    var webApp = new WebApp(
        $"app-{Deployment.Instance.ProjectName.ToLowerInvariant()}-{Deployment.Instance.StackName}",
        new WebAppArgs
        {
            Kind = "app",
            ResourceGroupName = resourceGroup.Name,
            ServerFarmId = appServicePlan.Id,
            SiteConfig = new SiteConfigArgs
            {
                AppSettings = new[]
                {
                    new NameValuePairArgs
                    {
                        Name = "ASPNETCORE_ENVIRONMENT",
                        Value = "Development",
                    }
                },
                NetFrameworkVersion = "v8.0",
                WindowsFxVersion = "DOTNETCORE|8.0",
            }
        });

    var publishingCredentials = ListWebAppPublishingCredentials.Invoke(new()
    {
        ResourceGroupName = resourceGroup.Name,
        Name = webApp.Name
    });

    return new Dictionary<string, object?>
    {
        ["publishingUserName"] = Output.CreateSecret(publishingCredentials.Apply(c => c.PublishingUserName)),
        ["publishingPassword"] = Output.CreateSecret(publishingCredentials.Apply(c => c.PublishingPassword)),
        ["webAppName"] = webApp.Name
    };
});
