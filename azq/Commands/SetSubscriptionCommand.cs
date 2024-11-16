using azq.Azure;
using Azure.ResourceManager.Resources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace azq.Commands;

public class SetSubscriptionCommand : AsyncCommand<SetSubscriptionCommand.Settings>
{
    private readonly AzureSubscriptionClient _subscriptionClient;

    public SetSubscriptionCommand(AzureSubscriptionClient subscriptionClient)
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
        
        var selectedSubscription = AnsiConsole.Prompt(
            new SelectionPrompt<SubscriptionResource>()
                .Title("Select a subscription")
                .PageSize(10)
                .AddChoices(subscriptions)
            
                .UseConverter(subscription => subscription.Data.DisplayName));
        
        
        _subscriptionClient.SetSubscription(selectedSubscription);
        
        return 0;
    }
}