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
        [CommandArgument(0, "[SearchTerm]")]
        public string? SearchTerm { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, KvSettings settings)
    {
        // Fetch subscription
        List<SubscriptionResource> subscriptions = [];
        
        await AnsiConsole.Status().Spinner(Constants.DefaultSpinner).StartAsync("Fetching subscriptions from Azure...", async ctx =>
        {
            subscriptions.AddRange(await _subscriptionClient.GetSubscriptions());
        });

        if (subscriptions.Count == 0)
            return 1;
        
        // AnsiConsole.MarkupLine("Current subscription: [bold cyan]{0}[/]", subscription.Data.DisplayName);
        
        // Fetch key vaults
        List<KeyVaultResource> keyVaults = [];
        
        await AnsiConsole.Status().Spinner(Constants.DefaultSpinner).StartAsync("Fetching key vaults from Azure...", async ctx =>
        {
            keyVaults.AddRange(await _keyVaultClient.GetKeyVaults(subscriptions));
        });
        
        var selectedKeyVault = keyVaults.Count switch {
            0 => null,
            1 => keyVaults[0],
            _ => PromptKeyVault(keyVaults)
        };

        if (selectedKeyVault is null)
        {
            AnsiConsole.MarkupLine(keyVaults.Count == 0 ? "No key vaults found" : "No key vault selected");
            return 0;
        }

        AnsiConsole.MarkupLine("Selected key vault: [bold cyan]{0}[/]", selectedKeyVault.Data.Name);
        
        // Search for secret
        var secretName = string.IsNullOrWhiteSpace(settings.SearchTerm) ?
            AnsiConsole.Ask<string>("Enter search term:") :
            settings.SearchTerm;

        List<SecretProperties> secrets = [];
        
        await AnsiConsole.Status().Spinner(Constants.DefaultSpinner).StartAsync("Searching for matching secrets...", async ctx =>
        {
            secrets.AddRange(await _keyVaultClient.GetSecretsBySearchTerm(selectedKeyVault, secretName));
        });
        
        // Select secret
        var selectedSecret = secrets.Count switch {
            0 => null,
            1 => secrets[0],
            _ => PromptSecret(secrets)
        };
        
        if (selectedSecret is null)
        {
            AnsiConsole.MarkupLine(secrets.Count == 0 ? "No matching secrets found" : "No secret selected");
            return 0;
        }
        
        AnsiConsole.MarkupLine("Selected secret: [bold cyan]{0}[/]", selectedSecret.Name);

        KeyVaultSecret? secretValue = null;
        
        // Fetch and print secret
        await AnsiConsole.Status().Spinner(Constants.DefaultSpinner).StartAsync("Fetching secret value...", async ctx =>
        {
            secretValue = await _keyVaultClient.GetSecret(selectedSecret);
        });

        switch (secretValue)
        {
            case not null:
                PrintSecret(secretValue);
                AnsiConsole.WriteLine();
                break;
            
            case null:
                AnsiConsole.MarkupLine("[red]Failed to fetch secret value[/]");
                return 1;
        }
        
        // Prompt for copy to clipboard
        var copyToClipboard = AnsiConsole.Confirm("Copy secret value to clipboard?");

        if (!copyToClipboard) return 0;
        
        await ClipboardService.SetTextAsync(secretValue.Value);
        AnsiConsole.MarkupLine("[bold green]Secret value copied to clipboard[/]");

        return 0;
    }

    private static void PrintSecret(KeyVaultSecret secretValue)
    {
        AnsiConsole.WriteLine();
        var panel = new Panel(secretValue.Value)
            .Padding(1, 1)
            .Header("Secret value");
        
        AnsiConsole.Write(panel);
    }

    private static SecretProperties PromptSecret(List<SecretProperties> secrets)
    {
        var selectedSecret = AnsiConsole.Prompt(
            new SelectionPrompt<SecretProperties>()
                .Title("Select a secret")
                .PageSize(20)
                .AddChoices(secrets)
                .UseConverter(secret => secret.Name));
        return selectedSecret;
    }

    private static KeyVaultResource PromptKeyVault(List<KeyVaultResource> keyVaults)
    {
        KeyVaultResource selectedKeyVault;
        selectedKeyVault = AnsiConsole.Prompt(
            new SelectionPrompt<KeyVaultResource>()
                .Title("Select a key vault")
                .PageSize(20)
                .AddChoices(keyVaults)
                .UseConverter(keyvault => $"{keyvault.Data.Name} ({keyvault.Data.Id.ResourceGroupName})"));
        return selectedKeyVault;
    }
}