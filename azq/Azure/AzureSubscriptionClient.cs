using System.Diagnostics;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

namespace azq.Azure;

public class AzureSubscriptionClient
{
    private readonly TokenCredential _credential;
    private readonly ArmClient _armClient;

    public AzureSubscriptionClient()
    {
        _credential = new AzureCliCredential();
        _armClient = new ArmClient(_credential);

    }

    public async Task<List<SubscriptionResource>> GetSubscriptions()
    {
        List<SubscriptionResource> subscriptions = new();
        await foreach(var tenant in _armClient.GetTenants().GetAllAsync())
        {
            Console.WriteLine(tenant.Data.TenantId);
            await foreach (var subscription in tenant.GetSubscriptions().GetAllAsync())
            {
                if (subscriptions.Any(s => s.Data.SubscriptionId == subscription.Data.SubscriptionId))
                    continue;
                
                subscriptions.Add(subscription);
            }
        }
        

        return subscriptions;
    }

    [Obsolete("not working properly", true)]
    public async Task<SubscriptionResource> GetDefaultSubscription()
    {
        return await _armClient.GetDefaultSubscriptionAsync();
    }

    public void SetSubscription(SubscriptionResource subscription)
    {
        if (string.IsNullOrWhiteSpace(subscription.Id.SubscriptionId))
            return;

        SetAzureCliSubscription(subscription.Id.SubscriptionId);
    }

    static void SetAzureCliSubscription(string subscriptionId)
    {
        var appName = "az";
        
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            appName += ".cmd";
        }
        
        var appPath = GetAppPath(appName);
        
        var processInfo = new ProcessStartInfo
        {
            FileName = appPath,
            Arguments = $"account set --subscription {subscriptionId}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = processInfo };
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            Console.WriteLine($"Successfully set the subscription to {subscriptionId}");
        }
        else
        {
            Console.WriteLine($"Error setting the subscription: {error}");
        }
    }
    
    static string? GetAppPath(string appName)
    {
        foreach (var dir in Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator))
        {
            string fullPath = Path.Combine(dir, appName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        return null;
    }
}