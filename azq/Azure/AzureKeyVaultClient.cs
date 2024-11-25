using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.Resources;
using Azure.Security.KeyVault.Secrets;
using FuzzySharp;
using Spectre.Console;

namespace azq.Azure;

public class AzureKeyVaultClient
{
    private readonly DefaultAzureCredential _credential;
    private readonly ArmClient _armClient;
    
    public AzureKeyVaultClient()
    {
        _credential = new DefaultAzureCredential();
        _armClient = new ArmClient(_credential);
    }

    private SecretClient? SecretClient { get; set; }
    
    public async Task<List<KeyVaultResource>> GetKeyVaults(List<SubscriptionResource> subscriptions)
    {
        List<KeyVaultResource> keyVaults = [];
        
        foreach (var subscription in subscriptions)
        {
            await foreach (var keyVault in subscription.GetKeyVaultsAsync())
            {
                keyVaults.Add(keyVault);
            }    
        }
        
        return keyVaults;
    }
    
    public async Task<IEnumerable<SecretProperties>> GetSecretsBySearchTerm(KeyVaultResource keyVault, string secretName)
    {
        var keyVaultUrl = keyVault.Data.Properties.VaultUri;
        SecretClient = new SecretClient(keyVaultUrl, _credential);
        
        List<(int Ratio, SecretProperties Secret)> secrets = [];
        await foreach (var secret in SecretClient.GetPropertiesOfSecretsAsync())
        {
            var ratio = Fuzz.PartialRatio(secretName.ToLower(), secret.Name.ToLower());
            if (ratio > 70)
            {
                secrets.Add((ratio, secret));
            }
        }

        secrets.Sort((x, y) => y.Ratio.CompareTo(x.Ratio));

        // foreach (var secret in secrets)
        // {
        //     AnsiConsole.MarkupLine("[bold green]{0}[/] ({1})", secret.Secret.Name, secret.Ratio);
        // }

        return secrets.Select(x => x.Secret);
    }
    
    public async Task<KeyVaultSecret> GetSecret(SecretProperties secret)
    {
        if (SecretClient is null)
            throw new ArgumentNullException();
        return await SecretClient.GetSecretAsync(secret.Name);
    }
}