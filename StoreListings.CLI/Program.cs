// See https://aka.ms/new-console-template for more information
using ConsoleAppFramework;
using StoreListings.CLI;

ConsoleApp.ConsoleAppBuilder builder = ConsoleApp.Create();
builder.Add<Commands>();
await builder.RunAsync(args);