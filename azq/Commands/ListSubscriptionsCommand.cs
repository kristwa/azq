using azq.Azure;
using Azure.ResourceManager.Resources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace azq.Commands;

public class ListSubscriptionsCommand : AsyncCommand<ListSubscriptionsCommand.Settings>
{
    private readonly AzureSubscriptionClient _subscriptionClient;

    public ListSubscriptionsCommand(AzureSubscriptionClient subscriptionClient)
    {
        _subscriptionClient = subscriptionClient;
    }
    
    public sealed class Settings : CommandSettings
    {
        
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        List<SubscriptionResource> subscriptions = [];
        
        await AnsiConsole.Status().Spinner(Constants.DefaultSpinner).StartAsync("Fetching subscriptions from Azure...", async ctx =>
        {
            subscriptions = await _subscriptionClient.GetSubscriptions();
        });
        
        if (subscriptions.Count == 0)
        {
            AnsiConsole.MarkupLine("No subscriptions found");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Subscription name");
        table.AddColumn("Subscription ID");
        table.AddColumn("Tenant ID");
        
        foreach (var subscription in subscriptions)
        {
            table.AddRow(subscription.Data.DisplayName, subscription.Data.SubscriptionId, subscription.Data.TenantId.ToString() ?? string.Empty);
        }
        
        AnsiConsole.Write(table);
        
        return 0;
    }
}