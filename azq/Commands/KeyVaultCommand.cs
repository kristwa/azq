using azq.Azure;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.Resources;
using Azure.Security.KeyVault.Secrets;
using Spectre.Console;
using Spectre.Console.Cli;
using TextCopy;

namespace azq.Commands;

public class KeyVaultCommand : AsyncCommand<KeyVaultCommand.KvSettings>
{
    private readonly AzureSubscriptionClient _subscriptionClient;
    private readonly AzureKeyVaultClient _keyVaultClient;

    public KeyVaultCommand(AzureSubscriptionClient subscriptionClient, AzureKeyVaultClient keyVaultClient)
    {
        _subscriptionClient = subscriptionClient;
        _keyVaultClient = keyVaultClient;
    }
    
    public sealed class KvSettings : CommandSettings
    {
        
    }

    public override async Task<int> ExecuteAsync(CommandContext context, KvSettings settings)
    {
        SubscriptionResource? subscription = null;
        
        await AnsiConsole.Status().Spinner(Constants.DefaultSpinner).StartAsync("Fetching subscriptions from Azure...", async ctx =>
        {
            subscription = await _subscriptionClient.GetDefaultSubscription();
        });

        if (subscription is null)
            return 1;
        
        AnsiConsole.MarkupLine("Current subscription: [bold cyan]{0}[/]", subscription.Data.DisplayName);
        
        List<KeyVaultResource> keyVaults = [];
        
        await AnsiConsole.Status().Spinner(Constants.DefaultSpinner).StartAsync("Fetching key vaults from Azure...", async ctx =>
        {
            keyVaults.AddRange(await _keyVaultClient.GetKeyVaults(subscription));
        });
        
        if (keyVaults.Count == 0)
        {
            AnsiConsole.MarkupLine("No key vaults found");
            return 0;
        }
                
        var selectedKeyVault = AnsiConsole.Prompt(
            new SelectionPrompt<KeyVaultResource>()
                .Title("Select a key vault")
                .PageSize(20)
                .AddChoices(keyVaults)
                .UseConverter(keyvault => $"{keyvault.Data.Name} ({keyvault.Data.Id.ResourceGroupName})"));
        
        AnsiConsole.MarkupLine("Selected key vault: [bold cyan]{0}[/]", selectedKeyVault.Data.Name);
        
        var secretName = AnsiConsole.Ask<string>("Enter search term:");

        List<SecretProperties> secrets = [];
        
        await AnsiConsole.Status().Spinner(Constants.DefaultSpinner).StartAsync("Searching for matching secrets...", async ctx =>
        {
            secrets.AddRange(await _keyVaultClient.GetSecretsBySearchTerm(selectedKeyVault, secretName));
        });
        
        if (secrets.Count == 0)
        {
            AnsiConsole.MarkupLine("No matching secrets found");
            return 0;
        }
        
        var selectedSecret = AnsiConsole.Prompt(
            new SelectionPrompt<SecretProperties>()
                .Title("Select a secret")
                .PageSize(20)
                .AddChoices(secrets)
                .UseConverter(secret => secret.Name));
        
        AnsiConsole.MarkupLine("Selected secret: [bold cyan]{0}[/]", selectedSecret.Name);

        KeyVaultSecret? secretValue = null;
        
        await AnsiConsole.Status().Spinner(Constants.DefaultSpinner).StartAsync("Fetching secret value...", async ctx =>
        {
            secretValue = await _keyVaultClient.GetSecret(selectedSecret);
        });

        if (secretValue is null) return 0;
        
        Console.WriteLine(secretValue.Value);

        var copyToClipboard = AnsiConsole.Confirm("Copy secret value to clipboard?");

        if (!copyToClipboard) return 0;
        
        await ClipboardService.SetTextAsync(secretValue.Value);
        AnsiConsole.MarkupLine("[bold green]Secret value copied to clipboard[/]");

        return 0;
    }
}