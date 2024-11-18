// See https://aka.ms/new-console-template for more information

using System.Text;
using azq.Azure;
using azq.Commands;
using azq.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var serviceCollection = new ServiceCollection();
serviceCollection.AddTransient<AzureSubscriptionClient>();
serviceCollection.AddTransient<AzureKeyVaultClient>();
var registrar = new TypeRegistrar(serviceCollection);

var app = new CommandApp(registrar);
app.Configure(config =>
{
    config.AddCommand<KeyVaultCommand>("keyvault")
        .WithAlias("kv")
        .WithDescription("(or [b yellow]kv[/]) - Fetch secrets from Azure Key Vault");
    config.AddBranch("subscription", subscription =>
    {
        subscription.SetDescription("(or [b yellow]sub[/]) - Manage Azure subscriptions");
        subscription.AddCommand<ListSubscriptionsCommand>("list")
            .WithDescription("List subscriptions");
        subscription.AddCommand<SetSubscriptionCommand>("set")
            .WithDescription("Set current subscription");
    }).WithAlias("sub");

});

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

return app.Run(args);
